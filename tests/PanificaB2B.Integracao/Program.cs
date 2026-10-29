using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using PanificaB2B.Api.Dados;
using PanificaB2B.Contratos;

namespace PanificaB2B.Integracao;

public static partial class Program
{
    static int quantidade;
    static void Verificar(bool condicao, string nome) { if (!condicao) throw new Exception(nome); quantidade++; Console.WriteLine($"OK: {nome}"); }
    static async Task<JsonElement> Requisicao(HttpClient client, HttpMethod metodo, string caminho, object? dados = null, HttpStatusCode esperado = HttpStatusCode.OK)
    {
        using var request = new HttpRequestMessage(metodo, caminho);
        if (dados is not null) request.Content = JsonContent.Create(dados);
        using var response = await client.SendAsync(request);
        var texto = await response.Content.ReadAsStringAsync();
        if (response.StatusCode != esperado) throw new Exception($"{metodo} {caminho}: {(int)response.StatusCode}; esperado {(int)esperado}. {texto.Split("HEADERS")[0]}");
        return texto.Length == 0 ? default : JsonSerializer.Deserialize<JsonElement>(texto);
    }
    static async Task<HttpClient> Entrar(FabricaApi api, string login, string senha, object? extra = null)
    {
        var client = api.CreateClient();
        var token = await Requisicao(client, HttpMethod.Post, "/api/auth/entrar", extra ?? new { login, senha });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.GetProperty("accessToken").GetString());
        return client;
    }
    static async Task<(string Caminho, string Texto)> Formulario(HttpClient site, string caminho, Dictionary<string, string> dados, string? paginaOrigem = null)
    {
        var pagina = await site.GetStringAsync(paginaOrigem ?? caminho);
        var match = Regex.Match(pagina, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        if (!match.Success) throw new Exception("Token do formulário ausente.");
        dados["__RequestVerificationToken"] = WebUtility.HtmlDecode(match.Groups[1].Value);
        using var result = await site.PostAsync(caminho, new FormUrlEncodedContent(dados));
        result.EnsureSuccessStatusCode();
        return (result.RequestMessage!.RequestUri!.AbsolutePath, await result.Content.ReadAsStringAsync());
    }
    public static async Task Main(string[] args)
    {
        var raiz = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var pasta = Path.Combine(raiz, "tests", "PanificaB2B.Integracao", "App_Data", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pasta);
        Dictionary<string, long>? contagens = null;
        if (args.Length > 0)
        {
            using var origem = new SqliteConnection($"Data Source={Path.GetFullPath(args[0])};Mode=ReadOnly"); origem.Open();
            using var destino = new SqliteConnection($"Data Source={Path.Combine(pasta, "banco.db")}"); destino.Open();
            origem.BackupDatabase(destino);
            contagens = new();
            foreach (var tabela in new[] { "Estados", "Cidades", "Vendedores", "Territorios", "TerritorioCidades", "Clientes", "Roteiros", "ItensRoteiro" })
            {
                using var cmd = destino.CreateCommand(); cmd.CommandText = $"SELECT COUNT(*) FROM \"{tabela}\"";
                contagens[tabela] = (long)cmd.ExecuteScalar()!;
            }
        }
        using var api = new FabricaApi(raiz, pasta);
        using var anonimo = api.CreateClient();
        using var siteFactory = new FabricaSite(raiz, pasta, api);
        using var site = siteFactory.CreateClient();
        using var banco = new SqliteConnection($"Data Source={Path.Combine(pasta, "banco.db")}"); banco.Open();
        if (contagens is not null)
        {
            foreach (var (tabela, total) in contagens)
            {
                using var cmd = banco.CreateCommand(); cmd.CommandText = $"SELECT COUNT(*) FROM \"{tabela}\"";
                Verificar((long)cmd.ExecuteScalar()! == total, $"Atualização preserva {tabela}");
            }
        }
        await Requisicao(anonimo, HttpMethod.Get, "/api/clientes", esperado: HttpStatusCode.Unauthorized);
        Verificar(true, "API exige autenticação");
        await Requisicao(anonimo, HttpMethod.Get, "/api/grupos-produto", esperado: HttpStatusCode.Unauthorized);
        Verificar(true, "Grupos exigem autenticação");
        var config = await Requisicao(anonimo, HttpMethod.Get, "/api/auth/configuracao");
        Verificar(!config.GetProperty("configurado").GetBoolean(), "Sem credenciais padrão");
        using (var pagina = await site.GetAsync("/Clientes")) Verificar(pagina.RequestMessage!.RequestUri!.AbsolutePath == "/Conta/Configurar", "Primeiro acesso abre configuração do administrador");
        var senhaAdmin = Convert.ToHexString(RandomNumberGenerator.GetBytes(20));
        var senhaVendedor = Convert.ToHexString(RandomNumberGenerator.GetBytes(20));
        var primeiro = await Formulario(site, "/Conta/Configurar", new() { ["Login"] = "admin.teste", ["Senha"] = senhaAdmin });
        Verificar(primeiro.Caminho == "/Conta/Entrar", "Administrador inicial criado pelo site");
        await Requisicao(anonimo, HttpMethod.Post, "/api/auth/configurar", new { login = "outro.admin", senha = senhaAdmin }, HttpStatusCode.Conflict);
        Verificar(true, "Configuração inicial não se repete");
        await Requisicao(anonimo, HttpMethod.Post, "/api/auth/entrar", new { login = "admin.teste", senha = "senha-incorreta-123" }, HttpStatusCode.Unauthorized);
        Verificar(true, "Senha incorreta é recusada");
        using var admin = await Entrar(api, "ADMIN.TESTE", senhaAdmin);
        var identidade = await Requisicao(admin, HttpMethod.Get, "/api/auth/eu");
        Verificar(identidade.GetProperty("perfil").GetInt32() == 1, "Login normalizado identifica administrador");
        var grupo = (await Requisicao(admin, HttpMethod.Post, "/api/grupos-produto", new { nome = "  Fermentos   ", ativo = true }, HttpStatusCode.Created)).Deserialize<GrupoProdutoDto>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Verificar(grupo.Nome == "Fermentos" && grupo.Versao == 1, "Grupo é criado com nome normalizado e versão inicial");
        await Requisicao(admin, HttpMethod.Post, "/api/grupos-produto", new { nome = "fermentos" }, HttpStatusCode.BadRequest);
        Verificar(true, "Nome de grupo duplicado é recusado sem diferenciar maiúsculas");
        await Requisicao(admin, HttpMethod.Post, "/api/grupos-produto", new { nome = "   " }, HttpStatusCode.BadRequest);
        await Requisicao(admin, HttpMethod.Post, "/api/grupos-produto", new { nome = new string('A', 101) }, HttpStatusCode.BadRequest);
        Verificar(true, "Nome de grupo vazio ou longo é recusado");
        var inativo = (await Requisicao(admin, HttpMethod.Put, $"/api/grupos-produto/{grupo.Id}", new { nome = "Fermentos e melhoradores", ativo = false, versao = grupo.Versao })).Deserialize<GrupoProdutoDto>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Verificar(!inativo.Ativo && inativo.Versao == 2, "Grupo pode ser editado e inativado sem exclusão");
        await Requisicao(admin, HttpMethod.Put, $"/api/grupos-produto/{grupo.Id}", new { nome = "Sobrescrever", ativo = true, versao = 1 }, HttpStatusCode.Conflict);
        Verificar(true, "Edição antiga de grupo não sobrescreve versão atual");
        await Requisicao(admin, HttpMethod.Post, "/api/grupos-produto", new { nome = "fermentos e melhoradores" }, HttpStatusCode.BadRequest);
        Verificar(true, "Grupo inativo continua protegido contra duplicação");
        var ativo = (await Requisicao(admin, HttpMethod.Post, "/api/grupos-produto", new { nome = "Misturas" }, HttpStatusCode.Created)).Deserialize<GrupoProdutoDto>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        await Requisicao(admin, HttpMethod.Get, $"/api/grupos-produto/{Guid.NewGuid()}", esperado: HttpStatusCode.NotFound);
        Verificar(true, "Consulta de grupo inexistente retorna não encontrado");
        using (var cmd = banco.CreateCommand())
        {
            cmd.CommandText = "SELECT SenhaHash FROM Usuarios WHERE LoginNormalizado='ADMIN.TESTE'";
            Verificar((string)cmd.ExecuteScalar()! != senhaAdmin, "Senha armazenada como hash");
        }
        var vendedores = (await admin.GetFromJsonAsync<List<VendedorDto>>("/api/vendedores"))!;
        var henrique = vendedores.Single(v => v.Nome == "Henrique"); var ana = vendedores.Single(v => v.Nome == "Ana");
        await Requisicao(admin, HttpMethod.Post, "/api/auth/usuarios", new { login = "vendedor.teste", senha = senhaVendedor, perfil = 2 }, HttpStatusCode.BadRequest);
        Verificar(true, "Conta de vendedor exige vínculo");
        var novoUsuario = await Requisicao(admin, HttpMethod.Post, "/api/auth/usuarios", new { login = "vendedor.teste", senha = senhaVendedor, perfil = 2, vendedorId = henrique.Id });
        Verificar(!novoUsuario.ToString().Contains("senha", StringComparison.OrdinalIgnoreCase), "Resposta não expõe hash ou senha");
        await Requisicao(admin, HttpMethod.Post, "/api/auth/usuarios", new { login = "VENDEDOR.TESTE", senha = senhaVendedor, perfil = 1 }, HttpStatusCode.Conflict);
        Verificar(true, "Login duplicado é recusado");
        using var vendedor = await Entrar(api, "vendedor.teste", senhaVendedor);
        var gruposVisiveis = (await vendedor.GetFromJsonAsync<List<GrupoProdutoDto>>("/api/grupos-produto"))!;
        Verificar(gruposVisiveis.Any(g => g.Id == ativo.Id) && gruposVisiveis.All(g => g.Id != inativo.Id), "Vendedor consulta somente grupos ativos");
        await Requisicao(vendedor, HttpMethod.Get, $"/api/grupos-produto/{inativo.Id}", esperado: HttpStatusCode.NotFound);
        await Requisicao(vendedor, HttpMethod.Post, "/api/grupos-produto", new { nome = "Não autorizado" }, HttpStatusCode.Forbidden);
        await Requisicao(vendedor, HttpMethod.Put, $"/api/grupos-produto/{ativo.Id}", new { nome = "Não autorizado", ativo = false, versao = 1 }, HttpStatusCode.Forbidden);
        Verificar(true, "Vendedor não gerencia grupos nem consulta grupo inativo");
        var visiveis = (await vendedor.GetFromJsonAsync<List<VendedorDto>>("/api/vendedores"))!;
        Verificar(visiveis.Count == 1 && visiveis[0].Id == henrique.Id, "Vendedor só recebe a própria identidade comercial");
        await Requisicao(vendedor, HttpMethod.Get, "/api/auth/usuarios", esperado: HttpStatusCode.Forbidden);
        await Requisicao(vendedor, HttpMethod.Post, "/api/auth/usuarios", new { login = "invasor", senha = senhaVendedor, perfil = 1 }, HttpStatusCode.Forbidden);
        Verificar(true, "Vendedor não gerencia usuários");
        await Requisicao(vendedor, HttpMethod.Post, "/api/estados", new { nome = "Teste", sigla = "TT" }, HttpStatusCode.Forbidden);
        await Requisicao(vendedor, HttpMethod.Post, "/api/roteiros", new { }, HttpStatusCode.Forbidden);
        Verificar(true, "Gestão de cadastros e roteiros é administrativa");
        var carteira = (await vendedor.GetFromJsonAsync<PreparacaoDto>($"/api/vendedores/{henrique.Id}/preparacao"))!;
        await Requisicao(vendedor, HttpMethod.Get, $"/api/vendedores/{ana.Id}/preparacao", esperado: HttpStatusCode.Forbidden);
        await Requisicao(vendedor, HttpMethod.Get, $"/api/roteiros?vendedorId={ana.Id}", esperado: HttpStatusCode.Forbidden);
        Verificar(true, "Trocar o vendedor na URL não concede acesso");
        var cidade = carteira.Cidades.First();
        var outroTerritorio = (await Requisicao(admin, HttpMethod.Post, "/api/territorios", new { nome = "Área de Ana", vendedorId = ana.Id, cidadeIds = new[] { cidade.Id }, ativo = true })).Deserialize<TerritorioDto>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var clienteOutro = (await Requisicao(admin, HttpMethod.Post, "/api/clientes", new { nome = "Cliente de Ana", endereco = "Rua de teste", cidadeId = cidade.Id, territorioId = outroTerritorio.Id })).Deserialize<ClienteDto>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var clientesVisiveis = (await vendedor.GetFromJsonAsync<List<ClienteDto>>("/api/clientes"))!;
        Verificar(clientesVisiveis.All(c => c.Id != clienteOutro.Id), "Consulta de clientes é filtrada na API");
        var proprio = (await Requisicao(vendedor, HttpMethod.Post, "/api/clientes", new { id = Guid.NewGuid(), nome = "Novo cliente Henrique", endereco = "Rua de teste", cidadeId = cidade.Id, territorioId = carteira.Territorios.First().Id })).Deserialize<ClienteDto>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Verificar(proprio.Nome == "Novo cliente Henrique", "Vendedor cadastra na própria carteira");
        var tentativa = new { id = clienteOutro.Id, nome = clienteOutro.Nome, endereco = clienteOutro.Endereco, cidadeId = cidade.Id, territorioId = carteira.Territorios.First().Id, versao = clienteOutro.Versao };
        await Requisicao(vendedor, HttpMethod.Put, $"/api/clientes/{clienteOutro.Id}", tentativa, HttpStatusCode.Forbidden);
        await Requisicao(vendedor, HttpMethod.Post, "/api/clientes", tentativa, HttpStatusCode.Forbidden);
        await Requisicao(vendedor, HttpMethod.Put, $"/api/clientes/{proprio.Id}", new { proprio.Nome, proprio.Endereco, proprio.CidadeId, territorioId = outroTerritorio.Id, proprio.Versao }, HttpStatusCode.Forbidden);
        Verificar(true, "Alteração e reenvio não transferem clientes de outra carteira");
        var historico = await Requisicao(admin, HttpMethod.Post, "/api/roteiros", new { nome = "Roteiro antigo", vendedorId = henrique.Id, diaSemana = 1, ativo = false, visitas = new[] { new { clienteId = proprio.Id, sequencia = 10 } } });
        await Requisicao(admin, HttpMethod.Put, $"/api/clientes/{proprio.Id}", new { proprio.Nome, proprio.Endereco, proprio.CidadeId, territorioId = outroTerritorio.Id, proprio.Versao, ativo = true });
        Verificar((await vendedor.GetFromJsonAsync<List<ClienteDto>>("/api/clientes"))!.All(c => c.Id != proprio.Id), "Transferência pela empresa retira o cliente da carteira anterior");
        Verificar((await vendedor.GetFromJsonAsync<List<RoteiroDto>>("/api/roteiros"))!.All(r => r.Id != historico.GetProperty("id").GetGuid()), "Roteiro inativo não expõe clientes transferidos ao vendedor anterior");
        using var outraSessao = await Entrar(api, "vendedor.teste", senhaVendedor, new { login = "vendedor.teste", senha = senhaVendedor, perfil = 1, vendedorId = ana.Id });
        var eu = await Requisicao(outraSessao, HttpMethod.Get, "/api/auth/eu");
        Verificar(eu.GetProperty("perfil").GetInt32() == 2 && eu.GetProperty("vendedorId").GetGuid() == henrique.Id, "Campos extras no login não alteram privilégios");
        using var adulterado = api.CreateClient(); adulterado.DefaultRequestHeaders.Authorization = new("Bearer", vendedor.DefaultRequestHeaders.Authorization!.Parameter + "x");
        await Requisicao(adulterado, HttpMethod.Get, "/api/clientes", esperado: HttpStatusCode.Unauthorized);
        Verificar(true, "Token adulterado é recusado");
        await Requisicao(vendedor, HttpMethod.Post, "/api/auth/sair", esperado: HttpStatusCode.NoContent);
        await Requisicao(vendedor, HttpMethod.Get, "/api/clientes", esperado: HttpStatusCode.Unauthorized);
        await Requisicao(outraSessao, HttpMethod.Get, "/api/auth/eu");
        Verificar(true, "Logout revoga somente a sessão usada");
        using (var cmd = banco.CreateCommand()) { cmd.CommandText = "UPDATE Usuarios SET Ativo=0 WHERE LoginNormalizado='VENDEDOR.TESTE'"; cmd.ExecuteNonQuery(); }
        await Requisicao(outraSessao, HttpMethod.Get, "/api/clientes", esperado: HttpStatusCode.Unauthorized);
        Verificar(true, "Usuário inativo perde acesso com token antigo");
        using (var cmd = banco.CreateCommand()) { cmd.CommandText = "UPDATE Usuarios SET Ativo=1 WHERE LoginNormalizado='VENDEDOR.TESTE'"; cmd.ExecuteNonQuery(); }
        using (var cmd = banco.CreateCommand()) { cmd.CommandText = "UPDATE Vendedores SET Ativo=0 WHERE Id=$id"; cmd.Parameters.AddWithValue("$id", henrique.Id); cmd.ExecuteNonQuery(); }
        await Requisicao(outraSessao, HttpMethod.Get, "/api/clientes", esperado: HttpStatusCode.Unauthorized);
        Verificar(true, "Vendedor inativo perde acesso com token antigo");
        using (var cmd = banco.CreateCommand()) { cmd.CommandText = "UPDATE Vendedores SET Ativo=1 WHERE Id=$id"; cmd.Parameters.AddWithValue("$id", henrique.Id); cmd.ExecuteNonQuery(); }
        using (var cmd = banco.CreateCommand()) { cmd.CommandText = "UPDATE SessoesAcesso SET ExpiraEm='2000-01-01 00:00:00+00:00' WHERE UsuarioId=(SELECT Id FROM Usuarios WHERE LoginNormalizado='VENDEDOR.TESTE')"; cmd.ExecuteNonQuery(); }
        await Requisicao(outraSessao, HttpMethod.Get, "/api/clientes", esperado: HttpStatusCode.Unauthorized);
        Verificar(true, "Sessão expirada é recusada");
        var acessoSite = await Formulario(site, "/Conta/Entrar", new() { ["Login"] = "admin.teste", ["Senha"] = senhaAdmin });
        Verificar(acessoSite.Caminho == "/" && acessoSite.Texto.Contains("admin.teste"), "Login do administrador no site");
        foreach (var caminho in new[] { "/Clientes", "/Clientes/Novo", "/Roteiros", "/Usuarios", "/Grupos", "/Grupos/Novo" })
        {
            using var page = await site.GetAsync(caminho);
            Verificar(page.RequestMessage!.RequestUri!.AbsolutePath == caminho && (await page.Content.ReadAsStringAsync()).Contains("</html>"), $"Página protegida {caminho}");
        }
        var criadoPeloSite = await Formulario(site, "/Grupos/Novo", new() { ["Nome"] = "Farinhas", ["Ativo"] = "true", ["Versao"] = "0" });
        Verificar(criadoPeloSite.Caminho == "/Grupos" && criadoPeloSite.Texto.Contains("Farinhas"), "Cadastro de grupo pelo navegador retorna à lista");
        var duplicadoPeloSite = await Formulario(site, "/Grupos/Novo", new() { ["Nome"] = "farinhas", ["Ativo"] = "true", ["Versao"] = "0" });
        Verificar(duplicadoPeloSite.Caminho == "/Grupos/Novo" && WebUtility.HtmlDecode(duplicadoPeloSite.Texto).Contains("Já existe um grupo"), "Formulário apresenta erro de grupo duplicado");
        var vazioPeloSite = await Formulario(site, "/Grupos/Novo", new() { ["Nome"] = "", ["Ativo"] = "true", ["Versao"] = "0" });
        Verificar(vazioPeloSite.Caminho == "/Grupos/Novo" && vazioPeloSite.Texto.Contains("Informe o nome do grupo"), "Formulário apresenta validação de nome obrigatório");
        var farinhas = (await admin.GetFromJsonAsync<List<GrupoProdutoDto>>("/api/grupos-produto"))!.Single(g => g.Nome == "Farinhas");
        await Formulario(site, $"/Grupos/Editar/{farinhas.Id}", new() { ["Nome"] = "Farinhas e bases", ["Ativo"] = "false", ["Versao"] = farinhas.Versao.ToString() });
        var editado = (await admin.GetFromJsonAsync<GrupoProdutoDto>($"/api/grupos-produto/{farinhas.Id}"))!;
        Verificar(!editado.Ativo && editado.Nome == "Farinhas e bases", "Edição e inativação pelo site são persistidas");
        await Formulario(site, $"/Grupos/Editar/{farinhas.Id}", new() { ["Nome"] = editado.Nome, ["Ativo"] = "true", ["Versao"] = editado.Versao.ToString() });
        Verificar((await admin.GetFromJsonAsync<GrupoProdutoDto>($"/api/grupos-produto/{farinhas.Id}"))!.Ativo, "Grupo pode ser reativado pelo site");
        var conflitoNoSite = await Formulario(site, $"/Grupos/Editar/{farinhas.Id}", new() { ["Nome"] = "Edição antiga", ["Ativo"] = "false", ["Versao"] = "1" });
        Verificar(conflitoNoSite.Caminho.Contains("/Editar/") && WebUtility.HtmlDecode(conflitoNoSite.Texto).Contains("Recarregue o cadastro"), "Conflito de edição é explicado sem perder o formulário");
        using (var scope = api.Services.CreateScope())
        {
            var contexto = scope.ServiceProvider.GetRequiredService<DistribuidoraDbContext>();
            await AtualizacaoGrupos.Aplicar(contexto);
            await AtualizacaoGrupos.Aplicar(contexto);
        }
        Verificar((await admin.GetFromJsonAsync<List<GrupoProdutoDto>>("/api/grupos-produto"))!.Count == 3, "Repetir atualização do banco preserva os grupos cadastrados");
        await Formulario(site, "/Clientes/Novo", new() { ["Entrada.Nome"] = "Cliente cadastrado com login", ["Entrada.Endereco"] = "Rua de teste", ["Entrada.CidadeId"] = cidade.Id.ToString(), ["Entrada.TerritorioId"] = outroTerritorio.Id.ToString() });
        Verificar((await admin.GetFromJsonAsync<List<ClienteDto>>("/api/clientes"))!.Any(c => c.Nome == "Cliente cadastrado com login"), "Cadastro pelo site utiliza a sessão autenticada da API");
        await Formulario(site, "/Usuarios", new() { ["Entrada.Login"] = "ana.teste", ["Entrada.Senha"] = Convert.ToHexString(RandomNumberGenerator.GetBytes(20)), ["Entrada.Perfil"] = "2", ["Entrada.VendedorId"] = ana.Id.ToString() });
        Verificar((await admin.GetFromJsonAsync<List<UsuarioDto>>("/api/auth/usuarios"))!.Any(x => x.Login == "ana.teste"), "Administrador cria acesso pelo site");
        using var visitante = siteFactory.CreateClient();
        var vendedorNoSite = await Formulario(visitante, "/Conta/Entrar", new() { ["Login"] = "vendedor.teste", ["Senha"] = senhaVendedor });
        Verificar(vendedorNoSite.Caminho == "/Conta/Entrar" && vendedorNoSite.Texto.Contains("aplicativo"), "Site recusa perfil de vendedor");
        Verificar(!vendedorNoSite.Texto.Contains(senhaVendedor), "Senha não reaparece no HTML do formulário");
        using (var semToken = await site.PostAsync("/Conta/Sair", new FormUrlEncodedContent([]))) Verificar(semToken.StatusCode == HttpStatusCode.BadRequest, "Logout exige token do formulário");
        await Formulario(site, "/Conta/Sair", new(), "/");
        using (var depois = await site.GetAsync("/Clientes")) Verificar(depois.RequestMessage!.RequestUri!.AbsolutePath == "/Conta/Entrar", "Logout encerra acesso ao site");
        await Formulario(site, "/Conta/Entrar", new() { ["Login"] = "admin.teste", ["Senha"] = senhaAdmin });
        using (var cmd = banco.CreateCommand()) { cmd.CommandText = "UPDATE SessoesAcesso SET Ativa=0 WHERE UsuarioId=(SELECT Id FROM Usuarios WHERE LoginNormalizado='ADMIN.TESTE')"; cmd.ExecuteNonQuery(); }
        using (var revogada = await site.GetAsync("/Clientes")) Verificar(revogada.RequestMessage!.RequestUri!.AbsolutePath == "/Conta/Entrar", "Revogação na API encerra acesso do site");
        using (var cmd = banco.CreateCommand()) { cmd.CommandText = "PRAGMA integrity_check"; Verificar((string)cmd.ExecuteScalar()! == "ok", "Banco íntegro após os testes"); }
        using (var cmd = banco.CreateCommand()) { cmd.CommandText = "PRAGMA foreign_key_check"; using var rows = cmd.ExecuteReader(); Verificar(!rows.Read(), "Relacionamentos válidos após os testes"); }
        var limitado = false;
        for (var i = 0; i < 16 && !limitado; i++)
        {
            using var resposta = await anonimo.PostAsJsonAsync("/api/auth/entrar", new { login = "nao.existe", senha = "senha-incorreta-123" });
            limitado = resposta.StatusCode == HttpStatusCode.TooManyRequests;
        }
        Verificar(limitado, "Limite de tentativas protege o login");
        await VerificarComercial(raiz, Path.Combine(pasta, "comercial"));
        await VerificarPerfis(raiz, Path.Combine(pasta, "perfis"));
        Console.WriteLine($"{quantidade} verificações integradas concluídas, sem sockets ou credenciais fixas.");
    }
}


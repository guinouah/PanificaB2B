using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PanificaB2B.Api.Dados;
using PanificaB2B.Contratos;

namespace PanificaB2B.Integracao;
public static partial class Program
{
    static async Task VerificarPerfis(string raiz, string pasta)
    {
        Directory.CreateDirectory(pasta);
        using var api = new FabricaApi(raiz, pasta);
        using var anonimo = api.CreateClient();
        var senha = Convert.ToHexString(RandomNumberGenerator.GetBytes(20));
        await Requisicao(anonimo, HttpMethod.Post, "/api/auth/configurar", new { login = "admin.perfis", senha });
        using var admin = await Entrar(api, "admin.perfis", senha);
        var vendedor = (await admin.GetFromJsonAsync<List<VendedorDto>>("/api/vendedores"))!.First();
        await Requisicao(admin, HttpMethod.Post, "/api/auth/usuarios", new NovoUsuarioEntrada { Login = "senha.curta", Senha = "1234567890", Perfil = PerfilUsuario.Financeiro }, HttpStatusCode.BadRequest);
        Verificar(true, "Novos acessos exigem pelo menos 12 caracteres na senha");
        var acessos = new Dictionary<PerfilUsuario, HttpClient> { [PerfilUsuario.Administrador] = admin };
        foreach (var perfil in new[] { PerfilUsuario.Vendedor, PerfilUsuario.GestorComercial, PerfilUsuario.Financeiro, PerfilUsuario.EstoqueExpedicao })
        {
            await Requisicao(admin, HttpMethod.Post, "/api/auth/usuarios", new NovoUsuarioEntrada { Login = "perfil." + (int)perfil, Senha = senha, Perfil = perfil, VendedorId = perfil == PerfilUsuario.Vendedor ? vendedor.Id : null });
            acessos[perfil] = await Entrar(api, "perfil." + (int)perfil, senha);
            var eu = await acessos[perfil].GetFromJsonAsync<UsuarioDto>("/api/auth/eu");
            Verificar(eu!.Perfil == perfil, "Login reconhece perfil " + perfil);
            if (perfil != PerfilUsuario.Vendedor)
                await Requisicao(admin, HttpMethod.Post, "/api/auth/usuarios", new NovoUsuarioEntrada { Login = "vinculo." + (int)perfil, Senha = senha, Perfil = perfil, VendedorId = vendedor.Id }, HttpStatusCode.BadRequest);
        }
        await Requisicao(admin, HttpMethod.Post, "/api/auth/usuarios", new NovoUsuarioEntrada { Login = "perfil.invalido", Senha = senha, Perfil = (PerfilUsuario)99 }, HttpStatusCode.BadRequest);
        Verificar(true, "Perfis de equipe não recebem vínculo comercial e perfis desconhecidos são recusados");
        var gestor = acessos[PerfilUsuario.GestorComercial]; var financeiro = acessos[PerfilUsuario.Financeiro]; var estoque = acessos[PerfilUsuario.EstoqueExpedicao];
        foreach (var perfil in acessos.Keys.Where(x => x != PerfilUsuario.Administrador))
        {
            var usuario = acessos[perfil];
            await Requisicao(usuario, HttpMethod.Get, "/api/auth/usuarios", esperado: HttpStatusCode.Forbidden);
            await Requisicao(usuario, HttpMethod.Post, "/api/auth/usuarios", new { login = "escalar.perfil", senha, perfil = 1 }, HttpStatusCode.Forbidden);
            Verificar(true, perfil + " não cria nem consulta acessos administrativos");
        }
        foreach (var rota in new[] { "/api/vendedores", "/api/territorios", "/api/roteiros", "/api/pedidos", "/api/estoque/lotes", "/api/vendas", "/api/painel" }) await Requisicao(gestor, HttpMethod.Get, rota);
        foreach (var rota in new[] { "/api/titulos", "/api/recebimentos" }) await Requisicao(gestor, HttpMethod.Get, rota, esperado: HttpStatusCode.Forbidden);
        foreach (var rota in new[] { "/api/estoque/lotes", "/api/pedidos", "/api/roteiros", "/api/vendedores" }) await Requisicao(financeiro, HttpMethod.Get, rota, esperado: HttpStatusCode.Forbidden);
        foreach (var rota in new[] { "/api/titulos", "/api/recebimentos", "/api/clientes", "/api/vendas", "/api/painel" }) await Requisicao(financeiro, HttpMethod.Get, rota);
        foreach (var rota in new[] { "/api/titulos", "/api/recebimentos", "/api/pedidos", "/api/roteiros" }) await Requisicao(estoque, HttpMethod.Get, rota, esperado: HttpStatusCode.Forbidden);
        foreach (var rota in new[] { "/api/estoque/lotes", "/api/estoque/movimentos", "/api/produtos", "/api/vendas", "/api/painel" }) await Requisicao(estoque, HttpMethod.Get, rota);
        Verificar(true, "Cada departamento consulta somente os módulos necessários à sua função");
        var cliente = (await admin.GetFromJsonAsync<List<ClienteDto>>("/api/clientes"))!.First(x => x.Ativo);
        await Requisicao(estoque, HttpMethod.Get, $"/api/clientes/{cliente.Id}/divida", esperado: HttpStatusCode.Forbidden);
        await Requisicao(financeiro, HttpMethod.Get, $"/api/clientes/{cliente.Id}/divida");
        var grupo = Ler<GrupoProdutoDto>(await Requisicao(gestor, HttpMethod.Post, "/api/grupos-produto", new { nome = "Grupo do comercial" }, HttpStatusCode.Created));
        var produto = await Post<ProdutoDto>(gestor, "/api/produtos", new ProdutoEntrada { Codigo = "PERFIS", Nome = "Produto do comercial", GrupoId = grupo.Id, Preco = 10 });
        var forma = await Post<FormaPagamentoDto>(financeiro, "/api/formas-pagamento", new { nome = "Pix" });
        var condicao = await Post<CondicaoPagamentoDto>(financeiro, "/api/condicoes-pagamento", new CondicaoPagamentoEntrada { Codigo = "VISTA", Nome = "À vista", Parcelas = [new() { Dias = 0, Percentual = 100, FormaPagamentoId = forma.Id }] });
        var entrada = new EntradaEstoque { ProdutoId = produto.Id, CodigoLote = "PERFIS", Validade = DateOnly.FromDateTime(DateTime.Today.AddDays(10)), Quantidade = 3 };
        await Requisicao(gestor, HttpMethod.Post, "/api/estoque/entradas", entrada, HttpStatusCode.Forbidden);
        await Requisicao(financeiro, HttpMethod.Post, "/api/estoque/entradas", entrada, HttpStatusCode.Forbidden);
        await Requisicao(estoque, HttpMethod.Post, "/api/estoque/entradas", entrada);
        await Requisicao(gestor, HttpMethod.Post, "/api/formas-pagamento", new { nome = "Indevida" }, HttpStatusCode.Forbidden);
        await Requisicao(estoque, HttpMethod.Post, "/api/produtos", new { }, HttpStatusCode.Forbidden);
        await Requisicao(financeiro, HttpMethod.Post, "/api/clientes", new { }, HttpStatusCode.Forbidden);
        Verificar(true, "Escritas cruzadas entre Comercial, Financeiro e Estoque são bloqueadas na API");
        var territorio = (await gestor.GetFromJsonAsync<List<TerritorioDto>>("/api/territorios"))!.Single(x => x.Id == cliente.TerritorioId);
        var pedido = await Post<PedidoDto>(gestor, "/api/pedidos", new PedidoEntrada { ClienteId = cliente.Id, VendedorId = territorio.VendedorId, CondicaoPagamentoId = condicao.Id, CondicaoVersao = condicao.Versao, Itens = [new() { ProdutoId = produto.Id, Quantidade = 5, PrecoUnitario = 10, ProdutoVersao = produto.Versao }] });
        var corte = new FaturamentoEntrada { Versao = pedido.Versao, Motivo = "Estoque disponível", Itens = [new() { ItemId = pedido.Itens[0].Id, Quantidade = 3 }] };
        foreach (var pessoa in new[] { financeiro, estoque, acessos[PerfilUsuario.Vendedor] }) await Requisicao(pessoa, HttpMethod.Post, $"/api/pedidos/{pedido.Id}/faturar", corte, HttpStatusCode.Forbidden);
        var venda = (await Post<FaturamentoDto>(gestor, $"/api/pedidos/{pedido.Id}/faturar", corte)).Venda!;
        Verificar(venda.Total == 30, "Comercial confirma corte sem permissão para lançar entrada ou receber título");
        var titulo = (await financeiro.GetFromJsonAsync<List<TituloDto>>("/api/titulos"))!.Single(x => x.VendaId == venda.Id);
        var recebimento = new RecebimentoEntrada { Versao = titulo.Versao, Data = DateOnly.FromDateTime(DateTime.Today), FormaPagamentoId = forma.Id, Valor = 30 };
        await Requisicao(gestor, HttpMethod.Post, $"/api/titulos/{titulo.Id}/receber", recebimento, HttpStatusCode.Forbidden);
        await Requisicao(estoque, HttpMethod.Post, $"/api/titulos/{titulo.Id}/receber", recebimento, HttpStatusCode.Forbidden);
        await Requisicao(financeiro, HttpMethod.Post, $"/api/titulos/{titulo.Id}/receber", recebimento);
        Verificar((await financeiro.GetFromJsonAsync<List<TituloDto>>("/api/titulos"))!.Single(x => x.Id == titulo.Id).Pago, "Financeiro liquida a parcela gerada pelo Comercial");
        await Requisicao(gestor, HttpMethod.Post, $"/api/vendas/{venda.Id}/entregar", new ConfirmacaoEntrada { Versao = venda.Versao }, HttpStatusCode.Forbidden);
        await Requisicao(financeiro, HttpMethod.Post, $"/api/vendas/{venda.Id}/entregar", new ConfirmacaoEntrada { Versao = venda.Versao }, HttpStatusCode.Forbidden);
        var entregue = await Post<VendaDto>(estoque, $"/api/vendas/{venda.Id}/entregar", new ConfirmacaoEntrada { Versao = venda.Versao });
        Verificar(entregue.Entregue, "Estoque/Expedição confirma a entrega da venda");
        var painelEstoque = (await estoque.GetFromJsonAsync<PainelDto>("/api/painel"))!;
        Verificar(painelEstoque.Vendas == 0 && painelEstoque.ContasAReceber == 0, "Painel de Estoque não recebe totais financeiros escondidos apenas na tela");
        using var siteFactory = new FabricaSite(raiz, pasta, api);
        foreach (var (perfil, permitidos, negados) in new[] {
            (PerfilUsuario.GestorComercial, new[] { "/Cadastros", "/Clientes", "/Produtos", "/Roteiros", "/Pedidos", "/Vendas" }, new[] { "/Usuarios", "/Pagamentos", "/Financeiro", "/Estoque" }),
            (PerfilUsuario.Financeiro, new[] { "/Clientes", "/Pagamentos", "/Financeiro", "/Vendas" }, new[] { "/Usuarios", "/Cadastros", "/Produtos", "/Pedidos", "/Estoque", "/Clientes/Novo" }),
            (PerfilUsuario.EstoqueExpedicao, new[] { "/Estoque", "/Vendas" }, new[] { "/Usuarios", "/Clientes", "/Cadastros", "/Produtos", "/Pedidos", "/Financeiro", "/Pagamentos" }) })
        {
            using var site = siteFactory.CreateClient();
            var login = await Formulario(site, "/Conta/Entrar", new() { ["Login"] = "perfil." + (int)perfil, ["Senha"] = senha });
            Verificar(login.Caminho == "/", "Perfil " + perfil + " entra no site");
            foreach (var rota in permitidos) { using var page = await site.GetAsync(rota); Verificar(page.StatusCode == HttpStatusCode.OK && page.RequestMessage!.RequestUri!.AbsolutePath == rota, perfil + " abre " + rota); }
            foreach (var rota in negados) { using var page = await site.GetAsync(rota); Verificar(page.StatusCode == HttpStatusCode.Forbidden, perfil + " não abre " + rota + " mesmo digitando a URL"); }
            Verificar(!login.Texto.Contains("href=\"/Usuarios\""), "Menu " + perfil + " omite gestão de acessos");
        }
        foreach (var pair in acessos.Where(x => x.Key != PerfilUsuario.Administrador)) pair.Value.Dispose();
    }

    static async Task VerificarMigracaoPerfis(string raiz, string pasta, string senha)
    {
        var legado = Path.Combine(pasta, "v1"); Directory.CreateDirectory(legado);
        var alvo = Path.Combine(legado, "banco.db");
        using (var origem = new SqliteConnection($"Data Source={Path.Combine(pasta, "banco.db")};Mode=ReadOnly"))
        using (var copia = new SqliteConnection($"Data Source={alvo}"))
        {
            origem.Open(); copia.Open(); origem.BackupDatabase(copia);
            using var cmd = copia.CreateCommand();
            // Fixture only: restore the known v1 CHECK in a disposable copy.
            cmd.CommandText = """
                PRAGMA foreign_keys=OFF;
                CREATE TABLE Usuarios_Legado (Id TEXT NOT NULL PRIMARY KEY, Login TEXT NOT NULL, LoginNormalizado TEXT NOT NULL, SenhaHash TEXT NOT NULL, Perfil INTEGER NOT NULL, VendedorId TEXT NULL REFERENCES Vendedores(Id) ON DELETE RESTRICT, Ativo INTEGER NOT NULL, CONSTRAINT CK_Usuario_Perfil CHECK ((Perfil = 1 AND VendedorId IS NULL) OR (Perfil = 2 AND VendedorId IS NOT NULL)));
                INSERT INTO Usuarios_Legado SELECT Id,Login,LoginNormalizado,SenhaHash,Perfil,VendedorId,Ativo FROM Usuarios;
                DROP TABLE Usuarios;
                ALTER TABLE Usuarios_Legado RENAME TO Usuarios;
                CREATE UNIQUE INDEX IX_Usuarios_LoginNormalizado ON Usuarios(LoginNormalizado);
                CREATE UNIQUE INDEX IX_Usuarios_VendedorId ON Usuarios(VendedorId) WHERE VendedorId IS NOT NULL;
                PRAGMA foreign_keys=ON;
                """;
            cmd.ExecuteNonQuery();
        }
        using var banco = new SqliteConnection($"Data Source={alvo};Foreign Keys=True"); banco.Open();
        string Linha(string sql) { using var cmd = banco.CreateCommand(); cmd.CommandText = sql; return Convert.ToString(cmd.ExecuteScalar())!; }
        var hash = Linha("SELECT SenhaHash FROM Usuarios WHERE Login='admin.comercial'");
        var totais = new Dictionary<string,string>();
        foreach (var tabela in new[] { "Usuarios", "SessoesAcesso", "Clientes", "Pedidos", "Vendas", "Titulos", "Recebimentos", "Movimentos" }) totais[tabela] = Linha($"SELECT COUNT(*) FROM {tabela}");
        using var atualizada = new FabricaApi(raiz, legado);
        using var cliente = atualizada.CreateClient();
        foreach (var (tabela, total) in totais) Verificar(Linha($"SELECT COUNT(*) FROM {tabela}") == total, "Migração de perfis preserva " + tabela);
        Verificar(Linha("SELECT SenhaHash FROM Usuarios WHERE Login='admin.comercial'") == hash && Directory.GetFiles(Path.Combine(legado, "Backups"), "antes-perfis-*.db").Length == 1, "Migração cria backup e preserva o hash da senha existente");
        using var admin = await Entrar(atualizada, "admin.comercial", senha);
        await Requisicao(admin, HttpMethod.Post, "/api/auth/usuarios", new NovoUsuarioEntrada { Login = "apos.migracao", Senha = senha, Perfil = PerfilUsuario.Financeiro });
        Verificar(true, "Administrador mantém acesso e cadastra perfil novo após atualização da base antiga");
        using (var scope = atualizada.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DistribuidoraDbContext>();
            await AtualizacaoPerfis.Aplicar(db); await AtualizacaoPerfis.Aplicar(db);
        }
        using (var cmd = banco.CreateCommand()) { cmd.CommandText = "PRAGMA foreign_key_check"; using var rows = cmd.ExecuteReader(); Verificar(!rows.Read(), "Migração dos perfis mantém vínculos de sessões, vendas e movimentos"); }
    }
}

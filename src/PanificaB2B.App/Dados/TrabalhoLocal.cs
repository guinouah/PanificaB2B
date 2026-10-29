using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using PanificaB2B.Contratos;
using SQLite;

namespace PanificaB2B.App.Dados;

[Table("OperacoesPendentes")]
public class OperacaoLocal
{
    [PrimaryKey] public string Chave { get; set; } = "";
    [Indexed] public string VendedorId { get; set; } = "";
    public string RegistroId { get; set; } = "";
    public string Tipo { get; set; } = "";
    public string Situacao { get; set; } = "Rascunho";
    public string ConteudoJson { get; set; } = "";
    public string Mensagem { get; set; } = "";
    public string AtualizadoEm { get; set; } = "";
}

public sealed class TrabalhoLocal(string caminhoBanco)
{
    readonly SQLiteAsyncConnection banco = new(caminhoBanco);
    static readonly JsonSerializerOptions json = new(JsonSerializerDefaults.Web);
    public static T Ler<T>(OperacaoLocal item) => JsonSerializer.Deserialize<T>(item.ConteudoJson, json) ?? throw new InvalidDataException("Pendência local inválida.");
    public async Task<List<OperacaoLocal>> Listar(Guid vendedorId)
    {
        await banco.CreateTableAsync<OperacaoLocal>();
        var id = vendedorId.ToString();
        return await banco.Table<OperacaoLocal>().Where(x => x.VendedorId == id).OrderBy(x => x.AtualizadoEm).ToListAsync();
    }
    public Task SalvarCliente(Guid vendedorId, ClienteEntrada cliente) => Salvar(vendedorId, cliente.Id, "Cliente", cliente, "Pronto");
    public Task SalvarPedido(Guid vendedorId, PedidoEntrada pedido, bool pronto) => Salvar(vendedorId, pedido.Id, "Pedido", pedido, pronto ? "Pronto" : "Rascunho");
    async Task Salvar(Guid vendedorId, Guid id, string tipo, object conteudo, string situacao)
    {
        if (id == Guid.Empty || vendedorId == Guid.Empty) throw new InvalidOperationException("Identificação local inválida.");
        await banco.CreateTableAsync<OperacaoLocal>();
        var chave = $"{tipo}:{id}";
        var existente = await banco.FindAsync<OperacaoLocal>(chave);
        if (existente is not null && (existente.VendedorId != vendedorId.ToString() || existente.Situacao is not ("Rascunho" or "Rejeitado" or "Pronto")))
            throw new InvalidOperationException("Esta operação já foi enviada ou aguarda confirmação. Tente o envio antes de alterá-la.");
        await banco.InsertOrReplaceAsync(new OperacaoLocal { Chave = chave, VendedorId = vendedorId.ToString(), RegistroId = id.ToString(), Tipo = tipo, Situacao = situacao, ConteudoJson = JsonSerializer.Serialize(conteudo, json), AtualizadoEm = DateTimeOffset.UtcNow.ToString("O") });
    }
    public async Task Atualizar(OperacaoLocal item, string situacao, string mensagem)
    {
        item.Situacao = situacao; item.Mensagem = mensagem; item.AtualizadoEm = DateTimeOffset.UtcNow.ToString("O");
        await banco.UpdateAsync(item);
    }
    public static List<ClienteDto> Clientes(PreparacaoDto dados, IEnumerable<OperacaoLocal> locais)
    {
        var clientes = dados.Clientes.ToDictionary(x => x.Id);
        foreach (var item in locais.Where(x => x.Tipo == "Cliente"))
        {
            if (item.Situacao == "Confirmado" && DateTimeOffset.TryParse(item.AtualizadoEm, out var confirmadoEm) && confirmadoEm < dados.AtualizadoEm) continue;
            var c = Ler<ClienteEntrada>(item);
            if (!clientes.ContainsKey(c.Id)) clientes[c.Id] = new(c.Id, c.Nome, c.Documento ?? "", c.Email ?? "", c.Telefone ?? "", c.Endereco, c.CidadeId, c.TerritorioId, c.Ativo, 0);
        }
        return clientes.Values.OrderBy(x => x.Nome).ToList();
    }
}

public sealed class EnvioPendencias(TrabalhoLocal local, HttpClient api)
{
    static readonly JsonSerializerOptions json = new(JsonSerializerDefaults.Web);
    public async Task<string> Enviar(Guid vendedorId, Uri raiz, string token, CancellationToken cancellationToken = default)
    {
        var todos = await local.Listar(vendedorId);
        var candidatos = todos.Where(x => x.Situacao is "Pronto" or "Pendente" or "Enviando").OrderBy(x => x.Tipo == "Cliente" ? 0 : 1).ThenBy(x => x.AtualizadoEm).ToList();
        var confirmados = 0; var rejeitados = 0;
        foreach (var item in candidatos)
        {
            if (item.Tipo == "Pedido")
            {
                var pedido = TrabalhoLocal.Ler<PedidoEntrada>(item);
                var cadastro = todos.FirstOrDefault(x => x.Tipo == "Cliente" && x.RegistroId == pedido.ClienteId.ToString());
                if (cadastro is not null && cadastro.Situacao != "Confirmado") { await local.Atualizar(item, "Pendente", "Aguardando confirmação do cadastro do cliente."); continue; }
            }
            await local.Atualizar(item, "Enviando", "Aguardando confirmação da API.");
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(raiz, item.Tipo == "Cliente" ? "api/clientes" : "api/pedidos"));
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Content = new StringContent(item.ConteudoJson, Encoding.UTF8, "application/json");
                using var resposta = await api.SendAsync(request, cancellationToken);
                var texto = await resposta.Content.ReadAsStringAsync(cancellationToken);
                if (resposta.IsSuccessStatusCode)
                {
                    var body = JsonSerializer.Deserialize<JsonElement>(texto, json);
                    if (!body.TryGetProperty("id", out var id) || id.GetGuid().ToString() != item.RegistroId) throw new InvalidDataException("Confirmação inválida.");
                    await local.Atualizar(item, "Confirmado", "Recebido pela distribuidora."); confirmados++;
                }
                else if (resposta.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                {
                    await local.Atualizar(item, "Pendente", "Acesso expirou ou foi alterado. Entre novamente."); break;
                }
                else if ((int)resposta.StatusCode >= 500 || resposta.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    await local.Atualizar(item, "Pendente", "Servidor indisponível. Tente novamente mais tarde."); break;
                }
                else
                {
                    var mensagem = "Pedido/cadastro recusado. Revise os dados e atualize o catálogo.";
                    try
                    {
                        var body = JsonDocument.Parse(texto).RootElement;
                        if (body.TryGetProperty("mensagem", out var m)) mensagem = m.GetString() ?? mensagem;
                        else if (body.TryGetProperty("errors", out var erros)) mensagem = string.Join(" ", erros.EnumerateObject().SelectMany(x => x.Value.EnumerateArray()).Select(x => x.GetString()));
                    }
                    catch (JsonException) { }
                    var conflitoId = mensagem.StartsWith("Identificador", StringComparison.OrdinalIgnoreCase);
                    await local.Atualizar(item, conflitoId ? "Conflito" : "Rejeitado", mensagem); rejeitados++;
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or InvalidDataException or JsonException)
            {
                await local.Atualizar(item, "Pendente", "Sem confirmação da API. O mesmo identificador será reutilizado na próxima tentativa."); break;
            }
        }
        return $"Confirmados: {confirmados}. Para revisar: {rejeitados}. As demais pendências foram mantidas.";
    }
}

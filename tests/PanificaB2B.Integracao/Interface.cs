using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using PanificaB2B.Contratos;

namespace PanificaB2B.Integracao;

public static partial class Program
{
    static async Task VerificarInterface(string raiz, string pasta)
    {
        Directory.CreateDirectory(pasta);
        using var api = new FabricaApi(raiz, pasta);
        using var visitante = api.CreateClient();
        var senha = Convert.ToHexString(RandomNumberGenerator.GetBytes(20));
        await Requisicao(visitante, HttpMethod.Post, "/api/auth/configurar", new { login = "admin.interface", senha });
        using var admin = await Entrar(api, "admin.interface", senha);
        using var fabricaSite = new FabricaSite(raiz, pasta, api);
        using var site = fabricaSite.CreateClient();
        var loginHtml = await site.GetStringAsync("/Conta/Entrar");
        Verificar(loginHtml.Contains("auth-shell") && loginHtml.Contains("autocomplete=\"current-password\""), "Tela de acesso renovada mantém formulário de autenticação");
        await Formulario(site, "/Conta/Entrar", new() { ["Login"]="admin.interface", ["Senha"]=senha });
        var home = await site.GetStringAsync("/");
        Verificar(home.Contains("sidebar") && home.Contains("aria-current=\"page\""), "Painel identifica a página ativa e mantém navegação administrativa");

        var territorio = (await admin.GetFromJsonAsync<List<TerritorioDto>>("/api/territorios"))!.First(x=>x.Ativo);
        var cidade = (await admin.GetFromJsonAsync<List<CidadeDto>>("/api/cidades"))!.First(x=>territorio.CidadeIds.Contains(x.Id));
        var nomes = new[] { "Padaria Aurora", "Confeitaria Doce Trigo", "Mercado Pão da Vila" };
        var clientes = new List<ClienteDto>();
        foreach (var nome in nomes)
            clientes.Add(await Post<ClienteDto>(admin, "/api/clientes", new { nome, endereco="Rua de demonstração, 100", cidadeId=cidade.Id, territorioId=territorio.Id }));
        var grupo = Ler<GrupoProdutoDto>(await Requisicao(admin, HttpMethod.Post, "/api/grupos-produto", new { nome="Panificação" }, HttpStatusCode.Created));
        var produto = await Post<ProdutoDto>(admin, "/api/produtos", new ProdutoEntrada { Codigo="UI-FER", Nome="Fermento biológico", GrupoId=grupo.Id, Unidade="UN", Preco=10 });
        var forma = await Post<FormaPagamentoDto>(admin, "/api/formas-pagamento", new { nome="Pix" });
        var condicao = await Post<CondicaoPagamentoDto>(admin, "/api/condicoes-pagamento", new CondicaoPagamentoEntrada { Codigo="UI7-14", Nome="7 e 14 dias", Parcelas=[new() { Dias=7, Percentual=50, FormaPagamentoId=forma.Id }, new() { Dias=14, Percentual=50, FormaPagamentoId=forma.Id }] });
        await Post<LoteDto>(admin, "/api/estoque/entradas", new EntradaEstoque { ProdutoId=produto.Id, CodigoLote="UI-LOTE", Quantidade=15, Validade=DateOnly.FromDateTime(DateTime.Today.AddDays(30)) });
        var pedidos = new List<PedidoDto>();
        var quantidades = new[] { 5m, 8m, 9m };
        for (var i=0; i<clientes.Count; i++)
            pedidos.Add(await Post<PedidoDto>(admin, "/api/pedidos", new PedidoEntrada { ClienteId=clientes[i].Id, VendedorId=territorio.VendedorId, CondicaoPagamentoId=condicao.Id, CondicaoVersao=condicao.Versao, Itens=[new() { ProdutoId=produto.Id, ProdutoVersao=produto.Versao, Quantidade=quantidades[i], PrecoUnitario=10 }] }));
        var lista = WebUtility.HtmlDecode(await site.GetStringAsync("/Pedidos"));
        Verificar(nomes.All(lista.Contains) && lista.Contains(">22</b>") && lista.Contains(">15</b>") && lista.Contains(">7</b>"), "Demanda por produto mostra três clientes, 22 solicitados, 15 disponíveis e falta de 7");
        var detalhe = await site.GetStringAsync($"/Pedidos/Detalhe/{pedidos[0].Id}");
        Verificar(detalhe.Contains("data-cut-form") && detalhe.Contains("data-requested=\"5\"") && detalhe.Contains("data-stock=\"15\"") && !detalhe.Contains(".ToString("), "Formulário de corte expõe quantidades e resumo sem expressão Razor literal");
        var clientesHtml = await site.GetStringAsync("/Clientes");
        Verificar(clientesHtml.Contains("data-table-search=\"clientes\"") && nomes.All(x=>WebUtility.HtmlDecode(clientesHtml).Contains(x)), "Carteira apresenta busca e clientes sem alterar seus vínculos");

        var previa = Environment.GetEnvironmentVariable("PANIFICA_PREVIEW_DIR");
        if (!string.IsNullOrWhiteSpace(previa))
        {
            Directory.CreateDirectory(previa);
            foreach (var (arquivo,conteudo) in new[] { ("login.html",loginHtml), ("painel.html",await site.GetStringAsync("/")), ("pedidos.html",lista), ("corte.html",detalhe), ("clientes.html",clientesHtml) })
            {
                var html = Regex.Replace(conteudo, "(href|src)=\"/(css|js|lib)/([^\"]*)\"", "$1=\"assets/$2/$3\"");
                await File.WriteAllTextAsync(Path.Combine(previa,arquivo),html);
            }
        }
        Dictionary<string,string> Dados(PedidoDto pedido,string quantidade,string motivo) => new()
        {
            ["Corte.Versao"]=pedido.Versao.ToString(), ["Corte.Itens[0].ItemId"]=pedido.Itens[0].Id.ToString(),
            ["Corte.Itens[0].Quantidade"]=quantidade, ["Corte.Motivo"]=motivo
        };
        var primeiro = pedidos[0];
        var rota = $"/Pedidos/Faturar/{primeiro.Id}";
        var origem = $"/Pedidos/Detalhe/{primeiro.Id}";
        var excesso = await Formulario(site,rota,Dados(primeiro,"6","Estoque disponível"),origem);
        Verificar(WebUtility.HtmlDecode(excesso.Texto).Contains("não pode superar"), "Formulário recusa atendimento acima da quantidade solicitada");
        var semMotivo = await Formulario(site,rota,Dados(primeiro,"2",""),origem);
        Verificar(WebUtility.HtmlDecode(semMotivo.Texto).Contains("Informe o motivo do corte"), "Formulário exige motivo quando há redução");
        var cortes = new[] { "2", "6", "7" };
        for (var i=0; i<pedidos.Count; i++)
        {
            var resultado = await Formulario(site,$"/Pedidos/Faturar/{pedidos[i].Id}",Dados(pedidos[i],cortes[i],"Distribuição do estoque disponível"),$"/Pedidos/Detalhe/{pedidos[i].Id}");
            Verificar(resultado.Caminho==$"/Pedidos/Detalhe/{pedidos[i].Id}" && WebUtility.HtmlDecode(resultado.Texto).Contains("Resultado do atendimento"), "Corte pelo formulário confirmado para " + nomes[i]);
        }
        var vendas = (await admin.GetFromJsonAsync<List<VendaDto>>("/api/vendas"))!;
        var saldo = (await admin.GetFromJsonAsync<List<LoteDto>>("/api/estoque/lotes"))!.Where(x=>x.ProdutoId==produto.Id).Sum(x=>x.Saldo);
        var titulos = (await admin.GetFromJsonAsync<List<TituloDto>>("/api/titulos"))!;
        Verificar(vendas.Count==3 && vendas.Sum(x=>x.Total)==150 && saldo==0 && titulos.Count==6, "Fluxo visual 2/6/7 gera R$ 150, seis parcelas e estoque zerado");
        var ficha=WebUtility.HtmlDecode(await site.GetStringAsync($"/Clientes/Detalhe/{clientes[0].Id}"));
        Verificar(ficha.Contains("Situação financeira") && ficha.Contains("Fermento biológico") && ficha.Contains("20,00"), "Ficha do cliente reflete dívida e produto efetivamente atendido");
    }
}

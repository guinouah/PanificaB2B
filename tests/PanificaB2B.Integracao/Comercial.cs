using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using PanificaB2B.Api.Dados;
using PanificaB2B.App.Dados;
using PanificaB2B.Contratos;

namespace PanificaB2B.Integracao;

public static partial class Program
{
    static readonly JsonSerializerOptions JsonWeb = new(JsonSerializerDefaults.Web);
    static T Ler<T>(JsonElement json) => json.Deserialize<T>(JsonWeb)!;
    static async Task<T> Post<T>(HttpClient client, string rota, object corpo) => Ler<T>(await Requisicao(client, HttpMethod.Post, rota, corpo));
    static async Task VerificarComercial(string raiz, string pasta)
    {
        Directory.CreateDirectory(pasta);
        using var fabrica = new FabricaApi(raiz, pasta);
        using var anonimo = fabrica.CreateClient();
        var senha = Convert.ToHexString(RandomNumberGenerator.GetBytes(20));
        await Requisicao(anonimo, HttpMethod.Post, "/api/auth/configurar", new { login = "admin.comercial", senha });
        using var admin = await Entrar(fabrica, "admin.comercial", senha);
        using var siteFactory = new FabricaSite(raiz, pasta, fabrica);
        using var site = siteFactory.CreateClient();
        await Formulario(site, "/Conta/Entrar", new() { ["Login"] = "admin.comercial", ["Senha"] = senha });
        using (var css = await anonimo.GetAsync("/api/produtos")) Verificar(css.StatusCode == HttpStatusCode.Unauthorized, "Catálogo exige acesso autenticado");
        using (var visitante = siteFactory.CreateClient())
        {
            using var css = await visitante.GetAsync("/css/site.css");
            Verificar(css.StatusCode == HttpStatusCode.OK && css.Content.Headers.ContentType?.MediaType == "text/css", "Estilo do site carrega antes do login");
        }
        var vendedores = (await admin.GetFromJsonAsync<List<VendedorDto>>("/api/vendedores"))!;
        var vendedor = vendedores.Single(x => x.Nome == "Henrique");
        var outro = vendedores.Single(x => x.Nome == "Ana");
        await Requisicao(admin, HttpMethod.Post, "/api/auth/usuarios", new { login = "venda.comercial", senha, perfil = 2, vendedorId = vendedor.Id });
        await Requisicao(admin, HttpMethod.Post, "/api/auth/usuarios", new { login = "outra.comercial", senha, perfil = 2, vendedorId = outro.Id });
        using var usuario = await Entrar(fabrica, "venda.comercial", senha);
        using var concorrente = await Entrar(fabrica, "outra.comercial", senha);
        var carteira = (await usuario.GetFromJsonAsync<PreparacaoDto>($"/api/vendedores/{vendedor.Id}/preparacao"))!;
        var territorio = carteira.Territorios.First(); var cidade = carteira.Cidades.First();
        async Task<ClienteDto> Cliente(string nome) => await Post<ClienteDto>(admin, "/api/clientes", new { nome, endereco = "Rua de teste", cidadeId = cidade.Id, territorioId = territorio.Id });
        var a = await Cliente("Cliente A"); var b = await Cliente("Cliente B"); var c = await Cliente("Cliente C");
        var grupo = Ler<GrupoProdutoDto>(await Requisicao(admin, HttpMethod.Post, "/api/grupos-produto", new { nome = "Produtos do fluxo" }, HttpStatusCode.Created));
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var produtoEntrada = new ProdutoEntrada { Codigo = "FER-01", Nome = "Fermento", GrupoId = grupo.Id, Preco = 10, Unidade = "UN", NovidadeAte = hoje.AddDays(20) };
        var produto = await Post<ProdutoDto>(admin, "/api/produtos", produtoEntrada);
        Verificar(produto.Preco == 10 && produto.Novidade(hoje), "Produto possui preço, grupo, unidade e novidade");
        await Requisicao(admin, HttpMethod.Post, "/api/produtos", produtoEntrada, HttpStatusCode.BadRequest);
        produtoEntrada.Codigo = "OUTRO"; produtoEntrada.Preco = 10.123m;
        await Requisicao(admin, HttpMethod.Post, "/api/produtos", produtoEntrada, HttpStatusCode.BadRequest);
        Verificar(true, "Código duplicado e preço com precisão inválida são recusados");
        var promo = await Post<ProdutoDto>(admin, "/api/produtos", new ProdutoEntrada { Codigo = "PROMO", Nome = "Mistura", GrupoId = grupo.Id, Preco = 15, PrecoPromocional = 12, PromocaoInicio = hoje, PromocaoFim = hoje.AddDays(1) });
        Verificar(promo.PrecoNaData(hoje) == 12 && promo.PrecoNaData(hoje.AddDays(2)) == 15, "Promoção respeita vigência sem modificar preço histórico");
        var pix = await Post<FormaPagamentoDto>(admin, "/api/formas-pagamento", new { nome = "Pix" });
        var boleto = await Post<FormaPagamentoDto>(admin, "/api/formas-pagamento", new { nome = "Boleto" });
        var condicaoEntrada = new CondicaoPagamentoEntrada { Codigo = "7-14", Nome = "7 e 14 dias", MultaPercentual = 5, JurosDiariosPercentual = 2, DescontoDiarioPercentual = 1,
            Parcelas = [new() { Dias = 7, Percentual = 50, FormaPagamentoId = pix.Id }, new() { Dias = 14, Percentual = 50, FormaPagamentoId = boleto.Id }] };
        var condicao = await Post<CondicaoPagamentoDto>(admin, "/api/condicoes-pagamento", condicaoEntrada);
        condicaoEntrada.Codigo = "ERRADA"; condicaoEntrada.Parcelas[0].Percentual = 49;
        await Requisicao(admin, HttpMethod.Post, "/api/condicoes-pagamento", condicaoEntrada, HttpStatusCode.BadRequest);
        condicaoEntrada.Codigo = "7-14"; condicaoEntrada.Parcelas[0].Percentual = 50;
        Verificar(condicao.Parcelas.Select(x => x.FormaPagamentoId).Distinct().Count() == 2, "Condição permite formas diferentes e exige soma exata de 100%");
        var entrada = new EntradaEstoque { ProdutoId = produto.Id, CodigoLote = "CEDO", Quantidade = 5, Validade = hoje.AddDays(5) };
        var cedo = await Post<LoteDto>(admin, "/api/estoque/entradas", entrada);
        await Requisicao(admin, HttpMethod.Post, "/api/estoque/entradas", entrada);
        var tarde = await Post<LoteDto>(admin, "/api/estoque/entradas", new EntradaEstoque { ProdutoId = produto.Id, CodigoLote = "TARDE", Quantidade = 10, Validade = hoje.AddDays(10) });
        var vencido = await Post<LoteDto>(admin, "/api/estoque/entradas", new EntradaEstoque { ProdutoId = produto.Id, CodigoLote = "VENCIDO", Quantidade = 50, Validade = hoje.AddDays(-1) });
        Verificar((await admin.GetFromJsonAsync<List<LoteDto>>("/api/estoque/lotes"))!.Where(x => x.Validade >= hoje).Sum(x => x.Saldo) == 15, "Entrada repetida não duplica saldo; vencidos ficam separados");
        foreach (var rota in new[] { "/api/estoque/lotes", "/api/estoque/movimentos", "/api/titulos", "/api/recebimentos", "/api/vendas", "/api/painel" }) await Requisicao(usuario, HttpMethod.Get, rota, esperado: HttpStatusCode.Forbidden);
        await Requisicao(usuario, HttpMethod.Post, "/api/produtos", produtoEntrada, HttpStatusCode.Forbidden);
        await Requisicao(usuario, HttpMethod.Post, "/api/estoque/entradas", entrada, HttpStatusCode.Forbidden);
        Verificar(true, "Vendedor não consulta saldo nem gerencia estoque, faturamento ou financeiro");
        PedidoEntrada Novo(ClienteDto cliente, decimal qtd, ProdutoDto? p = null) => new() { ClienteId = cliente.Id, VendedorId = vendedor.Id, CondicaoPagamentoId = condicao.Id, CondicaoVersao = condicao.Versao,
            Itens = [new() { ProdutoId = (p ?? produto).Id, ProdutoVersao = (p ?? produto).Versao, Quantidade = qtd, PrecoUnitario = (p ?? produto).PrecoNaData(hoje) }] };
        var ra = Novo(a, 5); var pa = await Post<PedidoDto>(usuario, "/api/pedidos", ra);
        var pb = await Post<PedidoDto>(usuario, "/api/pedidos", Novo(b, 8));
        var pc = await Post<PedidoDto>(usuario, "/api/pedidos", Novo(c, 9));
        Verificar(pa.TotalPedido == 50 && (await usuario.GetFromJsonAsync<List<PedidoDto>>("/api/pedidos"))!.SelectMany(x => x.Itens).Sum(x => x.QuantidadePedida) == 22, "Recepção reúne demanda de 22 unidades sem exigir estoque na digitação");
        await Requisicao(usuario, HttpMethod.Post, "/api/pedidos", ra);
        ra.Itens[0].Quantidade = 5.000m; ra.Itens[0].PrecoUnitario = 10.00m; ra.CriadoEm = ra.CriadoEm.ToOffset(TimeSpan.FromHours(-3));
        var outraFormatacao = await Post<PedidoDto>(usuario, "/api/pedidos", ra);
        Verificar(outraFormatacao.Id == pa.Id, "Idempotência normaliza escala decimal e fuso do mesmo instante");
        ra.Observacao = "Diferente";
        await Requisicao(usuario, HttpMethod.Post, "/api/pedidos", ra, HttpStatusCode.Conflict);
        ra.Observacao = "";
        Verificar((await admin.GetFromJsonAsync<List<PedidoDto>>("/api/pedidos"))!.Count == 3, "Reenvio confirma o mesmo pedido; conteúdo diferente gera conflito");
        await Requisicao(concorrente, HttpMethod.Get, $"/api/pedidos/{pa.Id}", esperado: HttpStatusCode.NotFound);
        await Requisicao(concorrente, HttpMethod.Get, $"/api/clientes/{a.Id}/divida", esperado: HttpStatusCode.NotFound);
        await Requisicao(concorrente, HttpMethod.Get, $"/api/clientes/{a.Id}/ultimas-compras", esperado: HttpStatusCode.NotFound);
        Verificar((await concorrente.GetFromJsonAsync<List<PedidoDto>>("/api/pedidos"))!.Count == 0, "Pedidos, histórico e dívida respeitam a carteira autenticada");
        FaturamentoEntrada Corte(PedidoDto p, decimal qtd) => new() { Versao = p.Versao, Motivo = "Distribuição de estoque", Itens = p.Itens.Select(i => new CorteItemEntrada { ItemId = i.Id, Quantidade = qtd }).ToList() };
        await Requisicao(usuario, HttpMethod.Post, $"/api/pedidos/{pa.Id}/faturar", Corte(pa, 2), HttpStatusCode.Forbidden);
        await Requisicao(admin, HttpMethod.Post, $"/api/pedidos/{pa.Id}/faturar", Corte(pa, 6), HttpStatusCode.BadRequest);
        var antigo = Corte(pa, 2); antigo.Versao = 0;
        await Requisicao(admin, HttpMethod.Post, $"/api/pedidos/{pa.Id}/faturar", antigo, HttpStatusCode.Conflict);
        Verificar(true, "Corte exige administrador, versão atual e quantidade até a solicitada");
        var semSaldoEntrada = Novo(a, 2); semSaldoEntrada.Itens.Add(new() { ProdutoId = promo.Id, ProdutoVersao = promo.Versao, PrecoUnitario = 12, Quantidade = 1 });
        var semSaldo = await Post<PedidoDto>(usuario, "/api/pedidos", semSaldoEntrada);
        await Requisicao(admin, HttpMethod.Post, $"/api/pedidos/{semSaldo.Id}/faturar", new FaturamentoEntrada { Versao = semSaldo.Versao, Itens = semSaldo.Itens.Select(i => new CorteItemEntrada { ItemId = i.Id, Quantidade = i.QuantidadePedida }).ToList() }, HttpStatusCode.BadRequest);
        Verificar((await admin.GetFromJsonAsync<List<LoteDto>>("/api/estoque/lotes"))!.Where(x => x.Validade >= hoje).Sum(x => x.Saldo) == 15 && (await admin.GetFromJsonAsync<List<VendaDto>>("/api/vendas"))!.Count == 0 && (await admin.GetFromJsonAsync<List<TituloDto>>("/api/titulos"))!.Count == 0,
            "Falha no segundo item desfaz estoque, venda e parcelas da transação inteira");
        var zero = await Post<FaturamentoDto>(admin, $"/api/pedidos/{semSaldo.Id}/faturar", Corte(semSaldo, 0));
        Verificar(zero.Venda is null && zero.Pedido.Situacao == SituacaoPedido.NaoAtendido, "Corte total encerra pedido sem venda nem cobrança");
        var fa = await Post<FaturamentoDto>(admin, $"/api/pedidos/{pa.Id}/faturar", Corte(pa, 2));
        var fb = await Post<FaturamentoDto>(admin, $"/api/pedidos/{pb.Id}/faturar", Corte(pb, 6));
        var fc = await Post<FaturamentoDto>(admin, $"/api/pedidos/{pc.Id}/faturar", Corte(pc, 7));
        Verificar(fa.Venda!.Total == 20 && fb.Venda!.Total == 60 && fc.Venda!.Total == 70 && new[] { fa, fb, fc }.All(x => x.Pedido.Situacao == SituacaoPedido.FaturadoParcialmente), "Exemplo do professor: 5/8/9 pedidos, 2/6/7 atendidos, cobrança só de 15 unidades");
        var lotes = (await admin.GetFromJsonAsync<List<LoteDto>>("/api/estoque/lotes"))!;
        Verificar(lotes.Single(x => x.Id == cedo.Id).Saldo == 0 && lotes.Single(x => x.Id == tarde.Id).Saldo == 0 && lotes.Single(x => x.Id == vencido.Id).Saldo == 50, "Baixa prioriza validade mais próxima e não utiliza lote vencido");
        var repetida = await Post<FaturamentoDto>(admin, $"/api/pedidos/{pa.Id}/faturar", Corte(pa, 2));
        await Requisicao(admin, HttpMethod.Post, $"/api/pedidos/{pa.Id}/faturar", Corte(pa, 1), HttpStatusCode.Conflict);
        Verificar(repetida.Venda!.Id == fa.Venda.Id && (await admin.GetFromJsonAsync<List<VendaDto>>("/api/vendas"))!.Count == 3, "Confirmação repetida não duplica venda, títulos ou baixa");
        var ultimo = (await usuario.GetFromJsonAsync<List<UltimaCompraDto>>($"/api/clientes/{a.Id}/ultimas-compras"))!.Single();
        Verificar(ultimo.Quantidade == 2 && ultimo.PrecoUnitario == 10, "Última compra usa a quantidade faturada, não a solicitada");
        var catalogoBaixado = (await usuario.GetFromJsonAsync<PreparacaoDto>($"/api/vendedores/{vendedor.Id}/preparacao"))!;
        var jsonDownload = JsonSerializer.Serialize(catalogoBaixado, JsonWeb);
        Verificar(catalogoBaixado.Produtos.Count == 2 && catalogoBaixado.CondicoesPagamento.Count == 1 && catalogoBaixado.Dividas.Single(x => x.ClienteId == a.Id).PrincipalAberto == 20 && catalogoBaixado.Pedidos.Count == 4 && !jsonDownload.Contains("saldoEstoque", StringComparison.OrdinalIgnoreCase) && !jsonDownload.Contains("lotes", StringComparison.OrdinalIgnoreCase), "Download inclui catálogo, pagamentos, compras e dívida sem expor estoque");
        var p235 = await Post<ProdutoDto>(admin, "/api/produtos", new ProdutoEntrada { Codigo = "235", Nome = "Produto para parcelar", GrupoId = grupo.Id, Preco = 47, Unidade = "UN" });
        await Requisicao(admin, HttpMethod.Post, "/api/estoque/entradas", new EntradaEstoque { ProdutoId = p235.Id, CodigoLote = "FIN", Quantidade = 20, Validade = hoje.AddDays(30) });
        var pedido235 = await Post<PedidoDto>(usuario, "/api/pedidos", Novo(a, 5, p235));
        var venda235 = (await Post<FaturamentoDto>(admin, $"/api/pedidos/{pedido235.Id}/faturar", Corte(pedido235, 5))).Venda!;
        var titulos235 = (await admin.GetFromJsonAsync<List<TituloDto>>("/api/titulos"))!.Where(x => x.VendaId == venda235.Id).OrderBy(x => x.Vencimento).ToList();
        Verificar(titulos235.Count == 2 && titulos235.All(x => x.ValorOriginal == 117.50m) && titulos235[0].Vencimento == hoje.AddDays(7) && titulos235[1].Vencimento == hoje.AddDays(14) && titulos235[1].Identificacao == $"{venda235.Numero}/2", "Venda de R$235 gera R$117,50 em 7 e 14 dias e identificadores venda/parcela");
        var condicaoTres = await Post<CondicaoPagamentoDto>(admin, "/api/condicoes-pagamento", new CondicaoPagamentoEntrada { Codigo = "3X", Nome = "Três parcelas", Parcelas = [new() { Dias = 7, Percentual = 33.33m, FormaPagamentoId = pix.Id }, new() { Dias = 14, Percentual = 33.33m, FormaPagamentoId = pix.Id }, new() { Dias = 21, Percentual = 33.34m, FormaPagamentoId = boleto.Id }] });
        var r3 = Novo(a, 5, p235); r3.CondicaoPagamentoId = condicaoTres.Id; r3.CondicaoVersao = condicaoTres.Versao;
        var pedido3 = await Post<PedidoDto>(usuario, "/api/pedidos", r3);
        var venda3 = (await Post<FaturamentoDto>(admin, $"/api/pedidos/{pedido3.Id}/faturar", Corte(pedido3, 5))).Venda!;
        var t3 = (await admin.GetFromJsonAsync<List<TituloDto>>("/api/titulos"))!.Where(x => x.VendaId == venda3.Id).OrderBy(x => x.Vencimento).ToList();
        Verificar(t3.Select(x => x.ValorOriginal).SequenceEqual(new[] { 78.33m, 78.33m, 78.34m }) && t3.Sum(x => x.ValorOriginal) == 235, "Arredondamento de três parcelas fecha exatamente o total da venda");
        var dataAtraso = titulos235[0].Vencimento.AddDays(2);
        var atrasado = (await admin.GetFromJsonAsync<List<TituloDto>>($"/api/titulos?data={dataAtraso:yyyy-MM-dd}"))!.Single(x => x.Id == titulos235[0].Id);
        Verificar(atrasado.Multa == 5.88m && atrasado.Juros == 4.70m && atrasado.ValorQuitacao == 128.08m, "Multa única e juros simples em dias corridos são calculados sem gravar cobranças repetidas");
        condicaoEntrada.Versao = condicao.Versao; condicaoEntrada.MultaPercentual = 1;
        condicao = Ler<CondicaoPagamentoDto>(await Requisicao(admin, HttpMethod.Put, $"/api/condicoes-pagamento/{condicao.Id}", condicaoEntrada));
        Verificar((await admin.GetFromJsonAsync<List<TituloDto>>($"/api/titulos?data={dataAtraso:yyyy-MM-dd}"))!.Single(x => x.Id == titulos235[0].Id).Multa == 5.88m, "Alterar condição não reescreve taxas das parcelas emitidas");
        var recebimento = new RecebimentoEntrada { Data = hoje, Versao = titulos235[0].Versao, FormaPagamentoId = boleto.Id, Valor = titulos235[0].ValorQuitacao };
        var valorCorreto = recebimento.Valor; recebimento.Valor = .01m;
        await Requisicao(admin, HttpMethod.Post, $"/api/titulos/{titulos235[0].Id}/receber", recebimento, HttpStatusCode.Conflict);
        recebimento.Valor = valorCorreto;
        var recebido = await Post<RecebimentoDto>(admin, $"/api/titulos/{titulos235[0].Id}/receber", recebimento);
        await Requisicao(admin, HttpMethod.Post, $"/api/titulos/{titulos235[0].Id}/receber", recebimento);
        Verificar(recebido.Principal == 117.5m && recebido.Desconto == 8.23m && recebido.Valor == 109.27m && recebido.FormaPagamento == "Boleto" && (await admin.GetFromJsonAsync<List<RecebimentoDto>>("/api/recebimentos"))!.Count == 1, "Baixa integral aplica desconto, registra forma real e resiste ao reenvio");
        await Requisicao(admin, HttpMethod.Post, $"/api/vendas/{venda235.Id}/cancelar", new CancelamentoEntrada { Versao = venda235.Versao, Motivo = "Teste" }, HttpStatusCode.BadRequest);
        var estornada = await Post<VendaDto>(admin, $"/api/vendas/{fb.Venda!.Id}/cancelar", new CancelamentoEntrada { Versao = fb.Venda.Versao, Motivo = "Entrega desfeita antes da saída" });
        await Requisicao(admin, HttpMethod.Post, $"/api/vendas/{fb.Venda.Id}/cancelar", new CancelamentoEntrada { Versao = fb.Venda.Versao, Motivo = "Entrega desfeita antes da saída" });
        var dividaB = (await usuario.GetFromJsonAsync<DividaClienteDto>($"/api/clientes/{b.Id}/divida"))!;
        Verificar(estornada.Cancelada && dividaB.PrincipalAberto == 0 && (await admin.GetFromJsonAsync<List<LoteDto>>("/api/estoque/lotes"))!.Where(x => x.ProdutoId == produto.Id && x.Validade >= hoje).Sum(x => x.Saldo) == 6 && (await usuario.GetFromJsonAsync<List<UltimaCompraDto>>($"/api/clientes/{b.Id}/ultimas-compras"))!.Count == 0, "Estorno devolve uma vez o estoque, cancela dívida e remove venda do histórico efetivo");
        var entregue = await Post<VendaDto>(admin, $"/api/vendas/{fc.Venda!.Id}/entregar", new ConfirmacaoEntrada { Versao = fc.Venda.Versao });
        await Requisicao(admin, HttpMethod.Post, $"/api/vendas/{entregue.Id}/cancelar", new CancelamentoEntrada { Versao = entregue.Versao, Motivo = "Teste" }, HttpStatusCode.BadRequest);
        Verificar(entregue.Entregue, "Entrega é registrada separadamente e impede estorno sem devolução");
        var perda = new PerdaEstoqueEntrada { LoteId = vencido.Id, Quantidade = 50, Motivo = "Vencimento", Versao = vencido.Versao };
        await Requisicao(admin, HttpMethod.Post, "/api/estoque/perdas", perda);
        await Requisicao(admin, HttpMethod.Post, "/api/estoque/perdas", perda);
        Verificar((await admin.GetFromJsonAsync<List<LoteDto>>("/api/estoque/lotes"))!.Single(x => x.Id == vencido.Id).Saldo == 0, "Perda de lote vencido fica registrada e não é aplicada duas vezes");
        await VerificarOffline(fabrica, admin, usuario, vendedor.Id, cidade.Id, territorio.Id, produto, condicao, a, pasta);
        foreach (var caminho in new[] { "/", "/Cadastros", "/Produtos", "/Produtos/Novo", $"/Produtos/Editar/{produto.Id}", "/Pagamentos", "/Pagamentos/NovaCondicao", $"/Pagamentos/EditarCondicao/{condicao.Id}", "/Estoque", "/Estoque/Entrada", $"/Estoque/Perda/{cedo.Id}", "/Pedidos", "/Pedidos/Novo", $"/Pedidos/Detalhe/{pa.Id}", "/Vendas", $"/Vendas/Detalhe/{venda235.Id}", "/Financeiro", $"/Financeiro/Receber/{titulos235[1].Id}", "/Roteiros/Novo", $"/Clientes/Detalhe/{a.Id}", $"/Clientes/Editar/{a.Id}" })
        {
            using var pagina = await site.GetAsync(caminho);
            Verificar(pagina.StatusCode == HttpStatusCode.OK && pagina.RequestMessage!.RequestUri!.AbsolutePath == caminho && (await pagina.Content.ReadAsStringAsync()).Contains("</html>"), "Tela integrada " + caminho);
        }
        await VerificarFormulariosComerciais(site, admin, grupo, pix, produto, condicao, a, hoje);
        var limitado = await Post<ProdutoDto>(admin, "/api/produtos", new ProdutoEntrada { Codigo = "ULTIMA", Nome = "Última unidade", GrupoId = grupo.Id, Preco = 10 });
        await Requisicao(admin, HttpMethod.Post, "/api/estoque/entradas", new EntradaEstoque { ProdutoId = limitado.Id, CodigoLote = "UNICO", Quantidade = 1, Validade = hoje.AddDays(5) });
        var simultaneo1 = await Post<PedidoDto>(usuario, "/api/pedidos", Novo(a, 1, limitado));
        var simultaneo2 = await Post<PedidoDto>(usuario, "/api/pedidos", Novo(c, 1, limitado));
        var respostas = await Task.WhenAll(admin.PostAsJsonAsync($"/api/pedidos/{simultaneo1.Id}/faturar", Corte(simultaneo1, 1)), admin.PostAsJsonAsync($"/api/pedidos/{simultaneo2.Id}/faturar", Corte(simultaneo2, 1)));
        try { Verificar(respostas.Count(x => x.IsSuccessStatusCode) == 1 && respostas.All(x => x.IsSuccessStatusCode || x.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict) && (await admin.GetFromJsonAsync<List<LoteDto>>("/api/estoque/lotes"))!.Single(x => x.ProdutoId == limitado.Id).Saldo == 0, "Duas confirmações simultâneas disputam a última unidade sem estoque negativo"); }
        finally { foreach (var resposta in respostas) resposta.Dispose(); }
        var desatualizado = Novo(a, 1, limitado);
        await Requisicao(admin, HttpMethod.Put, $"/api/produtos/{limitado.Id}", new ProdutoEntrada { Codigo = limitado.Codigo, Nome = limitado.Nome, GrupoId = grupo.Id, Preco = 11, Versao = limitado.Versao });
        await Requisicao(usuario, HttpMethod.Post, "/api/pedidos", desatualizado, HttpStatusCode.Conflict);
        desatualizado = Novo(a, 1.0001m);
        await Requisicao(usuario, HttpMethod.Post, "/api/pedidos", desatualizado, HttpStatusCode.BadRequest);
        Verificar(true, "Preço/versão antigos e quantidade com mais de três casas são recusados sem recalcular silenciosamente");
        var centavo = await Post<ProdutoDto>(admin, "/api/produtos", new ProdutoEntrada { Codigo = "CENTAVO", Nome = "Item mínimo", GrupoId = grupo.Id, Preco = .01m });
        await Requisicao(admin, HttpMethod.Post, "/api/estoque/entradas", new EntradaEstoque { ProdutoId = centavo.Id, CodigoLote = "MIN", Quantidade = 1, Validade = hoje.AddDays(5) });
        var minimo = await Post<PedidoDto>(usuario, "/api/pedidos", Novo(c, 1, centavo));
        await Requisicao(admin, HttpMethod.Post, $"/api/pedidos/{minimo.Id}/faturar", Corte(minimo, 1), HttpStatusCode.BadRequest);
        Verificar((await admin.GetFromJsonAsync<List<LoteDto>>("/api/estoque/lotes"))!.Single(x => x.ProdutoId == centavo.Id).Saldo == 1 && (await admin.GetFromJsonAsync<List<PedidoDto>>("/api/pedidos"))!.Single(x => x.Id == minimo.Id).Situacao == SituacaoPedido.Recebido && (await admin.GetFromJsonAsync<List<VendaDto>>("/api/vendas"))!.All(x => x.PedidoId != minimo.Id), "Parcela sem valor positivo desfaz também a baixa que ocorreu antes da geração financeira");
        using (var scope = fabrica.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<DistribuidoraDbContext>(); await AtualizacaoComercial.Aplicar(db); await AtualizacaoComercial.Aplicar(db); }
        await VerificarMigracaoPerfis(raiz, pasta, senha);
        using var banco = new SqliteConnection($"Data Source={Path.Combine(pasta, "banco.db")}"); banco.Open();
        using (var cmd = banco.CreateCommand()) { cmd.CommandText = "PRAGMA integrity_check"; Verificar((string)cmd.ExecuteScalar()! == "ok", "Banco comercial íntegro após faturamentos, recebimentos e atualização repetida"); }
        using (var cmd = banco.CreateCommand()) { cmd.CommandText = "PRAGMA foreign_key_check"; using var rows = cmd.ExecuteReader(); Verificar(!rows.Read(), "Relacionamentos comerciais preservados"); }
    }
    sealed class RespostaPerdidaHandler(HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        bool perder = true;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var resposta = await base.SendAsync(request, cancellationToken);
            if (perder && request.RequestUri!.AbsolutePath == "/api/pedidos" && resposta.IsSuccessStatusCode) { perder = false; resposta.Dispose(); throw new HttpRequestException("Resposta perdida depois do commit."); }
            return resposta;
        }
    }
    static async Task VerificarOffline(FabricaApi fabrica, HttpClient admin, HttpClient usuario, Guid vendedorId, Guid cidadeId, Guid territorioId, ProdutoDto produto, CondicaoPagamentoDto condicao, ClienteDto cliente, string pasta)
    {
        var caminho = Path.Combine(pasta, "vendedor.db3"); var local = new TrabalhoLocal(caminho); var cache = new CarteiraLocal(caminho);
        var dados = (await usuario.GetFromJsonAsync<PreparacaoDto>($"/api/vendedores/{vendedorId}/preparacao"))!;
        await cache.Salvar(dados);
        var novo = new ClienteEntrada { Id = Guid.NewGuid(), Nome = "Cliente cadastrado offline", Endereco = "Rua offline", CidadeId = cidadeId, TerritorioId = territorioId };
        await local.SalvarCliente(vendedorId, novo);
        var pedido = new PedidoEntrada { ClienteId = novo.Id, VendedorId = vendedorId, CondicaoPagamentoId = condicao.Id, CondicaoVersao = condicao.Versao, Itens = [new() { ProdutoId = produto.Id, ProdutoVersao = produto.Versao, Quantidade = 1, PrecoUnitario = produto.Preco }] };
        await local.SalvarPedido(vendedorId, pedido, false);
        using var proxy = new HttpClient(new RespostaPerdidaHandler(fabrica.Server.CreateHandler()));
        var envio = new EnvioPendencias(local, proxy); var token = usuario.DefaultRequestHeaders.Authorization!.Parameter!;
        await envio.Enviar(vendedorId, new Uri("http://localhost/"), token);
        Verificar((await local.Listar(vendedorId)).Single(x => x.Tipo == "Pedido").Situacao == "Rascunho" && (await admin.GetFromJsonAsync<List<ClienteDto>>("/api/clientes"))!.Any(x => x.Id == novo.Id), "Envio prioriza cliente e conserva pedido em rascunho sem enviar");
        await local.SalvarPedido(vendedorId, pedido, true);
        await envio.Enviar(vendedorId, new Uri("http://localhost/"), token);
        Verificar((await local.Listar(vendedorId)).Single(x => x.Tipo == "Pedido").Situacao == "Pendente" && (await admin.GetFromJsonAsync<List<PedidoDto>>("/api/pedidos"))!.Count(x => x.Id == pedido.Id) == 1, "Resposta perdida após gravação mantém pedido pendente no dispositivo");
        await cache.Salvar(dados with { AtualizadoEm = DateTimeOffset.UtcNow });
        Verificar((await new TrabalhoLocal(caminho).Listar(vendedorId)).Count == 2, "Atualizar carteira e reiniciar acesso ao SQLite não apaga pendências");
        var bloqueouEdicao = false;
        try { await local.SalvarPedido(vendedorId, pedido, true); } catch (InvalidOperationException) { bloqueouEdicao = true; }
        Verificar(bloqueouEdicao, "Pedido com confirmação incerta não permite alterar o conteúdo antes de reenviar");
        await envio.Enviar(vendedorId, new Uri("http://localhost/"), token);
        Verificar((await local.Listar(vendedorId)).All(x => x.Situacao == "Confirmado") && (await admin.GetFromJsonAsync<List<PedidoDto>>("/api/pedidos"))!.Count(x => x.Id == pedido.Id) == 1, "Reenvio com mesmo identificador confirma a operação sem duplicar o pedido");
        Verificar((await local.Listar(Guid.NewGuid())).Count == 0, "Fila local de outro vendedor permanece isolada");
        var invalido = new ClienteEntrada { Id = Guid.NewGuid(), Nome = "Cadastro inválido", Endereco = "Rua", Email = "inválido", CidadeId = cidadeId, TerritorioId = territorioId };
        await local.SalvarCliente(vendedorId, invalido);
        pedido = new() { ClienteId = invalido.Id, VendedorId = vendedorId, CondicaoPagamentoId = condicao.Id, CondicaoVersao = condicao.Versao, Itens = [new() { ProdutoId = produto.Id, ProdutoVersao = produto.Versao, Quantidade = 1, PrecoUnitario = produto.Preco }] };
        await local.SalvarPedido(vendedorId, pedido, true);
        var valido = new PedidoEntrada { ClienteId = cliente.Id, VendedorId = vendedorId, CondicaoPagamentoId = condicao.Id, CondicaoVersao = condicao.Versao, Itens = [new() { ProdutoId = produto.Id, ProdutoVersao = produto.Versao, Quantidade = 1, PrecoUnitario = produto.Preco }] };
        await local.SalvarPedido(vendedorId, valido, true);
        await envio.Enviar(vendedorId, new Uri("http://localhost/"), token);
        var operacoes = await local.Listar(vendedorId);
        Verificar(operacoes.Single(x => x.RegistroId == invalido.Id.ToString()).Situacao == "Rejeitado" && operacoes.Single(x => x.RegistroId == pedido.Id.ToString()).Situacao == "Pendente" && operacoes.Single(x => x.RegistroId == valido.Id.ToString()).Situacao == "Confirmado", "Falha individual conserva cadastro e dependências; outros pedidos são confirmados");
        var aposTransferencia = dados with { AtualizadoEm = DateTimeOffset.UtcNow.AddMinutes(1), Clientes = [] };
        Verificar(TrabalhoLocal.Clientes(aposTransferencia, operacoes).All(x => x.Id != novo.Id), "Cadastro já confirmado não reaparece indevidamente após transferência da carteira");
    }
    static async Task VerificarFormulariosComerciais(HttpClient site, HttpClient admin, GrupoProdutoDto grupo, FormaPagamentoDto forma, ProdutoDto produto, CondicaoPagamentoDto condicao, ClienteDto cliente, DateOnly hoje)
    {
        var cadastro = await Formulario(site, "/Produtos/Novo", new() { ["Entrada.Codigo"] = "WEB", ["Entrada.Nome"] = "Produto pelo site", ["Entrada.GrupoId"] = grupo.Id.ToString(), ["Entrada.Unidade"] = "KG", ["Entrada.Preco"] = "12.34", ["Entrada.Ativo"] = "true" });
        Verificar(cadastro.Caminho == "/Produtos" && (await admin.GetFromJsonAsync<List<ProdutoDto>>("/api/produtos"))!.Single(x => x.Codigo == "WEB").Preco == 12.34m, "Formulário de produto salva centavos corretamente em português");
        var estoque = await Formulario(site, "/Estoque/Entrada", new() { ["Entrada.Id"] = Guid.NewGuid().ToString(), ["Entrada.ProdutoId"] = produto.Id.ToString(), ["Entrada.CodigoLote"] = "WEB", ["Entrada.Validade"] = hoje.AddDays(15).ToString("yyyy-MM-dd"), ["Entrada.Quantidade"] = "2.125" });
        Verificar(estoque.Caminho == "/Estoque" && (await admin.GetFromJsonAsync<List<LoteDto>>("/api/estoque/lotes"))!.Single(x => x.Codigo == "WEB").Saldo == 2.125m, "Formulário de entrada preserva três casas na quantidade");
        var id = Guid.NewGuid();
        var pedido = await Formulario(site, "/Pedidos/Novo", new() { ["Id"] = id.ToString(), ["CriadoEm"] = DateTimeOffset.UtcNow.ToString("O"), ["ClienteId"] = cliente.Id.ToString(), ["CondicaoPagamentoId"] = condicao.Id.ToString(), ["CondicaoVersao"] = condicao.Versao.ToString(), ["Observacao"] = "", ["Itens[0].ProdutoId"] = produto.Id.ToString(), ["Itens[0].ProdutoVersao"] = produto.Versao.ToString(), ["Itens[0].PrecoUnitario"] = "10.00", ["Itens[0].Quantidade"] = "1" });
        Verificar(pedido.Caminho == $"/Pedidos/Detalhe/{id}", "Pedido criado pelo formulário recebe confirmação da API");
        var dto = (await admin.GetFromJsonAsync<PedidoDto>($"/api/pedidos/{id}"))!;
        var faturamento = await Formulario(site, $"/Pedidos/Faturar/{id}", new() { ["Corte.Versao"] = dto.Versao.ToString(), ["Corte.Motivo"] = "", ["Corte.Itens[0].ItemId"] = dto.Itens[0].Id.ToString(), ["Corte.Itens[0].Quantidade"] = "1" }, $"/Pedidos/Detalhe/{id}");
        var faturado = (await admin.GetFromJsonAsync<PedidoDto>($"/api/pedidos/{id}"))!;
        Verificar(faturamento.Caminho == $"/Pedidos/Detalhe/{id}" && faturado.VendaId is not null, "Formulário de corte confirma faturamento, venda e estoque");
        var titulo = (await admin.GetFromJsonAsync<List<TituloDto>>("/api/titulos"))!.First(x => x.VendaId == faturado.VendaId);
        var pagamento = await Formulario(site, $"/Financeiro/Receber/{titulo.Id}", new() { ["Entrada.Id"] = Guid.NewGuid().ToString(), ["Entrada.Versao"] = titulo.Versao.ToString(), ["Entrada.Data"] = hoje.ToString("yyyy-MM-dd"), ["Entrada.FormaPagamentoId"] = forma.Id.ToString(), ["Entrada.Valor"] = titulo.ValorQuitacao.ToString(System.Globalization.CultureInfo.InvariantCulture) });
        Verificar(pagamento.Caminho == "/Financeiro" && (await admin.GetFromJsonAsync<List<TituloDto>>("/api/titulos"))!.Single(x => x.Id == titulo.Id).Pago, "Formulário registra recebimento e atualiza a dívida");
        var parcela = await Formulario(site, "/Pagamentos/NovaCondicao", new() { ["Entrada.Codigo"] = "WEB-VISTA", ["Entrada.Nome"] = "À vista pelo site", ["Entrada.Ativo"] = "true", ["Entrada.MultaPercentual"] = "0", ["Entrada.JurosDiariosPercentual"] = "0", ["Entrada.DescontoDiarioPercentual"] = "0", ["Entrada.Parcelas[0].Dias"] = "0", ["Entrada.Parcelas[0].Percentual"] = "100", ["Entrada.Parcelas[0].FormaPagamentoId"] = forma.Id.ToString() });
        Verificar(parcela.Caminho == "/Pagamentos" && (await admin.GetFromJsonAsync<List<CondicaoPagamentoDto>>("/api/condicoes-pagamento"))!.Any(x => x.Codigo == "WEB-VISTA"), "Condição à vista pode ser cadastrada pelo site");
        await Formulario(site, "/Cadastros/Estado", new() { ["Nome"] = "Estado de teste", ["Sigla"] = "TT", ["Ativo"] = "true" }, "/Cadastros");
        var estado = (await admin.GetFromJsonAsync<List<EstadoDto>>("/api/estados"))!.Single(x => x.Sigla == "TT");
        await Formulario(site, "/Cadastros/Cidade", new() { ["Nome"] = "Cidade de teste", ["EstadoId"] = estado.Id.ToString(), ["Ativo"] = "true" }, "/Cadastros");
        var cidade = (await admin.GetFromJsonAsync<List<CidadeDto>>("/api/cidades"))!.Single(x => x.EstadoId == estado.Id);
        await Formulario(site, "/Cadastros/Vendedor", new() { ["Nome"] = "Vendedor pelo site", ["Ativo"] = "true" }, "/Cadastros");
        var vendedor = (await admin.GetFromJsonAsync<List<VendedorDto>>("/api/vendedores"))!.Single(x => x.Nome == "Vendedor pelo site");
        await Formulario(site, "/Cadastros/Territorio", new() { ["Nome"] = "Território pelo site", ["VendedorId"] = vendedor.Id.ToString(), ["CidadeIds"] = cidade.Id.ToString(), ["Ativo"] = "true" }, "/Cadastros");
        Verificar((await admin.GetFromJsonAsync<List<TerritorioDto>>("/api/territorios"))!.Any(x => x.Nome == "Território pelo site"), "Estados, cidades, vendedores e territórios podem ser mantidos pelo site");
        var territorioCliente = (await admin.GetFromJsonAsync<List<TerritorioDto>>("/api/territorios"))!.Single(x => x.Id == cliente.TerritorioId);
        var roteiro = await Formulario(site, "/Roteiros/Novo", new() { ["Entrada.Nome"] = "Roteiro organizado no site", ["Entrada.VendedorId"] = territorioCliente.VendedorId.ToString(), ["Entrada.DiaSemana"] = "2", ["Entrada.Ativo"] = "true", ["Entrada.Visitas[0].ClienteId"] = cliente.Id.ToString(), ["Entrada.Visitas[0].Sequencia"] = "15" });
        Verificar(roteiro.Caminho == "/Roteiros" && (await admin.GetFromJsonAsync<List<RoteiroDto>>("/api/roteiros"))!.Any(x => x.Nome == "Roteiro organizado no site" && x.Visitas[0].Sequencia == 15), "Site organiza roteiro com sequência intermediária 15");
        var editar = await Formulario(site, $"/Clientes/Editar/{cliente.Id}", new() { ["Entrada.Nome"] = cliente.Nome, ["Entrada.Documento"] = cliente.Documento, ["Entrada.Endereco"] = "Endereço atualizado", ["Entrada.CidadeId"] = cliente.CidadeId.ToString(), ["Entrada.TerritorioId"] = cliente.TerritorioId.ToString(), ["Entrada.Ativo"] = "false", ["Entrada.Versao"] = cliente.Versao.ToString() });
        Verificar(editar.Caminho == "/Clientes" && (await admin.GetFromJsonAsync<List<ClienteDto>>("/api/clientes"))!.Single(x => x.Id == cliente.Id).Ativo == false, "Edição e inativação de cliente pelo site preservam histórico");
        await Requisicao(admin, HttpMethod.Put, $"/api/clientes/{cliente.Id}", new ClienteEntrada { Nome = cliente.Nome, Documento = cliente.Documento, Endereco = cliente.Endereco, CidadeId = cliente.CidadeId, TerritorioId = cliente.TerritorioId, Versao = cliente.Versao + 1 });
    }
}

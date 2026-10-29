using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using PanificaB2B.Contratos;
using PanificaB2B.Web.Models;

namespace PanificaB2B.Web.Controllers;

[Authorize(Roles = Permissoes.Comercial)]
public sealed class PedidosController(IHttpClientFactory factory) : ApiMvcController(factory)
{
    public async Task<IActionResult> Index() => View(new PedidosModel { Pedidos = await Api.GetFromJsonAsync<List<PedidoDto>>("api/pedidos") ?? [], Lotes = await Api.GetFromJsonAsync<List<LoteDto>>("api/estoque/lotes") ?? [] });
    async Task<FormPedido> Formulario(FormPedido? model = null)
    {
        model ??= new();
        model.Clientes = (await Api.GetFromJsonAsync<List<ClienteDto>>("api/clientes") ?? []).Where(x => x.Ativo).ToList();
        var gruposAtivos = (await Api.GetFromJsonAsync<List<GrupoProdutoDto>>("api/grupos-produto") ?? []).Where(x => x.Ativo).Select(x => x.Id).ToHashSet();
        model.Produtos = (await Api.GetFromJsonAsync<List<ProdutoDto>>("api/produtos") ?? []).Where(x => x.Ativo && gruposAtivos.Contains(x.GrupoId)).ToList();
        model.Condicoes = (await Api.GetFromJsonAsync<List<CondicaoPagamentoDto>>("api/condicoes-pagamento") ?? []).Where(x => x.Ativo).ToList();
        if (model.Itens.Count == 0) model.Itens = model.Produtos.Select(x => new LinhaPedido { ProdutoId = x.Id, ProdutoVersao = x.Versao, PrecoUnitario = x.PrecoNaData(DateOnly.FromDateTime(DateTime.Today)) }).ToList();
        return model;
    }
    public async Task<IActionResult> Novo() => View(await Formulario());
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Novo(FormPedido form)
    {
        if (!ModelState.IsValid) return View(await Formulario(form));
        var cliente = (await Api.GetFromJsonAsync<List<ClienteDto>>("api/clientes"))!.SingleOrDefault(x => x.Id == form.ClienteId);
        var territorios = (await Api.GetFromJsonAsync<List<TerritorioDto>>("api/territorios"))!;
        var territorio = territorios.SingleOrDefault(x => x.Id == cliente?.TerritorioId);
        var condicao = (await Api.GetFromJsonAsync<List<CondicaoPagamentoDto>>("api/condicoes-pagamento"))!.SingleOrDefault(x => x.Id == form.CondicaoPagamentoId);
        if (cliente is null || territorio is null || condicao is null) ModelState.AddModelError("", "Selecione cliente e condição de pagamento.");
        else
        {
            var r = new PedidoEntrada { Id = form.Id, CriadoEm = form.CriadoEm, ClienteId = cliente.Id, VendedorId = territorio.VendedorId, CondicaoPagamentoId = condicao.Id, CondicaoVersao = form.CondicaoVersao, Observacao = form.Observacao,
                Itens = form.Itens.Where(x => x.Quantidade > 0).Select(x => new ItemPedidoEntrada { ProdutoId = x.ProdutoId, ProdutoVersao = x.ProdutoVersao, Quantidade = x.Quantidade, PrecoUnitario = x.PrecoUnitario }).ToList() };
            if (await Gravar("api/pedidos", r)) { Sucesso("Pedido recebido pela distribuidora."); return RedirectToAction(nameof(Detalhe), new { id = r.Id }); }
        }
        return View(await Formulario(form));
    }
    async Task<PedidoDto?> Pedido(Guid id) => (await Api.GetFromJsonAsync<List<PedidoDto>>("api/pedidos"))!.SingleOrDefault(x => x.Id == id);
    public async Task<IActionResult> Detalhe(Guid id)
    {
        var p = await Pedido(id);
        return p is null ? NotFound() : View(new FormFaturamento { Pedido = p, Corte = new() { Versao = p.Versao, Itens = p.Itens.Select(x => new CorteItemEntrada { ItemId = x.Id, Quantidade = x.QuantidadePedida }).ToList() }, Lotes = await Api.GetFromJsonAsync<List<LoteDto>>("api/estoque/lotes") ?? [] });
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Faturar(Guid id, [Bind(Prefix = "Corte")] FaturamentoEntrada r)
    {
        if (ModelState.IsValid && await Gravar($"api/pedidos/{id}/faturar", r)) { Sucesso("Atendimento confirmado; estoque e parcelas atualizados."); return RedirectToAction(nameof(Detalhe), new { id }); }
        var pedido = await Pedido(id);
        return pedido is null ? NotFound() : View("Detalhe", new FormFaturamento { Pedido = pedido, Corte = r, Lotes = await Api.GetFromJsonAsync<List<LoteDto>>("api/estoque/lotes") ?? [] });
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancelar(Guid id, CancelamentoEntrada r)
    {
        if (ModelState.IsValid && await Gravar($"api/pedidos/{id}/cancelar", r)) Sucesso("Pedido cancelado."); else GuardarErro();
        return RedirectToAction(nameof(Detalhe), new { id });
    }
}

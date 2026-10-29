using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using PanificaB2B.Contratos;
using PanificaB2B.Web.Models;

namespace PanificaB2B.Web.Controllers;

[Authorize(Roles = Permissoes.Estoque)]
public sealed class EstoqueController(IHttpClientFactory factory) : ApiMvcController(factory)
{
    public async Task<IActionResult> Index() => View(new EstoqueModel { Lotes = await Api.GetFromJsonAsync<List<LoteDto>>("api/estoque/lotes") ?? [], Movimentos = await Api.GetFromJsonAsync<List<MovimentoDto>>("api/estoque/movimentos") ?? [] });
    async Task<IActionResult> Formulario(EntradaEstoque r) => View("Entrada", new FormEntradaEstoque { Entrada = r, Produtos = await Api.GetFromJsonAsync<List<ProdutoDto>>("api/produtos") ?? [] });
    public Task<IActionResult> Entrada() => Formulario(new());
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Entrada([Bind(Prefix = "Entrada")] EntradaEstoque r)
    {
        if (!ModelState.IsValid || !await Gravar("api/estoque/entradas", r)) return await Formulario(r);
        Sucesso("Entrada registrada no estoque."); return RedirectToAction(nameof(Index));
    }
    async Task<LoteDto?> Lote(Guid id) => (await Api.GetFromJsonAsync<List<LoteDto>>("api/estoque/lotes"))!.SingleOrDefault(x => x.Id == id);
    public async Task<IActionResult> Perda(Guid id)
    {
        var lote = await Lote(id);
        return lote is null ? NotFound() : View(new FormPerda { Lote = lote, Entrada = new() { LoteId = id, Versao = lote.Versao } });
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Perda([Bind(Prefix = "Entrada")] PerdaEstoqueEntrada r)
    {
        if (!ModelState.IsValid || !await Gravar("api/estoque/perdas", r)) return View(new FormPerda { Lote = await Lote(r.LoteId), Entrada = r });
        Sucesso("Perda registrada com histórico."); return RedirectToAction(nameof(Index));
    }
}

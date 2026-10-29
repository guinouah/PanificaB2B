using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using PanificaB2B.Contratos;

namespace PanificaB2B.Web.Controllers;

[Authorize(Roles = Permissoes.Equipe)]
public sealed class VendasController(IHttpClientFactory factory) : ApiMvcController(factory)
{
    public async Task<IActionResult> Index() => View(await Api.GetFromJsonAsync<List<VendaDto>>("api/vendas") ?? []);
    public async Task<IActionResult> Detalhe(Guid id) => View(await Api.GetFromJsonAsync<VendaDto>($"api/vendas/{id}"));
    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = Permissoes.Estoque)]
    public async Task<IActionResult> Entregar(Guid id, int versao)
    {
        if (await Gravar($"api/vendas/{id}/entregar", new ConfirmacaoEntrada { Versao = versao })) Sucesso("Entrega confirmada."); else GuardarErro();
        return RedirectToAction(nameof(Detalhe), new { id });
    }
    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = Permissoes.Comercial)]
    public async Task<IActionResult> Cancelar(Guid id, CancelamentoEntrada r)
    {
        if (ModelState.IsValid && await Gravar($"api/vendas/{id}/cancelar", r)) Sucesso("Venda estornada; estoque devolvido e títulos cancelados."); else GuardarErro();
        return RedirectToAction(nameof(Detalhe), new { id });
    }
}

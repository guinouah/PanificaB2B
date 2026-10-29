using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using PanificaB2B.Contratos;
using PanificaB2B.Web.Models;

namespace PanificaB2B.Web.Controllers;

[Authorize(Roles = Permissoes.Financeiro)]
public sealed class FinanceiroController(IHttpClientFactory factory) : ApiMvcController(factory)
{
    public async Task<IActionResult> Index(DateOnly? data)
    {
        var dia = data ?? DateOnly.FromDateTime(DateTime.Today);
        return View(new FinanceiroModel { Data = dia, Titulos = await Api.GetFromJsonAsync<List<TituloDto>>($"api/titulos?data={dia:yyyy-MM-dd}") ?? [], Recebimentos = await Api.GetFromJsonAsync<List<RecebimentoDto>>("api/recebimentos") ?? [] });
    }
    async Task<FormReceber?> Formulario(Guid id, DateOnly data, RecebimentoEntrada? entrada = null)
    {
        var titulo = (await Api.GetFromJsonAsync<List<TituloDto>>($"api/titulos?data={data:yyyy-MM-dd}"))!.SingleOrDefault(x => x.Id == id);
        return titulo is null ? null : new() { Titulo = titulo, Entrada = entrada ?? new() { Data = data, Versao = titulo.Versao, FormaPagamentoId = titulo.FormaPagamentoId, Valor = titulo.ValorQuitacao }, Formas = (await Api.GetFromJsonAsync<List<FormaPagamentoDto>>("api/formas-pagamento") ?? []).Where(f=>CatalogosCadastro.Forma(f.Nome) is not null).ToList() };
    }
    public async Task<IActionResult> Receber(Guid id, DateOnly? data)
    {
        var model = await Formulario(id, data ?? DateOnly.FromDateTime(DateTime.Today));
        return model is null ? NotFound() : View(model);
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Receber(Guid id, [Bind(Prefix = "Entrada")] RecebimentoEntrada r)
    {
        if (ModelState.IsValid && await Gravar($"api/titulos/{id}/receber", r)) { Sucesso("Recebimento registrado."); return RedirectToAction(nameof(Index)); }
        var model = await Formulario(id, r.Data, r);
        return model is null ? NotFound() : View(model);
    }
}

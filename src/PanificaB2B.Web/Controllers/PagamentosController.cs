using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using PanificaB2B.Contratos;
using PanificaB2B.Web.Models;

namespace PanificaB2B.Web.Controllers;

[Authorize(Roles = Permissoes.Financeiro)]
public sealed class PagamentosController(IHttpClientFactory factory) : ApiMvcController(factory)
{
    public async Task<IActionResult> Index() => View(new PagamentosModel { Formas = await Api.GetFromJsonAsync<List<FormaPagamentoDto>>("api/formas-pagamento") ?? [], Condicoes = await Api.GetFromJsonAsync<List<CondicaoPagamentoDto>>("api/condicoes-pagamento") ?? [] });
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Forma(Guid? id, FormaPagamentoEntrada entrada)
    {
        if (ModelState.IsValid && await Gravar("api/formas-pagamento", entrada, id)) Sucesso("Forma de pagamento salva."); else GuardarErro();
        return RedirectToAction(nameof(Index));
    }
    async Task<IActionResult> Formulario(CondicaoPagamentoEntrada entrada, Guid? id = null)
    {
        ViewData["Id"] = id; ViewData["Acao"] = id is null ? "NovaCondicao" : "EditarCondicao";
        return View("Condicao", new FormCondicao { Entrada = entrada, Formas = await Api.GetFromJsonAsync<List<FormaPagamentoDto>>("api/formas-pagamento") ?? [] });
    }
    public Task<IActionResult> NovaCondicao() => Formulario(new() { Parcelas = [new() { Dias = 7, Percentual = 50 }, new() { Dias = 14, Percentual = 50 }] });
    public async Task<IActionResult> EditarCondicao(Guid id)
    {
        var x = (await Api.GetFromJsonAsync<List<CondicaoPagamentoDto>>("api/condicoes-pagamento"))!.SingleOrDefault(x => x.Id == id);
        return x is null ? NotFound() : await Formulario(new() { Codigo = x.Codigo, Nome = x.Nome, Ativo = x.Ativo, Versao = x.Versao, MultaPercentual = x.MultaPercentual, JurosDiariosPercentual = x.JurosDiariosPercentual, DescontoDiarioPercentual = x.DescontoDiarioPercentual,
            Parcelas = x.Parcelas.Select(p => new ParcelaCondicaoEntrada { Dias = p.Dias, Percentual = p.Percentual, FormaPagamentoId = p.FormaPagamentoId }).ToList() }, id);
    }
    [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> NovaCondicao([Bind(Prefix = "Entrada")] CondicaoPagamentoEntrada entrada) => Salvar(entrada, null);
    [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> EditarCondicao(Guid id, [Bind(Prefix = "Entrada")] CondicaoPagamentoEntrada entrada) => Salvar(entrada, id);
    async Task<IActionResult> Salvar(CondicaoPagamentoEntrada r, Guid? id)
    {
        if (!ModelState.IsValid || !await Gravar("api/condicoes-pagamento", r, id)) return await Formulario(r, id);
        Sucesso("Condição de pagamento salva."); return RedirectToAction(nameof(Index));
    }
}

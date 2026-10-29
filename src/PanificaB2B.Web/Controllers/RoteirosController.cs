using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using PanificaB2B.Contratos;
using PanificaB2B.Web.Models;
namespace PanificaB2B.Web.Controllers;
[Authorize(Roles = Permissoes.Comercial)]
public sealed class RoteirosController(IHttpClientFactory factory) : ApiMvcController(factory)
{
    public async Task<IActionResult> Index() => View(await Api.GetFromJsonAsync<List<RoteiroDto>>("api/roteiros") ?? []);
    async Task<IActionResult> Formulario(RoteiroEntrada r, Guid? id = null)
    {
        ViewData["Id"] = id; ViewData["Acao"] = id is null ? "Novo" : "Editar";
        return View("Formulario", new FormRoteiro { Entrada = r, Vendedores = await Api.GetFromJsonAsync<List<VendedorDto>>("api/vendedores") ?? [], Clientes = await Api.GetFromJsonAsync<List<ClienteDto>>("api/clientes") ?? [], Territorios = await Api.GetFromJsonAsync<List<TerritorioDto>>("api/territorios") ?? [] });
    }
    public Task<IActionResult> Novo() => Formulario(new() { DiaSemana = 1, Visitas = [new() { Sequencia = 10 }] });
    public async Task<IActionResult> Editar(Guid id)
    {
        var x = (await Api.GetFromJsonAsync<List<RoteiroDto>>("api/roteiros"))!.SingleOrDefault(x => x.Id == id);
        return x is null ? NotFound() : await Formulario(new() { Nome = x.Nome, VendedorId = x.VendedorId, DiaSemana = x.DiaSemana, Ativo = x.Ativo, Versao = x.Versao, Visitas = x.Visitas.Select(v => new VisitaEntrada { ClienteId = v.ClienteId, Sequencia = v.Sequencia }).ToList() }, id);
    }
    [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> Novo([Bind(Prefix = "Entrada")] RoteiroEntrada r) => Salvar(r, null);
    [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> Editar(Guid id, [Bind(Prefix = "Entrada")] RoteiroEntrada r) => Salvar(r, id);
    async Task<IActionResult> Salvar(RoteiroEntrada r, Guid? id)
    {
        if (!ModelState.IsValid || !await Gravar("api/roteiros", r, id)) return await Formulario(r, id);
        Sucesso("Roteiro salvo."); return RedirectToAction(nameof(Index));
    }
}

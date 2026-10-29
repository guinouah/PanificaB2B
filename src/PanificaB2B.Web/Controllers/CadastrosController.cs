using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using PanificaB2B.Contratos;
using PanificaB2B.Web.Models;

namespace PanificaB2B.Web.Controllers;

[Authorize(Roles = Permissoes.Comercial)]
public sealed class CadastrosController(IHttpClientFactory factory) : ApiMvcController(factory)
{
    public async Task<IActionResult> Index() => View(new CadastrosModel { Estados = await Api.GetFromJsonAsync<List<EstadoDto>>("api/estados") ?? [], Cidades = await Api.GetFromJsonAsync<List<CidadeDto>>("api/cidades") ?? [], Vendedores = await Api.GetFromJsonAsync<List<VendedorDto>>("api/vendedores") ?? [], Territorios = await Api.GetFromJsonAsync<List<TerritorioDto>>("api/territorios") ?? [] });
    async Task<IActionResult> Salvar(string rota, object r, Guid? id)
    {
        if (ModelState.IsValid && await Gravar(rota, r, id)) Sucesso("Cadastro salvo."); else GuardarErro();
        return RedirectToAction(nameof(Index));
    }
    [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> Estado(Guid? id, EstadoEntrada r) => Salvar("api/estados", r, id);
    [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> Cidade(Guid? id, CidadeEntrada r) => Salvar("api/cidades", r, id);
    [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> Vendedor(Guid? id, VendedorEntrada r) => Salvar("api/vendedores", r, id);
    [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> Territorio(Guid? id, TerritorioEntrada r) => Salvar("api/territorios", r, id);
}

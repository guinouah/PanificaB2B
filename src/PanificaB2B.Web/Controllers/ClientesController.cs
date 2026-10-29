using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using PanificaB2B.Contratos;
using PanificaB2B.Web.Models;

namespace PanificaB2B.Web.Controllers;

[Authorize(Roles = Permissoes.ComercialFinanceiro)]
public sealed class ClientesController(IHttpClientFactory factory) : Controller
{
    HttpClient Api => factory.CreateClient("Distribuidora");
    public async Task<IActionResult> Index() => View(await Api.GetFromJsonAsync<List<ClienteDto>>("api/clientes") ?? []);
    async Task<FormCliente> Formulario(ClienteEntrada? entrada = null) => new()
    {
        Entrada = entrada ?? new(),
        Cidades = (await Api.GetFromJsonAsync<List<CidadeDto>>("api/cidades") ?? []).Where(x => x.Ativo || x.Id == entrada?.CidadeId).ToList(),
        Territorios = (await Api.GetFromJsonAsync<List<TerritorioDto>>("api/territorios") ?? []).Where(x => x.Ativo || x.Id == entrada?.TerritorioId).ToList()
    };
    [Authorize(Roles = Permissoes.Comercial)]
    public async Task<IActionResult> Novo() => View(await Formulario());
    [Authorize(Roles = Permissoes.Comercial)]
    public async Task<IActionResult> Editar(Guid id)
    {
        var x = (await Api.GetFromJsonAsync<List<ClienteDto>>("api/clientes"))!.SingleOrDefault(x => x.Id == id);
        if (x is null) return NotFound();
        ViewData["Id"] = id;
        return View("Novo", await Formulario(new() { Nome = x.Nome, Documento = x.Documento, Email = x.Email, Telefone = x.Telefone, Endereco = x.Endereco, CidadeId = x.CidadeId, TerritorioId = x.TerritorioId, Ativo = x.Ativo, Versao = x.Versao }));
    }
    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = Permissoes.Comercial)]
    public async Task<IActionResult> Editar(Guid id, [Bind(Prefix = "Entrada")] ClienteEntrada entrada)
    {
        ViewData["Id"] = id;
        if (!ModelState.IsValid) return View("Novo", await Formulario(entrada));
        using var resposta = await Api.PutAsJsonAsync($"api/clientes/{id}", entrada);
        if (resposta.IsSuccessStatusCode) return RedirectToAction(nameof(Index));
        var erro = await resposta.Content.ReadFromJsonAsync<Dictionary<string, System.Text.Json.JsonElement>>();
        ModelState.AddModelError("", erro?.TryGetValue("mensagem", out var m) == true ? m.GetString() ?? "Confira os dados." : "Confira os dados.");
        return View("Novo", await Formulario(entrada));
    }
    public async Task<IActionResult> Detalhe(Guid id)
    {
        var cliente = (await Api.GetFromJsonAsync<List<ClienteDto>>("api/clientes"))!.SingleOrDefault(x => x.Id == id);
        return cliente is null ? NotFound() : View(new ClienteDetalhes(cliente, (await Api.GetFromJsonAsync<DividaClienteDto>($"api/clientes/{id}/divida"))!, await Api.GetFromJsonAsync<List<UltimaCompraDto>>($"api/clientes/{id}/ultimas-compras") ?? []));
    }
    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(Roles = Permissoes.Comercial)]
    public async Task<IActionResult> Novo([Bind(Prefix = "Entrada")] ClienteEntrada entrada)
    {
        if (!ModelState.IsValid) return View(await Formulario(entrada));
        using var response = await Api.PostAsJsonAsync("api/clientes", entrada);
        if (!response.IsSuccessStatusCode)
        {
            var erro = await response.Content.ReadFromJsonAsync<Dictionary<string, System.Text.Json.JsonElement>>();
            var mensagem = erro is not null && erro.TryGetValue("mensagem", out var detalhe) && detalhe.ValueKind == System.Text.Json.JsonValueKind.String ? detalhe.GetString() : "Revise os dados do cadastro.";
            ModelState.AddModelError("", mensagem ?? "Revise os dados do cadastro.");
            return View(await Formulario(entrada));
        }
        return RedirectToAction(nameof(Index));
    }
}

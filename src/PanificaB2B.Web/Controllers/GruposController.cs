using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PanificaB2B.Contratos;

namespace PanificaB2B.Web.Controllers;

[Authorize(Roles = Permissoes.Comercial)]
public sealed class GruposController(IHttpClientFactory factory) : Controller
{
    HttpClient Api => factory.CreateClient("Distribuidora");
    public async Task<IActionResult> Index() => View(await Api.GetFromJsonAsync<List<GrupoProdutoDto>>("api/grupos-produto") ?? []);
    IActionResult Formulario(GrupoProdutoEntrada entrada, Guid? id = null)
    {
        ViewData["Id"] = id;
        ViewData["Acao"] = id is null ? "Novo" : "Editar";
        return View("Formulario", entrada);
    }
    public IActionResult Novo() => Formulario(new());
    public async Task<IActionResult> Editar(Guid id)
    {
        using var result = await Api.GetAsync($"api/grupos-produto/{id}");
        if (result.StatusCode == HttpStatusCode.NotFound) return NotFound();
        result.EnsureSuccessStatusCode();
        var grupo = (await result.Content.ReadFromJsonAsync<GrupoProdutoDto>())!;
        return Formulario(new() { Nome = grupo.Nome, Ativo = grupo.Ativo, Versao = grupo.Versao }, id);
    }
    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Novo(GrupoProdutoEntrada entrada) => Salvar(entrada, null);
    [HttpPost, ValidateAntiForgeryToken]
    public Task<IActionResult> Editar(Guid id, GrupoProdutoEntrada entrada) => Salvar(entrada, id);
    async Task<IActionResult> Salvar(GrupoProdutoEntrada entrada, Guid? id)
    {
        if (!ModelState.IsValid) return Formulario(entrada, id);
        using var response = id is null ? await Api.PostAsJsonAsync("api/grupos-produto", entrada) : await Api.PutAsJsonAsync($"api/grupos-produto/{id}", entrada);
        if (response.IsSuccessStatusCode)
        {
            TempData["Sucesso"] = id is null ? "Grupo cadastrado." : "Grupo atualizado.";
            return RedirectToAction(nameof(Index));
        }
        var erro = await response.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
        var mensagem = erro is not null && erro.TryGetValue("mensagem", out var detalhe) && detalhe.ValueKind == JsonValueKind.String ? detalhe.GetString() : "Não foi possível salvar. Confira os dados.";
        ModelState.AddModelError("", mensagem ?? "Não foi possível salvar.");
        return Formulario(entrada, id);
    }
}

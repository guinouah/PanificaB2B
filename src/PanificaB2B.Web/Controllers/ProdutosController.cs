using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using PanificaB2B.Contratos;
using PanificaB2B.Web.Models;

namespace PanificaB2B.Web.Controllers;

[Authorize(Roles = Permissoes.Comercial)]
public sealed class ProdutosController(IHttpClientFactory factory) : ApiMvcController(factory)
{
    public async Task<IActionResult> Index() => View(await Api.GetFromJsonAsync<List<ProdutoDto>>("api/produtos") ?? []);
    async Task<IActionResult> Formulario(ProdutoEntrada entrada, Guid? id = null)
    {
        ViewData["Id"] = id; ViewData["Acao"] = id is null ? "Novo" : "Editar";
        return View("Formulario", new FormProduto { Entrada = entrada, Grupos = await Api.GetFromJsonAsync<List<GrupoProdutoDto>>("api/grupos-produto") ?? [] });
    }
    public Task<IActionResult> Novo() => Formulario(new());
    public async Task<IActionResult> Editar(Guid id)
    {
        var p = (await Api.GetFromJsonAsync<List<ProdutoDto>>("api/produtos"))!.SingleOrDefault(x => x.Id == id);
        return p is null ? NotFound() : await Formulario(new() { Codigo = p.Codigo, Nome = p.Nome, GrupoId = p.GrupoId, Unidade = p.Unidade, Preco = p.Preco, PrecoPromocional = p.PrecoPromocional, PromocaoInicio = p.PromocaoInicio, PromocaoFim = p.PromocaoFim, NovidadeAte = p.NovidadeAte, Ativo = p.Ativo, Versao = p.Versao }, id);
    }
    [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> Novo([Bind(Prefix = "Entrada")] ProdutoEntrada entrada) => Salvar(entrada, null);
    [HttpPost, ValidateAntiForgeryToken] public Task<IActionResult> Editar(Guid id, [Bind(Prefix = "Entrada")] ProdutoEntrada entrada) => Salvar(entrada, id);
    async Task<IActionResult> Salvar(ProdutoEntrada r, Guid? id)
    {
        if (!ModelState.IsValid || !await Gravar("api/produtos", r, id)) return await Formulario(r, id);
        Sucesso("Produto salvo."); return RedirectToAction(nameof(Index));
    }
}

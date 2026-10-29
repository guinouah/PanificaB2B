using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PanificaB2B.Api.Dados;
using PanificaB2B.Api.Modelos;
using PanificaB2B.Api.Servicos;
using PanificaB2B.Contratos;

namespace PanificaB2B.Api.Controllers;

[ApiController, Authorize, Route("api/grupos-produto")]
public sealed class GruposProdutoController(DistribuidoraDbContext db) : ControllerBase
{
    static GrupoProdutoDto Dto(GrupoProduto x) => new(x.Id, x.Nome, x.Ativo, x.Versao);
    [HttpGet]
    public async Task<IActionResult> Listar()
    {
        var administrador = (User.IsInRole("Administrador") || User.IsInRole("GestorComercial"));
        var grupos = await db.GruposProduto.AsNoTracking().Where(x => administrador || x.Ativo).OrderBy(x => x.Nome).ToListAsync();
        return Ok(grupos.Select(Dto));
    }
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Consultar(Guid id)
    {
        var grupo = await db.GruposProduto.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
        if (grupo is null || !grupo.Ativo && !(User.IsInRole("Administrador") || User.IsInRole("GestorComercial"))) return NotFound();
        return Ok(Dto(grupo));
    }
    [Authorize(Roles = Permissoes.Comercial), HttpPost]
    public async Task<IActionResult> Criar(GrupoProdutoEntrada entrada) => await Salvar(null, entrada);
    [Authorize(Roles = Permissoes.Comercial), HttpPut("{id:guid}")]
    public async Task<IActionResult> Alterar(Guid id, GrupoProdutoEntrada entrada) => await Salvar(id, entrada);

    async Task<IActionResult> Salvar(Guid? id, GrupoProdutoEntrada entrada)
    {
        var nome = string.Join(' ', entrada.Nome.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Normalize(NormalizationForm.FormC);
        if (nome.Length == 0) throw new RegraNegocioException("Informe o nome do grupo.");
        var normalizado = nome.ToUpperInvariant();
        var grupo = id is null ? new GrupoProduto() : await db.GruposProduto.FindAsync(id) ?? throw new KeyNotFoundException();
        if (id is not null && grupo.Versao != entrada.Versao) throw new ConflitoException("Este grupo foi alterado. Recarregue o cadastro antes de salvar.");
        if (await db.GruposProduto.AnyAsync(x => x.NomeNormalizado == normalizado && x.Id != grupo.Id))
            throw new RegraNegocioException("Já existe um grupo com esse nome. Se estiver inativo, edite-o para reativar.");
        grupo.Nome = nome; grupo.NomeNormalizado = normalizado; grupo.Ativo = entrada.Ativo;
        if (id is null) db.GruposProduto.Add(grupo); else grupo.Versao++;
        await db.SaveChangesAsync();
        return id is null ? CreatedAtAction(nameof(Consultar), new { id = grupo.Id }, Dto(grupo)) : Ok(Dto(grupo));
    }
}

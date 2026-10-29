using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PanificaB2B.Api.Dados;
using PanificaB2B.Api.Servicos;
using PanificaB2B.Contratos;

namespace PanificaB2B.Api.Controllers;

[ApiController, Authorize, Route("api")]
public sealed class CatalogoController(DistribuidoraDbContext db, CatalogoService catalogo) : ControllerBase
{
    bool Admin => User.IsInRole("Administrador") || User.IsInRole("GestorComercial") || User.IsInRole("Financeiro") || User.IsInRole("EstoqueExpedicao");
    [HttpGet("produtos")] public async Task<IActionResult> Produtos() => Ok(await catalogo.Produtos(Admin));
    [Authorize(Roles = Permissoes.Comercial), HttpPost("produtos") ] public async Task<IActionResult> CriarProduto(ProdutoEntrada r) => Ok(await catalogo.SalvarProduto(null, r));
    [Authorize(Roles = Permissoes.Comercial), HttpPut("produtos/{id:guid}")] public async Task<IActionResult> AlterarProduto(Guid id, ProdutoEntrada r) => Ok(await catalogo.SalvarProduto(id, r));
    [HttpGet("formas-pagamento")] public async Task<IActionResult> Formas() => Ok(await db.FormasPagamento.AsNoTracking().Where(x => Admin || x.Ativo).OrderBy(x => x.Nome).Select(x => new FormaPagamentoDto(x.Id, x.Nome, x.Ativo)).ToListAsync());
    [Authorize(Roles = Permissoes.Financeiro), HttpPost("formas-pagamento")] public async Task<IActionResult> CriarForma(FormaPagamentoEntrada r) => Ok(await catalogo.SalvarForma(null, r));
    [Authorize(Roles = Permissoes.Financeiro), HttpPut("formas-pagamento/{id:guid}")] public async Task<IActionResult> AlterarForma(Guid id, FormaPagamentoEntrada r) => Ok(await catalogo.SalvarForma(id, r));
    [HttpGet("condicoes-pagamento")] public async Task<IActionResult> Condicoes() => Ok(await catalogo.Condicoes(Admin));
    [Authorize(Roles = Permissoes.Financeiro), HttpPost("condicoes-pagamento")] public async Task<IActionResult> CriarCondicao(CondicaoPagamentoEntrada r) => Ok(await catalogo.SalvarCondicao(null, r));
    [Authorize(Roles = Permissoes.Financeiro), HttpPut("condicoes-pagamento/{id:guid}")] public async Task<IActionResult> AlterarCondicao(Guid id, CondicaoPagamentoEntrada r) => Ok(await catalogo.SalvarCondicao(id, r));
}

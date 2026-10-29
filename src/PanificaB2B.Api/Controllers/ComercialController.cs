using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PanificaB2B.Api.Dados;
using PanificaB2B.Api.Seguranca;
using PanificaB2B.Api.Servicos;
using PanificaB2B.Contratos;
using static PanificaB2B.Api.Servicos.Valores;

namespace PanificaB2B.Api.Controllers;

[ApiController, Authorize, Route("api")]
public sealed class ComercialController(DistribuidoraDbContext db, PedidosService pedidos, EstoqueService estoque, FinanceiroService financeiro) : ControllerBase
{
    [Authorize(Roles = Permissoes.ConsultaEstoque), HttpGet("estoque/lotes") ] public async Task<IActionResult> Lotes() => Ok(await estoque.Lotes());
    [Authorize(Roles = Permissoes.ConsultaEstoque), HttpGet("estoque/movimentos") ] public async Task<IActionResult> Movimentos() => Ok(await estoque.Movimentos());
    [Authorize(Roles = Permissoes.Estoque), HttpPost("estoque/entradas") ] public async Task<IActionResult> Entrada(EntradaEstoque r) => Ok(await estoque.Entrada(r, User.Identidade().UsuarioId));
    [Authorize(Roles = Permissoes.Estoque), HttpPost("estoque/perdas") ] public async Task<IActionResult> Perda(PerdaEstoqueEntrada r) => Ok(await estoque.Perda(r, User.Identidade().UsuarioId));
    [Authorize(Roles = Permissoes.ComercialVendedor), HttpGet("pedidos") ] public async Task<IActionResult> Pedidos() => Ok(await pedidos.Listar(User.Identidade()));
    [Authorize(Roles = Permissoes.ComercialVendedor), HttpGet("pedidos/{id:guid}") ] public async Task<IActionResult> Pedido(Guid id)
    {
        var pedido = (await pedidos.Listar(User.Identidade())).SingleOrDefault(x => x.Id == id);
        return pedido is null ? NotFound() : Ok(pedido);
    }
    [Authorize(Roles = Permissoes.ComercialVendedor), HttpPost("pedidos") ] public async Task<IActionResult> Receber(PedidoEntrada r) => Ok(await pedidos.Receber(r, User.Identidade()));
    [Authorize(Roles = Permissoes.Comercial), HttpPost("pedidos/{id:guid}/faturar") ] public async Task<IActionResult> Faturar(Guid id, FaturamentoEntrada r) => Ok(await pedidos.Faturar(id, r, User.Identidade().UsuarioId));
    [Authorize(Roles = Permissoes.ComercialVendedor), HttpPost("pedidos/{id:guid}/cancelar") ]
    public async Task<IActionResult> CancelarPedido(Guid id, CancelamentoEntrada r)
    {
        if (!(await pedidos.Listar(User.Identidade())).Any(x => x.Id == id)) return NotFound();
        return Ok(await pedidos.Cancelar(id, r, User.Identidade().UsuarioId));
    }
    [Authorize(Roles = Permissoes.Equipe), HttpGet("vendas") ]
    public async Task<IActionResult> Vendas() => Ok((await db.Vendas.AsNoTracking().Include(x => x.Itens).OrderByDescending(x => x.Numero).ToListAsync()).Select(PedidosService.Dto));
    [Authorize(Roles = Permissoes.Equipe), HttpGet("vendas/{id:guid}") ]
    public async Task<IActionResult> Venda(Guid id)
    {
        var venda = await db.Vendas.AsNoTracking().Include(x => x.Itens).SingleOrDefaultAsync(x => x.Id == id);
        return venda is null ? NotFound() : Ok(PedidosService.Dto(venda));
    }
    [Authorize(Roles = Permissoes.Comercial), HttpPost("vendas/{id:guid}/cancelar") ] public async Task<IActionResult> CancelarVenda(Guid id, CancelamentoEntrada r) => Ok(await pedidos.CancelarVenda(id, r, User.Identidade().UsuarioId));
    [Authorize(Roles = Permissoes.Estoque), HttpPost("vendas/{id:guid}/entregar") ]
    public async Task<IActionResult> Entregar(Guid id, ConfirmacaoEntrada r)
    {
        var venda = await db.Vendas.Include(x => x.Itens).SingleOrDefaultAsync(x => x.Id == id) ?? throw new KeyNotFoundException();
        Exigir(!venda.Cancelada, "Venda cancelada.");
        if (!venda.Entregue) { Versao(r.Versao, venda.Versao); venda.Entregue = true; venda.EntregueEm = DateTimeOffset.UtcNow; venda.Versao++; await db.SaveChangesAsync(); }
        return Ok(PedidosService.Dto(venda));
    }
    [Authorize(Roles = Permissoes.Financeiro), HttpGet("titulos")] public async Task<IActionResult> Titulos(DateOnly? data) => Ok(await financeiro.Titulos(data ?? Hoje));
    [Authorize(Roles = Permissoes.Financeiro), HttpPost("titulos/{id:guid}/receber")] public async Task<IActionResult> ReceberTitulo(Guid id, RecebimentoEntrada r) => Ok(await financeiro.Receber(id, r, User.Identidade().UsuarioId));
    [Authorize(Roles = Permissoes.Financeiro), HttpGet("recebimentos")] public async Task<IActionResult> Recebimentos() => Ok((await db.Recebimentos.AsNoTracking().ToListAsync()).OrderByDescending(x => x.Data).Select(FinanceiroService.Dto));
    [Authorize(Roles = Permissoes.ComercialFinanceiro + ",Vendedor"), HttpGet("clientes/{id:guid}/ultimas-compras") ]
    public async Task<IActionResult> Ultimas(Guid id) => await pedidos.PodeCliente(id, User.Identidade()) ? Ok(await pedidos.UltimasCompras(id)) : NotFound();
    [Authorize(Roles = Permissoes.ComercialFinanceiro + ",Vendedor"), HttpGet("clientes/{id:guid}/divida") ]
    public async Task<IActionResult> Divida(Guid id)
    {
        if (!await pedidos.PodeCliente(id, User.Identidade())) return NotFound();
        var titulos = (await financeiro.Titulos(Hoje, id)).Where(x => !x.Pago && !x.Cancelado).ToList();
        return Ok(new DividaClienteDto(id, Hoje, titulos.Sum(x => x.SaldoPrincipal), titulos.Where(x => x.Vencimento < Hoje).Sum(x => x.SaldoPrincipal), titulos.Sum(x => x.ValorQuitacao), titulos));
    }
    [Authorize(Roles = Permissoes.Equipe), HttpGet("painel") ]
    public async Task<IActionResult> Painel()
    {
        var hoje = Hoje;
        var perfil = User.Identidade().Perfil;
        var comercial = Permissoes.PodeComercial(perfil);
        var financeiro = Permissoes.PodeFinanceiro(perfil);
        var estoquePermitido = Permissoes.PodeEstoque(perfil);
        return Ok(new PainelDto(comercial || financeiro ? await db.Clientes.CountAsync(x => x.Ativo) : 0, comercial || estoquePermitido ? await db.Produtos.CountAsync(x => x.Ativo) : 0, comercial ? await db.Pedidos.CountAsync(x => x.Situacao == SituacaoPedido.Recebido) : 0,
            comercial || financeiro ? (await db.Vendas.Where(x => !x.Cancelada).Select(x => x.TotalCentavos).ToListAsync()).Sum() / 100m : 0,
            financeiro ? (await db.Titulos.Where(x => !x.Pago && !x.Cancelado).Select(x => x.ValorCentavos).ToListAsync()).Sum() / 100m : 0,
            estoquePermitido ? await db.Lotes.CountAsync(x => x.Validade < hoje && x.SaldoMilesimos > 0) : 0));
    }
}

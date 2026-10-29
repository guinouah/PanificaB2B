using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using PanificaB2B.Api.Dados;
using PanificaB2B.Api.Servicos;
using PanificaB2B.Api.Seguranca;
using PanificaB2B.Contratos;

namespace PanificaB2B.Api.Controllers;

[Authorize, ApiController, Route("api")]
public sealed class CadastrosController(DistribuidoraDbContext db, CadastrosService service, CatalogoService catalogo, PedidosService pedidos, FinanceiroService financeiro) : ControllerBase
{
    [HttpGet("estados")] public async Task<IActionResult> Estados() => Ok((await db.Estados.AsNoTracking().OrderBy(x => x.Nome).ToListAsync()).Select(CadastrosService.Dto));
    [Authorize(Roles = Permissoes.Comercial), HttpPost("estados")] public async Task<IActionResult> CriarEstado(EstadoEntrada r) => Ok(await service.SalvarEstado(null, r));
    [Authorize(Roles = Permissoes.Comercial), HttpPut("estados/{id:guid}")] public async Task<IActionResult> AlterarEstado(Guid id, EstadoEntrada r) => Ok(await service.SalvarEstado(id, r));
    [HttpGet("cidades")] public async Task<IActionResult> Cidades() => Ok((await db.Cidades.AsNoTracking().OrderBy(x => x.Nome).ToListAsync()).Select(CadastrosService.Dto));
    [Authorize(Roles = Permissoes.Comercial), HttpPost("cidades")] public async Task<IActionResult> CriarCidade(CidadeEntrada r) => Ok(await service.SalvarCidade(null, r));
    [Authorize(Roles = Permissoes.Comercial), HttpPut("cidades/{id:guid}")] public async Task<IActionResult> AlterarCidade(Guid id, CidadeEntrada r) => Ok(await service.SalvarCidade(id, r));
    [Authorize(Roles = Permissoes.ComercialVendedor), HttpGet("vendedores")] public async Task<IActionResult> Vendedores()
    {
        var usuario = User.Identidade();
        return Ok((await db.Vendedores.AsNoTracking().Where(x => Permissoes.PodeComercial(usuario.Perfil) || x.Id == usuario.VendedorId).OrderBy(x => x.Nome).ToListAsync()).Select(CadastrosService.Dto));
    }
    [Authorize(Roles = Permissoes.Comercial), HttpPost("vendedores")] public async Task<IActionResult> CriarVendedor(VendedorEntrada r) => Ok(await service.SalvarVendedor(null, r));
    [Authorize(Roles = Permissoes.Comercial), HttpPut("vendedores/{id:guid}")] public async Task<IActionResult> AlterarVendedor(Guid id, VendedorEntrada r) => Ok(await service.SalvarVendedor(id, r));
    [Authorize(Roles = Permissoes.ComercialVendedor), HttpGet("territorios")] public async Task<IActionResult> Territorios()
    {
        var usuario = User.Identidade();
        return Ok((await db.Territorios.AsNoTracking().Include(x => x.Cidades).Where(x => Permissoes.PodeComercial(usuario.Perfil) || x.VendedorId == usuario.VendedorId).OrderBy(x => x.Nome).ToListAsync()).Select(CadastrosService.Dto));
    }
    [Authorize(Roles = Permissoes.Comercial), HttpPost("territorios")] public async Task<IActionResult> CriarTerritorio(TerritorioEntrada r) => Ok(await service.SalvarTerritorio(null, r));
    [Authorize(Roles = Permissoes.Comercial), HttpPut("territorios/{id:guid}")] public async Task<IActionResult> AlterarTerritorio(Guid id, TerritorioEntrada r) => Ok(await service.SalvarTerritorio(id, r));
    [HttpGet("clientes")] public async Task<IActionResult> Clientes()
    {
        var usuario = User.Identidade();
        return Ok((await db.Clientes.AsNoTracking().Where(x => Permissoes.PodeUsarSite(usuario.Perfil) || db.Territorios.Any(t => t.Id == x.TerritorioId && t.VendedorId == usuario.VendedorId && t.Ativo)).OrderBy(x => x.Nome).ToListAsync()).Select(CadastrosService.Dto));
    }
    async Task<bool> PodeAlterarCliente(ClienteEntrada r, Guid? id)
    {
        var usuario = User.Identidade();
        if (AcessoUsuario.PodeComercial(usuario)) return true;
        var territorio = await db.Territorios.AsNoTracking().SingleOrDefaultAsync(t => t.Id == r.TerritorioId && t.Ativo);
        if (territorio is null || !AcessoUsuario.PodeAcessarCarteira(usuario, territorio.VendedorId)) return false;
        var clienteId = id ?? r.Id;
        var atual = await db.Clientes.AsNoTracking().SingleOrDefaultAsync(c => c.Id == clienteId);
        if (atual is null) return true;
        return await db.Territorios.AnyAsync(t => t.Id == atual.TerritorioId && t.Ativo && t.VendedorId == usuario.VendedorId);
    }
    [Authorize(Roles = Permissoes.ComercialVendedor), HttpPost("clientes")] public async Task<IActionResult> CriarCliente(ClienteEntrada r)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        if (!await PodeAlterarCliente(r, null)) return Forbid();
        var result = await service.SalvarCliente(null, r); await tx.CommitAsync(); return Ok(result);
    }
    [Authorize(Roles = Permissoes.ComercialVendedor), HttpPut("clientes/{id:guid}")] public async Task<IActionResult> AlterarCliente(Guid id, ClienteEntrada r)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        if (!await PodeAlterarCliente(r, id)) return Forbid();
        var result = await service.SalvarCliente(id, r); await tx.CommitAsync(); return Ok(result);
    }
    [Authorize(Roles = Permissoes.ComercialVendedor), HttpGet("roteiros")]
    public async Task<IActionResult> Roteiros(Guid? vendedorId, int? diaSemana)
    {
        var query = db.Roteiros.AsNoTracking().Include(x => x.Visitas).AsQueryable();
        var usuario = User.Identidade();
        if (usuario.Perfil == PerfilUsuario.Vendedor)
        {
            if (vendedorId is not null && !AcessoUsuario.PodeAcessarCarteira(usuario, vendedorId.Value)) return Forbid();
            query = query.Where(x => x.VendedorId == usuario.VendedorId && x.Ativo);
        }
        if (vendedorId is not null) query = query.Where(x => x.VendedorId == vendedorId);
        if (diaSemana is not null) query = query.Where(x => x.DiaSemana == diaSemana);
        var result = new List<RoteiroDto>();
        foreach (var item in await query.OrderBy(x => x.DiaSemana).ThenBy(x => x.Nome).ToListAsync()) result.Add(await service.Dto(item));
        return Ok(result);
    }
    [Authorize(Roles = Permissoes.Comercial), HttpPost("roteiros")] public async Task<IActionResult> CriarRoteiro(RoteiroEntrada r) => Ok(await service.SalvarRoteiro(null, r));
    [Authorize(Roles = Permissoes.Comercial), HttpPut("roteiros/{id:guid}")] public async Task<IActionResult> AlterarRoteiro(Guid id, RoteiroEntrada r) => Ok(await service.SalvarRoteiro(id, r));
    [HttpGet("vendedores/{id:guid}/preparacao")]
    public async Task<IActionResult> Preparacao(Guid id)
    {
        if (!AcessoUsuario.PodeAcessarCarteira(User.Identidade(), id)) return Forbid();
        if (!await db.Vendedores.AnyAsync(x => x.Id == id && x.Ativo)) return NotFound();
        var territorios = await db.Territorios.AsNoTracking().Include(x => x.Cidades).Where(x => x.VendedorId == id && x.Ativo).ToListAsync();
        var territorioIds = territorios.Select(x => x.Id).ToList();
        var cidadesIds = territorios.SelectMany(x => x.Cidades.Select(c => c.CidadeId)).Distinct().ToList();
        var cidades = await db.Cidades.AsNoTracking().Where(x => cidadesIds.Contains(x.Id) && x.Ativo).ToListAsync();
        var estadosIds = cidades.Select(x => x.EstadoId).ToList();
        var estados = await db.Estados.AsNoTracking().Where(x => estadosIds.Contains(x.Id)).ToListAsync();
        var clientes = await db.Clientes.AsNoTracking().Where(x => territorioIds.Contains(x.TerritorioId) && x.Ativo).ToListAsync();
        var clientesIds = clientes.Select(x => x.Id).ToHashSet();
        var roteiros = new List<RoteiroDto>();
        foreach (var rt in await db.Roteiros.AsNoTracking().Include(x => x.Visitas).Where(x => x.VendedorId == id && x.Ativo).ToListAsync())
        {
            var dto = await service.Dto(rt);
            roteiros.Add(dto with { Visitas = dto.Visitas.Where(v => clientesIds.Contains(v.ClienteId)).ToList() });
        }
        var ultimas = new List<UltimaCompraDto>();
        var dividas = new List<DividaClienteDto>();
        var hoje = Valores.Hoje;
        var titulos = await financeiro.Titulos(hoje, carteira: clientesIds.ToList());
        foreach (var cliente in clientes)
        {
            ultimas.AddRange(await pedidos.UltimasCompras(cliente.Id));
            var abertos = titulos.Where(x => x.ClienteId == cliente.Id && !x.Pago && !x.Cancelado).ToList();
            dividas.Add(new(cliente.Id, hoje, abertos.Sum(x => x.SaldoPrincipal), abertos.Where(x => x.Vencimento < hoje).Sum(x => x.SaldoPrincipal), abertos.Sum(x => x.ValorQuitacao), abertos));
        }
        return Ok(new PreparacaoDto(id, DateTimeOffset.UtcNow, estados.Select(CadastrosService.Dto).ToList(), cidades.Select(CadastrosService.Dto).ToList(), territorios.Select(CadastrosService.Dto).ToList(), clientes.Select(CadastrosService.Dto).ToList(), roteiros)
        {
            Grupos = await db.GruposProduto.AsNoTracking().Where(x => x.Ativo).Select(x => new GrupoProdutoDto(x.Id, x.Nome, x.Ativo, x.Versao)).ToListAsync(),
            Produtos = await catalogo.Produtos(false), CondicoesPagamento = await catalogo.Condicoes(false),
            FormasPagamento = await db.FormasPagamento.AsNoTracking().Where(x => x.Ativo).Select(x => new FormaPagamentoDto(x.Id, x.Nome, x.Ativo)).ToListAsync(),
            UltimasCompras = ultimas, Dividas = dividas,
            Pedidos = await pedidos.Listar(new IdentidadeUsuario(User.Identidade().UsuarioId, PerfilUsuario.Vendedor, id))
        });
    }
}

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PanificaB2B.Api.Dados;
using PanificaB2B.Api.Modelos;
using PanificaB2B.Api.Seguranca;
using PanificaB2B.Contratos;
using static PanificaB2B.Api.Servicos.Valores;

namespace PanificaB2B.Api.Servicos;

public sealed class PedidosService(DistribuidoraDbContext db, EstoqueService estoque, FinanceiroService financeiro)
{
    static string Hash(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
    public static VendaDto Dto(Venda x) => new(x.Id, x.Numero, x.PedidoId, x.ClienteId, x.ClienteNome, x.VendedorNome, x.CondicaoNome, x.Data, x.TotalCentavos / 100m, x.Cancelada, x.Entregue, x.Versao,
        x.Itens.Select(i => new ItemVendaDto(i.ProdutoId, i.ProdutoNome, i.Unidade, i.QuantidadeMilesimos / 1000m, i.PrecoCentavos / 100m, i.TotalCentavos / 100m)).ToList());
    public async Task<PedidoDto> Dto(Pedido x)
    {
        var vendaId = await db.Vendas.Where(v => v.PedidoId == x.Id).Select(v => (Guid?)v.Id).SingleOrDefaultAsync();
        return new(x.Id, x.ClienteId, x.ClienteNome, x.VendedorId, x.VendedorNome, x.CondicaoPagamentoId, x.CondicaoNome, x.CriadoEm, x.RecebidoEm, x.Situacao, x.Versao, x.Observacao,
            x.Itens.Sum(i => Total(i.PrecoCentavos, i.QuantidadeMilesimos)) / 100m,
            x.Itens.Select(i => new ItemPedidoDto(i.Id, i.ProdutoId, i.ProdutoNome, i.Unidade, i.QuantidadeMilesimos / 1000m, i.AtendidaMilesimos / 1000m, i.PrecoCentavos / 100m)).ToList(), vendaId);
    }
    public async Task<bool> PodeCliente(Guid clienteId, IdentidadeUsuario usuario)
    {
        if (Permissoes.PodeComercial(usuario.Perfil) || Permissoes.PodeFinanceiro(usuario.Perfil)) return await db.Clientes.AnyAsync(x => x.Id == clienteId);
        return await db.Clientes.AnyAsync(c => c.Id == clienteId && db.Territorios.Any(t => t.Id == c.TerritorioId && t.Ativo && t.VendedorId == usuario.VendedorId));
    }
    public async Task<List<PedidoDto>> Listar(IdentidadeUsuario usuario)
    {
        var pedidos = await db.Pedidos.AsNoTracking().Include(x => x.Itens).Where(x => Permissoes.PodeComercial(usuario.Perfil) || x.VendedorId == usuario.VendedorId).ToListAsync();
        var result = new List<PedidoDto>();
        foreach (var p in pedidos.OrderByDescending(x => x.RecebidoEm)) result.Add(await Dto(p));
        return result;
    }
    public async Task<PedidoDto> Receber(PedidoEntrada r, IdentidadeUsuario usuario)
    {
        Exigir(r.Id != Guid.Empty && r.CriadoEm != default && r.CriadoEm <= DateTimeOffset.UtcNow.AddMinutes(10), "Identificador ou data do pedido inválidos.");
        if (usuario.Perfil == PerfilUsuario.Vendedor && r.VendedorId != usuario.VendedorId) throw new RegraNegocioException("O vendedor do pedido deve ser o usuário autenticado.");
        Exigir(r.Itens.Count is >= 1 and <= 200 && r.Itens.All(i => i is not null) && r.Itens.Select(i => i.ProdutoId).Distinct().Count() == r.Itens.Count, "Informe produtos distintos no pedido.");
        var hash = Hash(new { r.Id, r.ClienteId, r.VendedorId, r.CondicaoPagamentoId, r.CondicaoVersao, CriadoEm = r.CriadoEm.ToUniversalTime(), Observacao = Texto(r.Observacao), Itens = r.Itens.OrderBy(i => i.ProdutoId).Select(i => new { i.ProdutoId, Quantidade = Milesimos(i.Quantidade), Preco = Centavos(i.PrecoUnitario), i.ProdutoVersao }) });
        await using var tx = await db.Database.BeginTransactionAsync();
        var existente = await db.Pedidos.Include(x => x.Itens).SingleOrDefaultAsync(x => x.Id == r.Id);
        if (existente is not null)
        {
            if (existente.VendedorId != r.VendedorId || existente.ConteudoHash != hash) throw new ConflitoException("Identificador do pedido já utilizado com outros dados.");
            return await Dto(existente);
        }
        Exigir(await PodeCliente(r.ClienteId, usuario), "Cliente fora da sua carteira.");
        var cliente = await db.Clientes.FindAsync(r.ClienteId);
        Exigir(cliente is { Ativo: true }, "Cliente inexistente ou inativo.");
        var territorio = await db.Territorios.FindAsync(cliente!.TerritorioId);
        Exigir(territorio is { Ativo: true } && territorio.VendedorId == r.VendedorId, "A carteira do cliente foi alterada. Atualize os dados.");
        var vendedor = await db.Vendedores.FindAsync(r.VendedorId);
        Exigir(vendedor is { Ativo: true }, "Vendedor inexistente ou inativo.");
        var condicao = await db.CondicoesPagamento.Include(x => x.Parcelas).SingleOrDefaultAsync(x => x.Id == r.CondicaoPagamentoId);
        Exigir(condicao is { Ativo: true }, "Condição de pagamento inexistente ou inativa.");
        if (condicao!.Versao != r.CondicaoVersao) throw new ConflitoException("A condição de pagamento mudou. Atualize e confira o pedido.");
        var formasIds = condicao.Parcelas.Select(x => x.FormaPagamentoId).Distinct().ToList();
        Exigir(await db.FormasPagamento.CountAsync(x => formasIds.Contains(x.Id) && x.Ativo) == formasIds.Count, "A condição utiliza forma de pagamento inativa.");
        var produtos = await db.Produtos.ToDictionaryAsync(x => x.Id);
        var grupos = await db.GruposProduto.ToDictionaryAsync(x => x.Id);
        var pedido = new Pedido { Id = r.Id, ClienteId = cliente.Id, ClienteNome = cliente.Nome, VendedorId = vendedor!.Id, VendedorNome = vendedor.Nome,
            CondicaoPagamentoId = condicao.Id, CondicaoNome = condicao.Nome, CondicaoVersao = condicao.Versao, CriadoEm = r.CriadoEm, Observacao = Texto(r.Observacao), ConteudoHash = hash };
        foreach (var i in r.Itens)
        {
            Exigir(produtos.TryGetValue(i.ProdutoId, out var produto) && produto.Ativo && grupos[produto.GrupoId].Ativo, "Produto inexistente, inativo ou com grupo inativo. Atualize o catálogo.");
            var dto = CatalogoService.Dto(produto!, grupos[produto!.GrupoId].Nome);
            if (i.ProdutoVersao != produto.Versao || Centavos(i.PrecoUnitario) != Centavos(dto.PrecoNaData(Hoje))) throw new ConflitoException($"Preço ou cadastro de {produto.Nome} mudou. Atualize e confira o pedido.");
            var quantidade = Milesimos(i.Quantidade); var preco = Centavos(i.PrecoUnitario);
            Exigir(quantidade > 0 && preco > 0 && Total(preco, quantidade) > 0, "A quantidade deve gerar um item de pelo menos R$ 0,01.");
            pedido.Itens.Add(new() { PedidoId = pedido.Id, ProdutoId = produto.Id, ProdutoNome = produto.Nome, Unidade = produto.Unidade, QuantidadeMilesimos = quantidade, PrecoCentavos = preco, ProdutoVersao = produto.Versao });
        }
        Exigir(pedido.Itens.Sum(i => Total(i.PrecoCentavos, i.QuantidadeMilesimos)) <= 100000000000, "O total do pedido supera o limite desta aplicação.");
        db.Pedidos.Add(pedido); await db.SaveChangesAsync(); await tx.CommitAsync(); return await Dto(pedido);
    }
    public async Task<FaturamentoDto> Faturar(Guid pedidoId, FaturamentoEntrada r, Guid usuarioId)
    {
        Exigir(r.Itens.Count is >= 1 and <= 200 && r.Itens.All(i => i is not null), "Confira os itens do atendimento.");
        var hash = Hash(new { Motivo = Texto(r.Motivo), Itens = r.Itens.OrderBy(x => x.ItemId).Select(x => new { x.ItemId, Quantidade = Milesimos(x.Quantidade) }) });
        await using var tx = await db.Database.BeginTransactionAsync();
        var pedido = await db.Pedidos.Include(x => x.Itens).SingleOrDefaultAsync(x => x.Id == pedidoId) ?? throw new KeyNotFoundException();
        if (pedido.Situacao is SituacaoPedido.Faturado or SituacaoPedido.FaturadoParcialmente or SituacaoPedido.NaoAtendido)
        {
            if (pedido.FaturamentoHash != hash) throw new ConflitoException("Este pedido já foi analisado com outro atendimento.");
            var anterior = await db.Vendas.AsNoTracking().Include(x => x.Itens).SingleOrDefaultAsync(x => x.PedidoId == pedidoId);
            return new(await Dto(pedido), anterior is null ? null : Dto(anterior));
        }
        Exigir(pedido.Situacao == SituacaoPedido.Recebido, "Pedido encerrado não pode ser faturado.");
        Versao(r.Versao, pedido.Versao);
        Exigir(r.Itens.Count == pedido.Itens.Count && r.Itens.Select(x => x.ItemId).Distinct().Count() == r.Itens.Count && r.Itens.All(i => pedido.Itens.Any(p => p.Id == i.ItemId)), "Confira todos os itens do atendimento.");
        var quantidades = r.Itens.ToDictionary(x => x.ItemId, x => Milesimos(x.Quantidade));
        foreach (var item in pedido.Itens) Exigir(quantidades[item.Id] <= item.QuantidadeMilesimos, "O atendimento não pode superar a quantidade pedida.");
        var cortado = pedido.Itens.Any(i => quantidades[i.Id] < i.QuantidadeMilesimos);
        Exigir(!cortado || Texto(r.Motivo).Length > 0, "Informe o motivo do corte.");
        var temAtendimento = quantidades.Values.Any(q => q > 0);
        Venda? venda = null;
        if (temAtendimento)
        {
            var condicao = await db.CondicoesPagamento.Include(x => x.Parcelas).SingleAsync(x => x.Id == pedido.CondicaoPagamentoId);
            if (!condicao.Ativo || condicao.Versao != pedido.CondicaoVersao) throw new ConflitoException("A condição do pedido mudou. Cancele e refaça o pedido com uma condição vigente.");
            Exigir(await db.Clientes.AnyAsync(x => x.Id == pedido.ClienteId && x.Ativo), "Cliente inativo. Revise o pedido.");
            venda = new() { Numero = (await db.Vendas.MaxAsync(x => (int?)x.Numero) ?? 0) + 1, PedidoId = pedido.Id, ClienteId = pedido.ClienteId, ClienteNome = pedido.ClienteNome, VendedorId = pedido.VendedorId, VendedorNome = pedido.VendedorNome, CondicaoNome = pedido.CondicaoNome, Data = Hoje, UsuarioId = usuarioId };
            db.Vendas.Add(venda);
            foreach (var item in pedido.Itens.Where(i => quantidades[i.Id] > 0))
            {
                var quantidade = quantidades[item.Id];
                Exigir(await db.Produtos.AnyAsync(x => x.Id == item.ProdutoId && x.Ativo && db.GruposProduto.Any(g => g.Id == x.GrupoId && g.Ativo)), "Produto ou grupo inativo. Corte o item ou reative o cadastro.");
                var total = Total(item.PrecoCentavos, quantidade); Exigir(total > 0, "Quantidade atendida gera valor inferior a um centavo.");
                venda.Itens.Add(new() { VendaId = venda.Id, ProdutoId = item.ProdutoId, ProdutoNome = item.ProdutoNome, Unidade = item.Unidade, QuantidadeMilesimos = quantidade, PrecoCentavos = item.PrecoCentavos, TotalCentavos = total });
                await estoque.Baixar(item.ProdutoId, quantidade, venda, usuarioId);
            }
            venda.TotalCentavos = venda.Itens.Sum(x => x.TotalCentavos);
            await financeiro.Gerar(venda, condicao);
        }
        foreach (var item in pedido.Itens) item.AtendidaMilesimos = quantidades[item.Id];
        pedido.Situacao = !temAtendimento ? SituacaoPedido.NaoAtendido : cortado ? SituacaoPedido.FaturadoParcialmente : SituacaoPedido.Faturado;
        pedido.Versao++; pedido.FaturamentoHash = hash; pedido.MotivoEncerramento = Texto(r.Motivo); pedido.ResponsavelId = usuarioId; pedido.EncerradoEm = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(); await tx.CommitAsync(); return new(await Dto(pedido), venda is null ? null : Dto(venda));
    }
    public async Task<PedidoDto> Cancelar(Guid id, CancelamentoEntrada r, Guid usuarioId)
    {
        Exigir(Texto(r.Motivo).Length > 0, "Informe o motivo do cancelamento.");
        var pedido = await db.Pedidos.Include(x => x.Itens).SingleOrDefaultAsync(x => x.Id == id) ?? throw new KeyNotFoundException();
        if (pedido.Situacao == SituacaoPedido.Cancelado) return await Dto(pedido);
        Versao(r.Versao, pedido.Versao); Exigir(pedido.Situacao == SituacaoPedido.Recebido, "Somente pedidos recebidos podem ser cancelados aqui.");
        pedido.Situacao = SituacaoPedido.Cancelado; pedido.Versao++; pedido.MotivoEncerramento = Texto(r.Motivo); pedido.ResponsavelId = usuarioId; pedido.EncerradoEm = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(); return await Dto(pedido);
    }
    public async Task<VendaDto> CancelarVenda(Guid id, CancelamentoEntrada r, Guid usuarioId)
    {
        Exigir(Texto(r.Motivo).Length > 0, "Informe o motivo do estorno.");
        await using var tx = await db.Database.BeginTransactionAsync();
        var venda = await db.Vendas.Include(x => x.Itens).SingleOrDefaultAsync(x => x.Id == id) ?? throw new KeyNotFoundException();
        if (venda.Cancelada) return Dto(venda);
        Versao(r.Versao, venda.Versao);
        Exigir(!venda.Entregue, "Venda entregue exige processo de devolução; não pode ser estornada neste módulo.");
        var titulos = await db.Titulos.Where(x => x.VendaId == id).ToListAsync();
        Exigir(titulos.All(x => !x.Pago), "Venda com recebimento não pode ser estornada.");
        foreach (var t in titulos) { t.Cancelado = true; t.Versao++; }
        foreach (var saida in await db.Movimentos.Where(x => x.VendaId == id && x.Tipo == "Venda").ToListAsync())
        {
            var lote = await db.Lotes.FindAsync(saida.LoteId); lote!.SaldoMilesimos -= saida.QuantidadeMilesimos; lote.Versao++;
            db.Movimentos.Add(new() { LoteId = lote.Id, ProdutoId = lote.ProdutoId, QuantidadeMilesimos = -saida.QuantidadeMilesimos, Tipo = "Estorno", VendaId = id, Motivo = Texto(r.Motivo), UsuarioId = usuarioId });
        }
        venda.Cancelada = true; venda.Versao++; venda.MotivoCancelamento = Texto(r.Motivo);
        var pedido = await db.Pedidos.FindAsync(venda.PedidoId); pedido!.Situacao = SituacaoPedido.Cancelado; pedido.Versao++;
        await db.SaveChangesAsync(); await tx.CommitAsync(); return Dto(venda);
    }
    public async Task<List<UltimaCompraDto>> UltimasCompras(Guid clienteId)
    {
        var vendas = await db.Vendas.AsNoTracking().Include(x => x.Itens).Where(x => x.ClienteId == clienteId && !x.Cancelada).ToListAsync();
        return vendas.OrderByDescending(x => x.Data).ThenByDescending(x => x.Numero).SelectMany(v => v.Itens.Select(i => new UltimaCompraDto(clienteId, i.ProdutoId, i.ProdutoNome, v.Data, i.QuantidadeMilesimos / 1000m, i.PrecoCentavos / 100m)))
            .GroupBy(x => x.ProdutoId).Select(x => x.First()).ToList();
    }
}

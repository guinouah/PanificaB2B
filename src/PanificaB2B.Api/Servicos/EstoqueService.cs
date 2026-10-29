using Microsoft.EntityFrameworkCore;
using PanificaB2B.Api.Dados;
using PanificaB2B.Api.Modelos;
using PanificaB2B.Contratos;
using static PanificaB2B.Api.Servicos.Valores;

namespace PanificaB2B.Api.Servicos;

public sealed class EstoqueService(DistribuidoraDbContext db)
{
    public async Task<List<LoteDto>> Lotes()
    {
        var produtos = await db.Produtos.AsNoTracking().ToDictionaryAsync(x => x.Id);
        return (await db.Lotes.AsNoTracking().OrderBy(x => x.Validade).ToListAsync()).Select(x => new LoteDto(x.Id, x.ProdutoId, produtos[x.ProdutoId].Nome, produtos[x.ProdutoId].Unidade, x.Codigo, x.Validade, x.SaldoMilesimos / 1000m, x.Versao)).ToList();
    }
    public async Task<List<MovimentoDto>> Movimentos()
    {
        var produtos = await db.Produtos.AsNoTracking().ToDictionaryAsync(x => x.Id);
        var lotes = await db.Lotes.AsNoTracking().ToDictionaryAsync(x => x.Id);
        return (await db.Movimentos.AsNoTracking().ToListAsync()).OrderByDescending(x => x.CriadoEm).Select(x => new MovimentoDto(x.Id, x.ProdutoId, produtos[x.ProdutoId].Nome, lotes[x.LoteId].Codigo, x.QuantidadeMilesimos / 1000m, x.Tipo, x.Motivo, x.CriadoEm)).ToList();
    }
    public async Task<LoteDto> Entrada(EntradaEstoque r, Guid usuarioId)
    {
        Exigir(r.Id != Guid.Empty, "Informe o identificador da entrada.");
        var quantidade = Milesimos(r.Quantidade); var codigo = Texto(r.CodigoLote).ToUpperInvariant();
        Exigir(quantidade > 0 && codigo.Length > 0 && r.Validade != default, "Informe quantidade, lote e validade.");
        await using var tx = await db.Database.BeginTransactionAsync();
        var repetido = await db.Movimentos.AsNoTracking().SingleOrDefaultAsync(x => x.Id == r.Id);
        if (repetido is not null)
        {
            var loteExistente = await db.Lotes.FindAsync(repetido.LoteId);
            if (repetido.Tipo != "Entrada" || repetido.ProdutoId != r.ProdutoId || repetido.QuantidadeMilesimos != quantidade || loteExistente!.Codigo != codigo || loteExistente.Validade != r.Validade)
                throw new ConflitoException("Identificador de entrada já utilizado com outros dados.");
            return (await Lotes()).Single(x => x.Id == repetido.LoteId);
        }
        Exigir(await db.Produtos.AnyAsync(x => x.Id == r.ProdutoId && x.Ativo), "Produto inexistente ou inativo.");
        var lote = await db.Lotes.SingleOrDefaultAsync(x => x.ProdutoId == r.ProdutoId && x.Codigo == codigo);
        if (lote is null) { lote = new() { ProdutoId = r.ProdutoId, Codigo = codigo, Validade = r.Validade }; db.Lotes.Add(lote); }
        else { Exigir(lote.Validade == r.Validade, "O lote já existe com outra validade."); lote.Versao++; }
        lote.SaldoMilesimos = checked(lote.SaldoMilesimos + quantidade);
        db.Movimentos.Add(new() { Id = r.Id, LoteId = lote.Id, ProdutoId = r.ProdutoId, QuantidadeMilesimos = quantidade, Tipo = "Entrada", UsuarioId = usuarioId });
        await db.SaveChangesAsync(); await tx.CommitAsync(); return (await Lotes()).Single(x => x.Id == lote.Id);
    }
    public async Task<LoteDto> Perda(PerdaEstoqueEntrada r, Guid usuarioId)
    {
        Exigir(r.Id != Guid.Empty && Texto(r.Motivo).Length > 0, "Identifique a operação e o motivo da perda.");
        var quantidade = Milesimos(r.Quantidade); Exigir(quantidade > 0, "Informe quantidade maior que zero.");
        await using var tx = await db.Database.BeginTransactionAsync();
        var repetido = await db.Movimentos.SingleOrDefaultAsync(x => x.Id == r.Id);
        if (repetido is not null)
        {
            if (repetido.Tipo != "Perda" || repetido.LoteId != r.LoteId || repetido.QuantidadeMilesimos != -quantidade || repetido.Motivo != Texto(r.Motivo)) throw new ConflitoException("Identificador de perda já utilizado.");
            return (await Lotes()).Single(x => x.Id == r.LoteId);
        }
        var lote = await db.Lotes.FindAsync(r.LoteId) ?? throw new KeyNotFoundException();
        Versao(r.Versao, lote.Versao); Exigir(lote.SaldoMilesimos >= quantidade, "A perda supera o saldo do lote.");
        lote.SaldoMilesimos -= quantidade; lote.Versao++;
        db.Movimentos.Add(new() { Id = r.Id, LoteId = lote.Id, ProdutoId = lote.ProdutoId, QuantidadeMilesimos = -quantidade, Tipo = "Perda", Motivo = Texto(r.Motivo), UsuarioId = usuarioId });
        await db.SaveChangesAsync(); await tx.CommitAsync(); return (await Lotes()).Single(x => x.Id == r.LoteId);
    }
    // Caller owns the transaction. Expired lots never participate; nearest expiry leaves first.
    public async Task Baixar(Guid produtoId, long quantidade, Venda venda, Guid usuarioId)
    {
        var hoje = Hoje;
        var lotes = await db.Lotes.Where(x => x.ProdutoId == produtoId && x.Validade >= hoje && x.SaldoMilesimos > 0).OrderBy(x => x.Validade).ThenBy(x => x.Codigo).ToListAsync();
        Exigir(lotes.Sum(x => x.SaldoMilesimos) >= quantidade, "Estoque válido insuficiente. Revise as quantidades atendidas.");
        foreach (var lote in lotes)
        {
            var retirar = Math.Min(quantidade, lote.SaldoMilesimos);
            if (retirar == 0) break;
            lote.SaldoMilesimos -= retirar; lote.Versao++; quantidade -= retirar;
            db.Movimentos.Add(new() { LoteId = lote.Id, ProdutoId = produtoId, QuantidadeMilesimos = -retirar, Tipo = "Venda", VendaId = venda.Id, UsuarioId = usuarioId });
        }
    }
}

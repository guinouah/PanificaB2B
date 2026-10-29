using Microsoft.EntityFrameworkCore;
using PanificaB2B.Api.Dados;
using PanificaB2B.Api.Modelos;
using PanificaB2B.Contratos;
using static PanificaB2B.Api.Servicos.Valores;

namespace PanificaB2B.Api.Servicos;

public sealed class FinanceiroService(DistribuidoraDbContext db)
{
    // Academic rules: calendar days, simple daily interest and one late penalty.
    // Early discount is limited to 10% of principal. This version settles a whole installment.
    public static TituloDto Dto(TituloReceber x, DateOnly data)
    {
        var aberto = !x.Pago && !x.Cancelado;
        var dias = data.DayNumber - x.Vencimento.DayNumber;
        var multa = aberto && dias > 0 ? Arredondar((decimal)x.ValorCentavos * x.MultaPontos / 10000) : 0;
        var juros = aberto && dias > 0 ? Arredondar((decimal)x.ValorCentavos * x.JurosDiariosPontos * dias / 10000) : 0;
        var desconto = aberto && dias < 0 ? Math.Min(Arredondar(x.ValorCentavos * .10m), Arredondar((decimal)x.ValorCentavos * x.DescontoDiarioPontos * -dias / 10000)) : 0;
        var principal = aberto ? x.ValorCentavos : 0;
        return new(x.Id, x.VendaId, x.ClienteId, x.ClienteNome, x.Identificacao, x.Vencimento, x.ValorCentavos / 100m, principal / 100m, multa / 100m, juros / 100m, desconto / 100m,
            (principal + multa + juros - desconto) / 100m, x.FormaPagamentoId, x.FormaNome, x.Pago, x.Cancelado, x.Versao);
    }
    public async Task<List<TituloDto>> Titulos(DateOnly data, Guid? clienteId = null, List<Guid>? carteira = null)
    {
        var query = db.Titulos.AsNoTracking().AsQueryable();
        if (clienteId.HasValue) query = query.Where(x => x.ClienteId == clienteId);
        if (carteira is not null) query = query.Where(x => carteira.Contains(x.ClienteId));
        return (await query.OrderBy(x => x.Vencimento).ThenBy(x => x.Identificacao).ToListAsync()).Select(x => Dto(x, data)).ToList();
    }
    public async Task Gerar(Venda venda, CondicaoPagamento condicao)
    {
        var parcelas = condicao.Parcelas.OrderBy(x => x.Numero).ToList();
        var formas = await db.FormasPagamento.ToDictionaryAsync(x => x.Id);
        Exigir(parcelas.Count > 0 && parcelas.Sum(x => x.PercentualPontos) == 10000 && parcelas.All(x => formas[x.FormaPagamentoId].Ativo), "A condição possui formas de pagamento inativas ou parcelas inválidas.");
        long distribuido = 0;
        foreach (var p in parcelas)
        {
            var valor = p == parcelas[^1] ? venda.TotalCentavos - distribuido : Arredondar((decimal)venda.TotalCentavos * p.PercentualPontos / 10000);
            Exigir(valor > 0, "O valor da venda é insuficiente para gerar todas as parcelas. Escolha outra condição.");
            distribuido += valor;
            db.Titulos.Add(new() { VendaId = venda.Id, ClienteId = venda.ClienteId, ClienteNome = venda.ClienteNome, Identificacao = $"{venda.Numero}/{p.Numero}", Parcela = p.Numero,
                Vencimento = venda.Data.AddDays(p.Dias), ValorCentavos = valor, FormaPagamentoId = p.FormaPagamentoId, FormaNome = formas[p.FormaPagamentoId].Nome,
                MultaPontos = condicao.MultaPontos, JurosDiariosPontos = condicao.JurosDiariosPontos, DescontoDiarioPontos = condicao.DescontoDiarioPontos });
        }
    }
    public static RecebimentoDto Dto(Recebimento x) => new(x.Id, x.TituloId, x.Data, x.ValorCentavos / 100m, x.PrincipalCentavos / 100m, x.MultaCentavos / 100m, x.JurosCentavos / 100m, x.DescontoCentavos / 100m, x.FormaNome);
    public async Task<RecebimentoDto> Receber(Guid tituloId, RecebimentoEntrada r, Guid usuarioId)
    {
        Exigir(r.Id != Guid.Empty && r.Data != default && r.Data <= Hoje, "Informe uma data de recebimento até hoje e um identificador válido.");
        var valor = Centavos(r.Valor);
        await using var tx = await db.Database.BeginTransactionAsync();
        var repetido = await db.Recebimentos.AsNoTracking().SingleOrDefaultAsync(x => x.Id == r.Id);
        if (repetido is not null)
        {
            if (repetido.TituloId != tituloId || repetido.Data != r.Data || repetido.ValorCentavos != valor || repetido.FormaPagamentoId != r.FormaPagamentoId) throw new ConflitoException("Identificador de recebimento já usado com outros dados.");
            return Dto(repetido);
        }
        var titulo = await db.Titulos.FindAsync(tituloId) ?? throw new KeyNotFoundException();
        Versao(r.Versao, titulo.Versao); Exigir(!titulo.Pago && !titulo.Cancelado, "Título já pago ou cancelado.");
        var venda = await db.Vendas.FindAsync(titulo.VendaId);
        Exigir(r.Data >= venda!.Data && !venda.Cancelada, "O recebimento não pode anteceder a venda nem liquidar uma venda cancelada.");
        var forma = await db.FormasPagamento.FindAsync(r.FormaPagamentoId);
        Exigir(forma is { Ativo: true }, "Selecione uma forma de pagamento ativa.");
        var calculo = Dto(titulo, r.Data);
        if (valor != Centavos(calculo.ValorQuitacao)) throw new ConflitoException("O valor de quitação mudou. Recalcule para a data selecionada antes de confirmar.");
        var recebimento = new Recebimento { Id = r.Id, TituloId = tituloId, Data = r.Data, ValorCentavos = valor, PrincipalCentavos = titulo.ValorCentavos,
            MultaCentavos = Centavos(calculo.Multa), JurosCentavos = Centavos(calculo.Juros), DescontoCentavos = Centavos(calculo.Desconto), FormaPagamentoId = r.FormaPagamentoId, FormaNome = forma!.Nome, UsuarioId = usuarioId };
        titulo.Pago = true; titulo.Versao++; db.Recebimentos.Add(recebimento);
        await db.SaveChangesAsync(); await tx.CommitAsync(); return Dto(recebimento);
    }
}

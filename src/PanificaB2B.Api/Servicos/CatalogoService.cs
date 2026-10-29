using Microsoft.EntityFrameworkCore;
using PanificaB2B.Api.Dados;
using PanificaB2B.Api.Modelos;
using PanificaB2B.Contratos;
using static PanificaB2B.Api.Servicos.Valores;

namespace PanificaB2B.Api.Servicos;

public sealed class CatalogoService(DistribuidoraDbContext db)
{
    public async Task<List<ProdutoDto>> Produtos(bool todos)
    {
        var grupos = await db.GruposProduto.AsNoTracking().ToDictionaryAsync(x => x.Id);
        return (await db.Produtos.AsNoTracking().OrderBy(x => x.Nome).ToListAsync())
            .Where(x => todos || x.Ativo && grupos[x.GrupoId].Ativo).Select(x => Dto(x, grupos[x.GrupoId].Nome)).ToList();
    }
    public static ProdutoDto Dto(Produto x, string grupo) => new(x.Id, x.Codigo, x.Nome, x.GrupoId, grupo, x.Unidade, x.PrecoCentavos / 100m,
        x.PromocaoCentavos / 100m, x.PromocaoInicio, x.PromocaoFim, x.NovidadeAte, x.Ativo, x.Versao);
    public async Task<ProdutoDto> SalvarProduto(Guid? id, ProdutoEntrada r)
    {
        var grupo = await db.GruposProduto.FindAsync(r.GrupoId);
        Exigir(grupo is { Ativo: true } || !r.Ativo && grupo is not null, "Selecione um grupo ativo.");
        var codigo = Texto(r.Codigo).ToUpperInvariant();
        Exigir(codigo.Length > 0 && Texto(r.Nome).Length > 0, "Informe código e nome.");
        Exigir(CatalogosCadastro.UnidadesVenda.Contains(Texto(r.Unidade).ToUpperInvariant()), "Selecione uma unidade de venda válida.");
        Exigir(!await db.Produtos.AnyAsync(x => x.Codigo == codigo && x.Id != id), "Código de produto já cadastrado.");
        var preco = Centavos(r.Preco); Exigir(preco > 0, "O preço deve ser maior que zero.");
        long? promocao = r.PrecoPromocional is { } p ? Centavos(p) : null;
        if (promocao is not null) Exigir(promocao > 0 && promocao < preco && r.PromocaoInicio is not null && r.PromocaoFim >= r.PromocaoInicio, "Informe preço promocional menor que o normal e um período válido.");
        else Exigir(r.PromocaoInicio is null && r.PromocaoFim is null, "Informe o preço promocional ou limpe o período.");
        var x = id is null ? new Produto() : await db.Produtos.FindAsync(id) ?? throw new KeyNotFoundException();
        if (id is not null) { Versao(r.Versao, x.Versao); x.Versao++; }
        x.Codigo = codigo; x.Nome = Texto(r.Nome); x.GrupoId = r.GrupoId; x.Unidade = Texto(r.Unidade).ToUpperInvariant();
        x.PrecoCentavos = preco; x.PromocaoCentavos = promocao; x.PromocaoInicio = r.PromocaoInicio; x.PromocaoFim = r.PromocaoFim; x.NovidadeAte = r.NovidadeAte; x.Ativo = r.Ativo;
        if (id is null) db.Produtos.Add(x);
        await db.SaveChangesAsync(); return Dto(x, grupo!.Nome);
    }
    public async Task<FormaPagamentoDto> SalvarForma(Guid? id, FormaPagamentoEntrada r)
    {
        var atual = id is null ? null : await db.FormasPagamento.FindAsync(id) ?? throw new KeyNotFoundException();
        var nome = CatalogosCadastro.Forma(r.Nome);
        // Permite desativar um cadastro antigo invalido, mantendo seu historico.
        if (nome is null && atual is not null && !r.Ativo && Texto(r.Nome) == atual.Nome)
        { atual.Ativo=false; await db.SaveChangesAsync(); return new(atual.Id,atual.Nome,false); }
        Exigir(nome is not null, "Selecione uma forma de pagamento válida na lista.");
        var normalizado = nome!.ToUpperInvariant();
        Exigir(nome.Length > 0 && !await db.FormasPagamento.AnyAsync(x => x.NomeNormalizado == normalizado && x.Id != id), "Informe um nome de forma de pagamento ainda não cadastrado.");
        var x = id is null ? new FormaPagamento() : await db.FormasPagamento.FindAsync(id) ?? throw new KeyNotFoundException();
        x.Nome = nome!; x.NomeNormalizado = normalizado; x.Ativo = r.Ativo;
        if (id is null) db.FormasPagamento.Add(x);
        await db.SaveChangesAsync(); return new(x.Id, x.Nome, x.Ativo);
    }
    public async Task<List<CondicaoPagamentoDto>> Condicoes(bool todos)
    {
        var formas = await db.FormasPagamento.AsNoTracking().ToDictionaryAsync(x => x.Id);
        return (await db.CondicoesPagamento.AsNoTracking().Include(x => x.Parcelas).OrderBy(x => x.Nome).ToListAsync())
            .Where(x => todos || x.Ativo && x.Parcelas.All(p => formas[p.FormaPagamentoId].Ativo && CatalogosCadastro.Forma(formas[p.FormaPagamentoId].Nome) is not null)).Select(x => Dto(x, formas)).ToList();
    }
    static CondicaoPagamentoDto Dto(CondicaoPagamento x, Dictionary<Guid, FormaPagamento> formas) => new(x.Id, x.Codigo, x.Nome, x.Ativo, x.Versao,
        x.MultaPontos / 100m, x.JurosDiariosPontos / 100m, x.DescontoDiarioPontos / 100m,
        x.Parcelas.OrderBy(p => p.Numero).Select(p => new ParcelaCondicaoDto(p.Numero, p.Dias, p.PercentualPontos / 100m, p.FormaPagamentoId, formas[p.FormaPagamentoId].Nome)).ToList());
    public async Task<CondicaoPagamentoDto> SalvarCondicao(Guid? id, CondicaoPagamentoEntrada r)
    {
        var codigo = Texto(r.Codigo).ToUpperInvariant();
        Exigir(codigo.Length > 0 && Texto(r.Nome).Length > 0, "Informe código e nome da condição.");
        Exigir(!await db.CondicoesPagamento.AnyAsync(x => x.Codigo == codigo && x.Id != id), "Código de condição já cadastrado.");
        Exigir(r.Parcelas.Count is >= 1 and <= 24 && r.Parcelas.All(p => p is not null && p.Dias is >= 0 and <= 3650 && p.Percentual > 0), "Informe entre 1 e 24 parcelas com prazo e percentual válidos.");
        Exigir(r.Parcelas.Sum(p => Pontos(p.Percentual)) == 10000, "Os percentuais das parcelas devem somar exatamente 100%.");
        var formas = await db.FormasPagamento.ToDictionaryAsync(x => x.Id);
        Exigir(r.Parcelas.All(p => formas.TryGetValue(p.FormaPagamentoId, out var f) && (f.Ativo || !r.Ativo) && CatalogosCadastro.Forma(f.Nome) is not null), "Selecione formas de pagamento ativas.");
        var x = id is null ? new CondicaoPagamento() : await db.CondicoesPagamento.Include(x => x.Parcelas).SingleOrDefaultAsync(x => x.Id == id) ?? throw new KeyNotFoundException();
        if (id is not null) Versao(r.Versao, x.Versao);
        x.Codigo = codigo; x.Nome = Texto(r.Nome); x.Ativo = r.Ativo; x.MultaPontos = Pontos(r.MultaPercentual); x.JurosDiariosPontos = Pontos(r.JurosDiariosPercentual); x.DescontoDiarioPontos = Pontos(r.DescontoDiarioPercentual);
        await using var tx = await db.Database.BeginTransactionAsync();
        if (id is null) db.CondicoesPagamento.Add(x);
        else { db.ParcelasCondicao.RemoveRange(x.Parcelas); await db.SaveChangesAsync(); x.Versao++; }
        x.Parcelas = r.Parcelas.Select((p, i) => new ParcelaCondicao { CondicaoId = x.Id, Numero = i + 1, Dias = p.Dias, PercentualPontos = Pontos(p.Percentual), FormaPagamentoId = p.FormaPagamentoId }).ToList();
        if (id is not null) db.ParcelasCondicao.AddRange(x.Parcelas);
        await db.SaveChangesAsync(); await tx.CommitAsync(); return Dto(x, formas);
    }
}

using Microsoft.EntityFrameworkCore;
using PanificaB2B.Api.Dados;
using PanificaB2B.Api.Modelos;
using PanificaB2B.Contratos;

namespace PanificaB2B.Api.Servicos;

public sealed class RegraNegocioException(string message) : Exception(message);
public sealed class ConflitoException(string message) : Exception(message);

public sealed class CadastrosService(DistribuidoraDbContext db)
{
    static void Exigir(bool valido, string mensagem) { if (!valido) throw new RegraNegocioException(mensagem); }
    static string Nome(string valor) { Exigir(!string.IsNullOrWhiteSpace(valor), "Informe o nome."); return valor.Trim(); }
    static void Versao(int recebida, int atual) { if (recebida != atual) throw new ConflitoException("Registro alterado. Atualize os dados antes de salvar."); }
    public static EstadoDto Dto(Estado x) => new(x.Id, x.Nome, x.Sigla, x.Ativo);
    public static CidadeDto Dto(Cidade x) => new(x.Id, x.Nome, x.EstadoId, x.Ativo);
    public static VendedorDto Dto(Vendedor x) => new(x.Id, x.Nome, x.Email, x.Ativo);
    public static TerritorioDto Dto(Territorio x) => new(x.Id, x.Nome, x.VendedorId, x.Cidades.Select(c => c.CidadeId).ToList(), x.Ativo, x.Versao);
    public static ClienteDto Dto(Cliente x) => new(x.Id, x.Nome, x.Documento, x.Email, x.Telefone, x.Endereco, x.CidadeId, x.TerritorioId, x.Ativo, x.Versao);
    public async Task<RoteiroDto> Dto(Roteiro x)
    {
        var ids = x.Visitas.Select(v => v.ClienteId).ToList();
        var nomes = await db.Clientes.Where(c => ids.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Nome);
        return new(x.Id, x.Nome, x.VendedorId, x.DiaSemana, x.Ativo, x.Versao, x.Visitas.OrderBy(v => v.Sequencia).Select(v => new VisitaDto(v.ClienteId, nomes[v.ClienteId], v.Sequencia)).ToList());
    }
    public async Task<EstadoDto> SalvarEstado(Guid? id, EstadoEntrada r)
    {
        var x = id is null ? new Estado() : await db.Estados.FindAsync(id) ?? throw new KeyNotFoundException();
        x.Nome = Nome(r.Nome); x.Sigla = r.Sigla.Trim().ToUpperInvariant(); x.Ativo = r.Ativo;
        Exigir(x.Sigla.Length == 2 && x.Sigla.All(char.IsLetter), "A sigla deve ter duas letras.");
        Exigir(!await db.Estados.AnyAsync(e => e.Sigla == x.Sigla && e.Id != x.Id), "Sigla já cadastrada.");
        if (id is null) db.Estados.Add(x);
        await db.SaveChangesAsync(); return Dto(x);
    }
    public async Task<CidadeDto> SalvarCidade(Guid? id, CidadeEntrada r)
    {
        Exigir(await db.Estados.AnyAsync(e => e.Id == r.EstadoId && (e.Ativo || !r.Ativo)), "Estado inexistente ou inativo.");
        var x = id is null ? new Cidade() : await db.Cidades.FindAsync(id) ?? throw new KeyNotFoundException();
        x.Nome = Nome(r.Nome); x.EstadoId = r.EstadoId; x.Ativo = r.Ativo;
        if (id is null) db.Cidades.Add(x);
        await db.SaveChangesAsync(); return Dto(x);
    }
    public async Task<VendedorDto> SalvarVendedor(Guid? id, VendedorEntrada r)
    {
        var x = id is null ? new Vendedor() : await db.Vendedores.FindAsync(id) ?? throw new KeyNotFoundException();
        x.Nome = Nome(r.Nome); x.Email = r.Email?.Trim() ?? ""; x.Ativo = r.Ativo;
        if (id is null) db.Vendedores.Add(x);
        await db.SaveChangesAsync(); return Dto(x);
    }
    public async Task<TerritorioDto> SalvarTerritorio(Guid? id, TerritorioEntrada r)
    {
        Exigir(await db.Vendedores.AnyAsync(v => v.Id == r.VendedorId && (v.Ativo || !r.Ativo)), "Vendedor inexistente ou inativo.");
        Exigir(r.CidadeIds.Count > 0 && r.CidadeIds.Distinct().Count() == r.CidadeIds.Count, "Informe cidades distintas.");
        Exigir(await db.Cidades.CountAsync(c => r.CidadeIds.Contains(c.Id) && (c.Ativo || !r.Ativo)) == r.CidadeIds.Count, "Cidade inexistente ou inativa.");
        var x = id is null ? new Territorio() : await db.Territorios.Include(t => t.Cidades).SingleOrDefaultAsync(t => t.Id == id) ?? throw new KeyNotFoundException();
        if (id is not null)
        {
            Versao(r.Versao, x.Versao);
            Exigir(!await db.Clientes.AnyAsync(c => c.TerritorioId == x.Id && !r.CidadeIds.Contains(c.CidadeId)), "Território possui clientes em uma cidade removida.");
            Exigir(x.VendedorId == r.VendedorId || !await db.ItensRoteiro.AnyAsync(i => db.Clientes.Any(c => c.Id == i.ClienteId && c.TerritorioId == x.Id) && db.Roteiros.Any(rt => rt.Id == i.RoteiroId && rt.Ativo)), "Reorganize os roteiros ativos antes de transferir o território.");
            x.Versao++;
        }
        x.Nome = Nome(r.Nome); x.VendedorId = r.VendedorId; x.Ativo = r.Ativo;
        // Preserve existing composite keys when updating membership.
        x.Cidades.RemoveAll(c => !r.CidadeIds.Contains(c.CidadeId));
        foreach (var cidade in r.CidadeIds.Where(c => !x.Cidades.Any(tc => tc.CidadeId == c))) x.Cidades.Add(new() { TerritorioId = x.Id, CidadeId = cidade });
        if (id is null) db.Territorios.Add(x);
        await db.SaveChangesAsync(); return Dto(x);
    }
    public async Task<ClienteDto> SalvarCliente(Guid? id, ClienteEntrada r)
    {
        var territorio = await db.Territorios.Include(t => t.Cidades).SingleOrDefaultAsync(t => t.Id == r.TerritorioId);
        Exigir(territorio is not null && (territorio.Ativo || !r.Ativo) && territorio.Cidades.Any(c => c.CidadeId == r.CidadeId), "Cidade não pertence ao território ativo informado.");
        Exigir(await db.Cidades.AnyAsync(c => c.Id == r.CidadeId && (c.Ativo || !r.Ativo)), "Cidade inativa ou inexistente.");
        var documento = string.Concat((r.Documento ?? "").Where(char.IsLetterOrDigit)).ToUpperInvariant();
        var chave = id ?? (r.Id == Guid.Empty ? Guid.NewGuid() : r.Id);
        var existente = await db.Clientes.SingleOrDefaultAsync(c => c.Id == chave);
        if (id is not null && existente is null) throw new KeyNotFoundException();
        Exigir(documento.Length == 0 || !await db.Clientes.AnyAsync(c => c.Documento == documento && c.Id != chave), "Documento já cadastrado.");
        var x = existente ?? new Cliente { Id = chave };
        if (id is null && existente is not null)
        {
            if (x.Nome != r.Nome.Trim() || x.Documento != documento || x.CidadeId != r.CidadeId || x.TerritorioId != r.TerritorioId || x.Email != (r.Email?.Trim() ?? "") || x.Telefone != (r.Telefone?.Trim() ?? "") || x.Endereco != r.Endereco.Trim() || x.Ativo != r.Ativo)
                throw new ConflitoException("Identificador já usado com dados diferentes.");
            return Dto(x);
        }
        if (id is not null)
        {
            Versao(r.Versao, x.Versao);
            Exigir(!await db.ItensRoteiro.AnyAsync(i => i.ClienteId == x.Id && db.Roteiros.Any(rt => rt.Id == i.RoteiroId && rt.Ativo && rt.VendedorId != territorio!.VendedorId)), "Reorganize os roteiros antes de transferir o cliente.");
            x.Versao++;
        }
        x.Nome = Nome(r.Nome); x.Documento = documento; x.Email = r.Email?.Trim() ?? ""; x.Telefone = r.Telefone?.Trim() ?? ""; x.Endereco = Nome(r.Endereco); x.CidadeId = r.CidadeId; x.TerritorioId = r.TerritorioId; x.Ativo = r.Ativo;
        if (existente is null) db.Clientes.Add(x);
        await db.SaveChangesAsync(); return Dto(x);
    }
    public async Task<RoteiroDto> SalvarRoteiro(Guid? id, RoteiroEntrada r)
    {
        Exigir(r.DiaSemana is >= 0 and <= 6, "Dia da semana inválido.");
        Exigir(await db.Vendedores.AnyAsync(v => v.Id == r.VendedorId && v.Ativo), "Vendedor inexistente ou inativo.");
        Exigir(r.Visitas.All(v => v is not null && v.Sequencia > 0) && r.Visitas.Select(v => v.Sequencia).Distinct().Count() == r.Visitas.Count, "Sequências devem ser positivas e não podem se repetir no roteiro.");
        var ids = r.Visitas.Select(v => v.ClienteId).ToList();
        Exigir(ids.Distinct().Count() == ids.Count, "Cliente repetido no roteiro.");
        var carteira = await db.Clientes.Where(c => c.Ativo && ids.Contains(c.Id) && db.Territorios.Any(t => t.Id == c.TerritorioId && t.Ativo && t.VendedorId == r.VendedorId)).CountAsync();
        Exigir(carteira == ids.Count, "Um ou mais clientes não pertencem à carteira ativa do vendedor.");
        var x = id is null ? new Roteiro() : await db.Roteiros.Include(rt => rt.Visitas).SingleOrDefaultAsync(rt => rt.Id == id) ?? throw new KeyNotFoundException();
        if (id is not null) { Versao(r.Versao, x.Versao); x.Versao++; }
        x.Nome = Nome(r.Nome); x.VendedorId = r.VendedorId; x.DiaSemana = r.DiaSemana; x.Ativo = r.Ativo;
        // Delete and recreate the ordered membership atomically; swaps cannot violate unique positions.
        await using var transaction = await db.Database.BeginTransactionAsync();
        if (id is not null) { db.ItensRoteiro.RemoveRange(x.Visitas); await db.SaveChangesAsync(); }
        x.Visitas = r.Visitas.Select(v => new ItemRoteiro { RoteiroId = x.Id, ClienteId = v.ClienteId, Sequencia = v.Sequencia }).ToList();
        if (id is null) db.Roteiros.Add(x);
        else db.ItensRoteiro.AddRange(x.Visitas);
        await db.SaveChangesAsync(); await transaction.CommitAsync(); return await Dto(x);
    }
}

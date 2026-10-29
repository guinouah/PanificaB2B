using System.Text.Json;
using SQLite;
using PanificaB2B.Contratos;

namespace PanificaB2B.App.Dados;

[Table("CarteiraBaixada")]
public class CarteiraBaixada
{
    [PrimaryKey] public string VendedorId { get; set; } = "";
    public string ConteudoJson { get; set; } = "";
    public string AtualizadoEm { get; set; } = "";
}

// Official download cache. TrabalhoLocal keeps the customer's work in a separate table.
public sealed class CarteiraLocal(string caminhoBanco)
{
    readonly SQLiteAsyncConnection banco = new(caminhoBanco);
    public async Task Salvar(PreparacaoDto dados)
    {
        await banco.CreateTableAsync<CarteiraBaixada>();
        await banco.InsertOrReplaceAsync(new CarteiraBaixada { VendedorId = dados.VendedorId.ToString(), ConteudoJson = JsonSerializer.Serialize(dados), AtualizadoEm = dados.AtualizadoEm.ToString("O") });
    }
    public async Task<PreparacaoDto?> CarregarUltima()
    {
        await banco.CreateTableAsync<CarteiraBaixada>();
        var item = await banco.Table<CarteiraBaixada>().OrderByDescending(x => x.AtualizadoEm).FirstOrDefaultAsync();
        return item is null ? null : JsonSerializer.Deserialize<PreparacaoDto>(item.ConteudoJson);
    }
    public async Task<PreparacaoDto?> Carregar(Guid vendedorId)
    {
        await banco.CreateTableAsync<CarteiraBaixada>();
        var item = await banco.FindAsync<CarteiraBaixada>(vendedorId.ToString());
        return item is null ? null : JsonSerializer.Deserialize<PreparacaoDto>(item.ConteudoJson);
    }
}

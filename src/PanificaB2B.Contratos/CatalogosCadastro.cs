using System.Globalization;
using System.Text;
using System.Text.Json;

namespace PanificaB2B.Contratos;

public record MunicipioBrasil(int Id, string Nome);
public record EstadoBrasil(string Sigla, string Nome, List<MunicipioBrasil> Cidades);
public static class LocalidadesBrasil
{
    // Snapshot oficial do IBGE em 28/10/2026. Disponivel sem internet.
    // https://servicodados.ibge.gov.br/api/docs/localidades
    static readonly Lazy<IReadOnlyList<EstadoBrasil>> dados = new(() =>
    {
        using var stream = typeof(LocalidadesBrasil).Assembly.GetManifestResourceStream("PanificaB2B.Contratos.Dados.localidades-brasil.json")!;
        return JsonSerializer.Deserialize<List<EstadoBrasil>>(stream)!.AsReadOnly();
    });
    public static IReadOnlyList<EstadoBrasil> Estados => dados.Value;
    public static string Normalizar(string? valor) => string.Concat((valor ?? "").Trim().Normalize(NormalizationForm.FormD).Where(c=>CharUnicodeInfo.GetUnicodeCategory(c)!=UnicodeCategory.NonSpacingMark)).ToUpperInvariant();
    public static EstadoBrasil? Estado(string? sigla) => Estados.FirstOrDefault(x=>x.Sigla==Normalizar(sigla));
    public static MunicipioBrasil? Cidade(string? sigla,string? nome) => Estado(sigla)?.Cidades.FirstOrDefault(x=>Normalizar(x.Nome)==Normalizar(nome));
}
public static class CatalogosCadastro
{
    public static IReadOnlyList<string> FormasPagamento { get; } = Array.AsReadOnly(new[] { "Pix", "Boleto", "Dinheiro", "Cartão de débito", "Cartão de crédito", "Transferência bancária", "Cheque" });
    public static IReadOnlyList<string> UnidadesVenda { get; } = Array.AsReadOnly(new[] { "UN", "KG", "G", "L", "ML", "CX", "PCT", "SACO", "BALDE" });
    public static string? Forma(string? nome)
    {
        var n=LocalidadesBrasil.Normalizar(nome);
        if(n=="BOLETO BANCARIO") n="BOLETO";
        return FormasPagamento.FirstOrDefault(x=>LocalidadesBrasil.Normalizar(x)==n);
    }
}

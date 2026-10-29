namespace PanificaB2B.Api.Servicos;

public static class Valores
{
    public static void Exigir(bool condicao, string mensagem) { if (!condicao) throw new RegraNegocioException(mensagem); }
    public static string Texto(string? valor) => (valor ?? "").Trim();
    public static long Centavos(decimal valor) { Exigir(valor >= 0 && valor <= 1000000000 && decimal.Round(valor, 2) == valor, "Use um valor positivo com até duas casas decimais."); return checked((long)(valor * 100)); }
    public static long Milesimos(decimal valor) { Exigir(valor >= 0 && valor <= 1000000 && decimal.Round(valor, 3) == valor, "Use quantidade com até três casas decimais."); return checked((long)(valor * 1000)); }
    public static int Pontos(decimal valor) { Exigir(valor is >= 0 and <= 100 && decimal.Round(valor, 2) == valor, "Percentuais devem ter até duas casas decimais, entre 0 e 100."); return (int)(valor * 100); }
    public static long Arredondar(decimal valor) => checked((long)decimal.Round(valor, 0, MidpointRounding.AwayFromZero));
    public static long Total(long precoCentavos, long quantidadeMilesimos) => Arredondar((decimal)precoCentavos * quantidadeMilesimos / 1000m);
    public static DateOnly Hoje => DateOnly.FromDateTime(DateTime.Today);
    public static void Versao(int recebida, int atual) { if (recebida != atual) throw new ConflitoException("Registro alterado. Recarregue os dados antes de confirmar."); }
}

using System.ComponentModel.DataAnnotations;

namespace PanificaB2B.Contratos;

public sealed class IntervaloDecimalAttribute : RangeAttribute
{
    public IntervaloDecimalAttribute(string minimo, string maximo) : base(typeof(decimal), minimo, maximo)
    {
        ParseLimitsInInvariantCulture = true;
        ConvertValueInInvariantCulture = true;
        ErrorMessage = "Informe {0} entre {1} e {2}.";
    }
}

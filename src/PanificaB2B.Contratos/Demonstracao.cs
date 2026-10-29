using System.ComponentModel.DataAnnotations;
namespace PanificaB2B.Contratos;
public sealed class DemonstracaoEntrada
{
    [Required,MinLength(12),MaxLength(100),DataType(DataType.Password)]
    public string SenhaAcessos { get; set; } = "";
}
public record DemonstracaoDto(bool JaExistia,string Mensagem);

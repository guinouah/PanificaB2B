using System.ComponentModel.DataAnnotations;

namespace PanificaB2B.Contratos;

public record GrupoProdutoDto(Guid Id, string Nome, bool Ativo, int Versao);

public sealed class GrupoProdutoEntrada
{
    [Required(ErrorMessage = "Informe o nome do grupo."), StringLength(100, ErrorMessage = "Use até 100 caracteres.")]
    public string Nome { get; set; } = "";
    public bool Ativo { get; set; } = true;
    [Range(0, int.MaxValue)] public int Versao { get; set; }
}

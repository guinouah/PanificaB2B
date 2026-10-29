using System.ComponentModel.DataAnnotations;

namespace PanificaB2B.Contratos;

public class CredenciaisEntrada
{
    [Required(ErrorMessage = "Informe o login."), StringLength(60, MinimumLength = 3, ErrorMessage = "O login deve ter entre 3 e 60 caracteres."), RegularExpression(@"^[a-zA-Z0-9._-]+$", ErrorMessage = "Use somente letras sem acento, números, ponto, hífen e sublinhado no login, sem espaços.")]
    public string Login { get; set; } = "";
    [Required(ErrorMessage = "Informe a senha."), StringLength(128, MinimumLength = 10, ErrorMessage = "A senha deve ter entre 10 e 128 caracteres."), DataType(DataType.Password)]
    public string Senha { get; set; } = "";
}
public class NovoUsuarioEntrada : CredenciaisEntrada
{
    [EnumDataType(typeof(PerfilUsuario))] public PerfilUsuario Perfil { get; set; }
    public Guid? VendedorId { get; set; }
}
public record UsuarioDto(Guid Id, string Login, PerfilUsuario Perfil, Guid? VendedorId);
public record TokenResposta(string TokenType, string AccessToken, long ExpiresIn);
public record ConfiguracaoAcessoDto(bool Configurado);

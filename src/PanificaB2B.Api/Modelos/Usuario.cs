using PanificaB2B.Contratos;

namespace PanificaB2B.Api.Modelos;

public sealed class Usuario
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Login { get; set; } = "";
    public string LoginNormalizado { get; set; } = "";
    public string SenhaHash { get; set; } = "";
    public PerfilUsuario Perfil { get; set; }
    public Guid? VendedorId { get; set; }
    public bool Ativo { get; set; } = true;
}
public sealed class SessaoAcesso
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UsuarioId { get; set; }
    public DateTimeOffset ExpiraEm { get; set; }
    public bool Ativa { get; set; } = true;
}

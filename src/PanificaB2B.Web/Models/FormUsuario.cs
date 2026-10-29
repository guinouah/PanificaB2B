using PanificaB2B.Contratos;
namespace PanificaB2B.Web.Models;
public sealed class FormUsuario
{
    public NovoUsuarioEntrada Entrada { get; set; } = new() { Perfil = PerfilUsuario.Vendedor };
    public List<UsuarioDto> Usuarios { get; set; } = [];
    public List<VendedorDto> Vendedores { get; set; } = [];
}

using PanificaB2B.Contratos;

namespace PanificaB2B.Api.Seguranca;

// Identity comes from a validated token, never request-body profile fields.
public sealed record IdentidadeUsuario(Guid UsuarioId, PerfilUsuario Perfil, Guid? VendedorId);

public static class AcessoUsuario
{
    public static bool PodeAdministrar(IdentidadeUsuario usuario) =>
        usuario.UsuarioId != Guid.Empty && usuario.Perfil == PerfilUsuario.Administrador;

    public static bool PodeComercial(IdentidadeUsuario usuario) => usuario.UsuarioId != Guid.Empty && Permissoes.PodeComercial(usuario.Perfil);

    public static bool PodeAcessarCarteira(IdentidadeUsuario usuario, Guid vendedorDaCarteira) =>
        usuario.UsuarioId != Guid.Empty && vendedorDaCarteira != Guid.Empty &&
        (PodeComercial(usuario) ||
         usuario.Perfil == PerfilUsuario.Vendedor && usuario.VendedorId == vendedorDaCarteira);
}

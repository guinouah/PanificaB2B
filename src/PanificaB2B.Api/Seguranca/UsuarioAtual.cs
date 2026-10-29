using System.Security.Claims;
using PanificaB2B.Contratos;

namespace PanificaB2B.Api.Seguranca;

public static class UsuarioAtual
{
    public static IdentidadeUsuario Identidade(this ClaimsPrincipal principal)
    {
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id);
        Enum.TryParse<PerfilUsuario>(principal.FindFirstValue(ClaimTypes.Role), out var perfil);
        Guid? vendedor = Guid.TryParse(principal.FindFirstValue("vendedorId"), out var vendedorId) ? vendedorId : null;
        return new(id, perfil, vendedor);
    }
}

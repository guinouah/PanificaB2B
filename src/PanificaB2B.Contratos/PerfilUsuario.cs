using System.ComponentModel.DataAnnotations;
namespace PanificaB2B.Contratos;
public enum PerfilUsuario
{
    Administrador = 1,
    Vendedor = 2,
    [Display(Name = "Gestor Comercial")] GestorComercial = 3,
    Financeiro = 4,
    [Display(Name = "Estoque / Expedição")] EstoqueExpedicao = 5
}

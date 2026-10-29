namespace PanificaB2B.Contratos;
public static class Permissoes
{
    public const string Comercial = "Administrador,GestorComercial";
    public const string Financeiro = "Administrador,Financeiro";
    public const string Estoque = "Administrador,EstoqueExpedicao";
    public const string ConsultaEstoque = "Administrador,GestorComercial,EstoqueExpedicao";
    public const string ComercialFinanceiro = "Administrador,GestorComercial,Financeiro";
    public const string ComercialVendedor = "Administrador,GestorComercial,Vendedor";
    public const string Equipe = "Administrador,GestorComercial,Financeiro,EstoqueExpedicao";
    public static bool PodeUsarSite(PerfilUsuario p) => p is PerfilUsuario.Administrador or PerfilUsuario.GestorComercial or PerfilUsuario.Financeiro or PerfilUsuario.EstoqueExpedicao;
    public static bool PodeComercial(PerfilUsuario p) => p is PerfilUsuario.Administrador or PerfilUsuario.GestorComercial;
    public static bool PodeFinanceiro(PerfilUsuario p) => p is PerfilUsuario.Administrador or PerfilUsuario.Financeiro;
    public static bool PodeEstoque(PerfilUsuario p) => p is PerfilUsuario.Administrador or PerfilUsuario.EstoqueExpedicao;
}

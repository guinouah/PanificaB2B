using PanificaB2B.Contratos;
namespace PanificaB2B.App.Dados;
public record SessaoDispositivo(UsuarioDto Usuario, string ApiUrl, string Token, DateTimeOffset ExpiraEm);

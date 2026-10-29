using Microsoft.AspNetCore.Identity;
using PanificaB2B.Api.Modelos;

namespace PanificaB2B.Api.Seguranca;

public sealed class SenhasService
{
    readonly PasswordHasher<Usuario> hasher = new();
    readonly Usuario ficticio = new();
    readonly string hashFicticio;
    public SenhasService() => hashFicticio = hasher.HashPassword(ficticio, Guid.NewGuid().ToString());
    public string Gerar(Usuario usuario, string senha) => hasher.HashPassword(usuario, senha);
    public bool Conferir(Usuario? usuario, string senha) =>
        hasher.VerifyHashedPassword(usuario ?? ficticio, usuario?.SenhaHash ?? hashFicticio, senha) != PasswordVerificationResult.Failed && usuario is not null;
}

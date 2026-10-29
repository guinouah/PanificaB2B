using Microsoft.EntityFrameworkCore;

namespace PanificaB2B.Api.Dados;

public static class AtualizacaoAcesso
{
    // Small, additive upgrade for databases created by the first academic slice.
    // It never recreates or deletes existing customer/route tables.
    public static async Task Aplicar(DistribuidoraDbContext db)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "Usuarios" (
                "Id" TEXT NOT NULL PRIMARY KEY,
                "Login" TEXT NOT NULL,
                "LoginNormalizado" TEXT NOT NULL,
                "SenhaHash" TEXT NOT NULL,
                "Perfil" INTEGER NOT NULL,
                "VendedorId" TEXT NULL REFERENCES "Vendedores" ("Id") ON DELETE RESTRICT,
                "Ativo" INTEGER NOT NULL,
                CONSTRAINT "CK_Usuario_Perfil" CHECK ((Perfil IN (1, 3, 4, 5) AND VendedorId IS NULL) OR (Perfil = 2 AND VendedorId IS NOT NULL))
            );
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Usuarios_LoginNormalizado" ON "Usuarios" ("LoginNormalizado");
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Usuarios_VendedorId" ON "Usuarios" ("VendedorId") WHERE "VendedorId" IS NOT NULL;
            CREATE TABLE IF NOT EXISTS "SessoesAcesso" (
                "Id" TEXT NOT NULL PRIMARY KEY,
                "UsuarioId" TEXT NOT NULL REFERENCES "Usuarios" ("Id") ON DELETE RESTRICT,
                "ExpiraEm" TEXT NOT NULL,
                "Ativa" INTEGER NOT NULL
            );
            CREATE INDEX IF NOT EXISTS "IX_SessoesAcesso_UsuarioId" ON "SessoesAcesso" ("UsuarioId");
            """);
        await tx.CommitAsync();
    }
}

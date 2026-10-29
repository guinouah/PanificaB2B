using System.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
namespace PanificaB2B.Api.Dados;
public static class AtualizacaoPerfis
{
    // Preserve ids, hashes and references: SQLite cannot alter a CHECK constraint.
    public static async Task Aplicar(DistribuidoraDbContext db)
    {
        var conexao = (SqliteConnection)db.Database.GetDbConnection();
        var abriu = conexao.State != ConnectionState.Open;
        if (abriu) await db.Database.OpenConnectionAsync();
        try
        {
            using var consulta = conexao.CreateCommand();
            consulta.CommandText = "SELECT sql FROM sqlite_master WHERE type='table' AND name='Usuarios'";
            var esquema = (string?)await consulta.ExecuteScalarAsync() ?? "";
            if (esquema.Contains("Perfil IN (1, 3, 4, 5)", StringComparison.OrdinalIgnoreCase)) return;
            if (!esquema.Contains("Perfil = 1 AND VendedorId IS NULL", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Esquema de usuários desconhecido. Banco mantido para revisão.");
            var origem = new SqliteConnectionStringBuilder(conexao.ConnectionString).DataSource;
            var pasta = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(origem))!, "Backups");
            Directory.CreateDirectory(pasta);
            using (var backup = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = Path.Combine(pasta, $"antes-perfis-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6]}.db") }.ToString()))
            {
                await backup.OpenAsync(); conexao.BackupDatabase(backup);
            }
            // Only this connection; restore before returning it to the pool.
            await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=OFF;");
            try
            {
                using (var verificar = conexao.CreateCommand())
                {
                    verificar.CommandText = "PRAGMA foreign_keys";
                    if (Convert.ToInt64(await verificar.ExecuteScalarAsync()) != 0) throw new InvalidOperationException("Não foi possível iniciar a atualização dos perfis com segurança.");
                }
                await using var tx = await db.Database.BeginTransactionAsync();
                await db.Database.ExecuteSqlRawAsync("""
                    CREATE TABLE "Usuarios_PerfisV2" (
                        "Id" TEXT NOT NULL PRIMARY KEY,
                        "Login" TEXT NOT NULL,
                        "LoginNormalizado" TEXT NOT NULL,
                        "SenhaHash" TEXT NOT NULL,
                        "Perfil" INTEGER NOT NULL,
                        "VendedorId" TEXT NULL REFERENCES "Vendedores" ("Id") ON DELETE RESTRICT,
                        "Ativo" INTEGER NOT NULL,
                        CONSTRAINT "CK_Usuario_Perfil" CHECK ((Perfil IN (1, 3, 4, 5) AND VendedorId IS NULL) OR (Perfil = 2 AND VendedorId IS NOT NULL))
                    );
                    INSERT INTO "Usuarios_PerfisV2" ("Id", "Login", "LoginNormalizado", "SenhaHash", "Perfil", "VendedorId", "Ativo")
                        SELECT "Id", "Login", "LoginNormalizado", "SenhaHash", "Perfil", "VendedorId", "Ativo" FROM "Usuarios";
                    DROP TABLE "Usuarios";
                    ALTER TABLE "Usuarios_PerfisV2" RENAME TO "Usuarios";
                    CREATE UNIQUE INDEX "IX_Usuarios_LoginNormalizado" ON "Usuarios" ("LoginNormalizado");
                    CREATE UNIQUE INDEX "IX_Usuarios_VendedorId" ON "Usuarios" ("VendedorId") WHERE "VendedorId" IS NOT NULL;
                    """);
                using var conferir = conexao.CreateCommand();
                conferir.Transaction = (SqliteTransaction)tx.GetDbTransaction();
                conferir.CommandText = "PRAGMA foreign_key_check";
                using (var linhas = await conferir.ExecuteReaderAsync())
                    if (await linhas.ReadAsync()) throw new InvalidOperationException("Vínculos inconsistentes. Atualização dos perfis desfeita.");
                await tx.CommitAsync();
            }
            finally { await db.Database.ExecuteSqlRawAsync("PRAGMA foreign_keys=ON;"); }
        }
        finally { if (abriu) await db.Database.CloseConnectionAsync(); }
    }
}

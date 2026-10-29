using Microsoft.EntityFrameworkCore;

namespace PanificaB2B.Api.Dados;

public static class AtualizacaoGrupos
{
    // Add only this slice's table; keep existing registrations and accounts intact.
    public static Task Aplicar(DistribuidoraDbContext db) => db.Database.ExecuteSqlRawAsync("""
        CREATE TABLE IF NOT EXISTS "GruposProduto" (
            "Id" TEXT NOT NULL PRIMARY KEY,
            "Nome" TEXT NOT NULL,
            "Ativo" INTEGER NOT NULL,
            "NomeNormalizado" TEXT NOT NULL,
            "Versao" INTEGER NOT NULL
        );
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_GruposProduto_NomeNormalizado"
            ON "GruposProduto" ("NomeNormalizado");
        """);
}

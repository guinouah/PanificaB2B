using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;

namespace PanificaB2B.Api.Dados;

public static class AtualizacaoComercial
{
    public static async Task BackupSeNecessario(DistribuidoraDbContext db)
    {
        var configuracao = new SqliteConnectionStringBuilder(db.Database.GetConnectionString());
        if (string.IsNullOrWhiteSpace(configuracao.DataSource) || configuracao.DataSource == ":memory:" || !File.Exists(configuracao.DataSource)) return;
        using var origem = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = configuracao.DataSource, Mode = SqliteOpenMode.ReadOnly }.ToString());
        await origem.OpenAsync();
        using var comando = origem.CreateCommand();
        comando.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Produtos'";
        if (Convert.ToInt64(await comando.ExecuteScalarAsync()) != 0) return;
        var pasta = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(configuracao.DataSource))!, "Backups");
        Directory.CreateDirectory(pasta);
        var nome = Path.Combine(pasta, $"antes-comercial-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.db");
        using var destino = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = nome }.ToString());
        await destino.OpenAsync(); origem.BackupDatabase(destino);
    }
    // Additive academic upgrade: old tables/rows remain untouched. The SQL is generated from this model.
    public static async Task Aplicar(DistribuidoraDbContext db)
    {
        var sql = db.Database.GenerateCreateScript()
            .Replace("CREATE TABLE ", "CREATE TABLE IF NOT EXISTS ", StringComparison.Ordinal)
            .Replace("CREATE UNIQUE INDEX ", "CREATE UNIQUE INDEX IF NOT EXISTS ", StringComparison.Ordinal)
            .Replace("CREATE INDEX ", "CREATE INDEX IF NOT EXISTS ", StringComparison.Ordinal);
        await db.Database.ExecuteSqlRawAsync(sql);
    }
}

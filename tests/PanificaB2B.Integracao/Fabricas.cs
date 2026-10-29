using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using PanificaB2B.Api.Dados;

namespace PanificaB2B.Integracao;

public sealed class FabricaApi(string raiz, string pastaTeste) : WebApplicationFactory<PanificaB2B.Api.Modelos.Usuario>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseContentRoot(Path.Combine(raiz, "src", "PanificaB2B.Api"));
        builder.ConfigureLogging(x => x.ClearProviders());
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<DistribuidoraDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<DistribuidoraDbContext>>();
            services.AddDbContext<DistribuidoraDbContext>(o => o.UseSqlite($"Data Source={Path.Combine(pastaTeste, "banco.db")};Foreign Keys=True"));
            services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(pastaTeste, "chaves-api")));
        });
    }
}
public sealed class FabricaSite(string raiz, string pastaTeste, FabricaApi api) : WebApplicationFactory<PanificaB2B.Web.Models.FormUsuario>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseContentRoot(Path.Combine(raiz, "src", "PanificaB2B.Web"));
        builder.ConfigureLogging(x => x.ClearProviders());
        builder.ConfigureServices(services =>
        {
            services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(pastaTeste, "chaves-site")));
            foreach (var nome in new[] { "Acesso", "Distribuidora" })
                services.AddHttpClient(nome).ConfigurePrimaryHttpMessageHandler(() => api.Server.CreateHandler());
        });
    }
}

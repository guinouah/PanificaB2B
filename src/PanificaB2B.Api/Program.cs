using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PanificaB2B.Api.Dados;
using PanificaB2B.Api.Servicos;
using PanificaB2B.Api.Seguranca;
using PanificaB2B.Contratos;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders().AddConsole();
var localData = Path.Combine(builder.Environment.ContentRootPath, "App_Data");
Directory.CreateDirectory(localData);
var databasePath = builder.Configuration["DatabasePath"] ?? Path.Combine(localData, "distribuidora.db");
builder.Services.AddDbContext<DistribuidoraDbContext>(o => o.UseSqlite($"Data Source={databasePath};Foreign Keys=True"));
builder.Services.AddScoped<CadastrosService>();
builder.Services.AddScoped<CatalogoService>();
builder.Services.AddScoped<EstoqueService>();
builder.Services.AddScoped<FinanceiroService>();
builder.Services.AddScoped<PedidosService>();
builder.Services.AddScoped<DemonstracaoService>();
builder.Services.AddSingleton<SenhasService>();
builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(localData, "ChavesAcesso"))).SetApplicationName("PanificaB2B.Api");
builder.Services.AddAuthentication(BearerTokenDefaults.AuthenticationScheme).AddBearerToken(o => o.BearerTokenExpiration = TimeSpan.FromMinutes(30));
builder.Services.AddAuthorization(o => o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new() { PermitLimit = 15, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
builder.Services.AddControllers();
var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DistribuidoraDbContext>();
    await AtualizacaoComercial.BackupSeNecessario(db);
    // Initial academic slice. Replace EnsureCreated with migrations before evolving a real database.
    await db.Database.EnsureCreatedAsync();
    await AtualizacaoAcesso.Aplicar(db);
    await AtualizacaoPerfis.Aplicar(db);
    await AtualizacaoGrupos.Aplicar(db);
    await AtualizacaoComercial.Aplicar(db);
    if (builder.Configuration.GetValue<bool>("DadosExemplo")) await DadosExemplo.Criar(db);
}
app.Use(async (context, next) =>
{
    // Keep this academic slice local; no change to system certificates or network settings.
    if (context.Connection.RemoteIpAddress is { } ip && !IPAddress.IsLoopback(ip)) { context.Response.StatusCode = 403; return; }
    if (context.Request.Headers.Origin.FirstOrDefault() is { } origin && (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) || uri.Authority != context.Request.Host.Value))
    { context.Response.StatusCode = 403; return; }
    try { await next(); }
    catch (Exception ex) when (ex is RegraNegocioException or ConflitoException or KeyNotFoundException or DbUpdateException)
    {
        if (ex is DbUpdateException) app.Logger.LogWarning(ex, "Conflito durante a gravação dos cadastros.");
        var status = ex switch { RegraNegocioException => 400, KeyNotFoundException => 404, _ => 409 };
        var message = ex is DbUpdateException ? "Conflito de dados. Atualize e confira os vínculos." : ex is KeyNotFoundException ? "Registro não encontrado." : ex.Message;
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new { mensagem = message });
    }
    catch (SqliteException ex) when (ex.SqliteErrorCode is 5 or 6)
    {
        context.Response.StatusCode = 409;
        await context.Response.WriteAsJsonAsync(new { mensagem = "Outra gravação está em andamento. Tente novamente." });
    }
});
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true)
    {
        var identidade = context.User.Identidade();
        var db = context.RequestServices.GetRequiredService<DistribuidoraDbContext>();
        var usuario = await db.Usuarios.AsNoTracking().SingleOrDefaultAsync(x => x.Id == identidade.UsuarioId);
        var sessaoValida = Guid.TryParse(context.User.FindFirstValue("sessaoId"), out var sessaoId)
            ? await db.SessoesAcesso.AsNoTracking().SingleOrDefaultAsync(x => x.Id == sessaoId && x.UsuarioId == identidade.UsuarioId) : null;
        if (usuario is not { Ativo: true } || !Enum.IsDefined(usuario.Perfil) || usuario.Perfil != identidade.Perfil || usuario.VendedorId != identidade.VendedorId ||
            sessaoValida is not { Ativa: true } || sessaoValida.ExpiraEm <= DateTimeOffset.UtcNow ||
            usuario.Perfil == PerfilUsuario.Vendedor && !await db.Vendedores.AnyAsync(v => v.Id == usuario.VendedorId && v.Ativo))
        { context.Response.StatusCode = 401; return; }
    }
    await next();
});
app.UseAuthorization();
app.MapGet("/api/saude", () => new { estado = "ok", banco = "SQLite", etapa = "Cadastros, pedidos, estoque, vendas e financeiro" }).AllowAnonymous();
app.MapControllers();
app.Run();

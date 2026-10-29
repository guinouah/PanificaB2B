using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using PanificaB2B.Web.Seguranca;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders().AddConsole();

// Add services to the container.
builder.Services.AddControllersWithViews(o => o.ModelBinderProviders.Insert(0, new PanificaB2B.Web.Models.DecimalFormularioBinder()));
builder.Services.Configure<RequestLocalizationOptions>(o => o.SetDefaultCulture("pt-BR").AddSupportedCultures("pt-BR").AddSupportedUICultures("pt-BR"));
// This local demo does not read/write the machine-wide ASP.NET key directory.
// Tokens expire on restart; persistence is part of the future authentication slice.
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(builder.Environment.ContentRootPath, "App_Data", "ChavesLocais")))
    .UseEphemeralDataProtectionProvider();
builder.Services.AddHttpContextAccessor();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(o => { o.Cookie.Name = "Panifica.Sessao"; o.Cookie.HttpOnly = true; o.Cookie.SameSite = SameSiteMode.Strict; o.IdleTimeout = TimeSpan.FromMinutes(30); });
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o =>
{
    o.Cookie.Name = "Panifica.Acesso"; o.Cookie.HttpOnly = true; o.Cookie.SameSite = SameSiteMode.Strict;
    o.LoginPath = "/Conta/Entrar"; o.AccessDeniedPath = "/Conta/AcessoNegado"; o.ExpireTimeSpan = TimeSpan.FromMinutes(30); o.SlidingExpiration = false;
});
builder.Services.AddAuthorization(o => o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().RequireRole(PanificaB2B.Contratos.Permissoes.Equipe.Split(',')).Build());
builder.Services.AddTransient<TokenApiHandler>();
builder.Services.AddHttpClient("Distribuidora", client => client.BaseAddress = new Uri(builder.Configuration["ApiUrl"] ?? "http://localhost:5187/")).AddHttpMessageHandler<TokenApiHandler>();
builder.Services.AddHttpClient("Acesso", client => client.BaseAddress = new Uri(builder.Configuration["ApiUrl"] ?? "http://localhost:5187/"));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
}

app.Use(async (context, next) =>
{
    if (context.Connection.RemoteIpAddress is { } ip && !System.Net.IPAddress.IsLoopback(ip)) { context.Response.StatusCode = 403; return; }
    await next();
});
app.UseRequestLocalization();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.Use(async (context, next) =>
{
    try { await next(); }
    catch (SessaoApiExpiradaException)
    {
        context.Session.Clear();
        await context.SignOutAsync();
        context.Response.Redirect("/Conta/Entrar");
    }
});
app.UseAuthorization();

app.MapStaticAssets().AllowAnonymous();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();

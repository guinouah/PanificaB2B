using System.Net.Http.Headers;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PanificaB2B.Contratos;

namespace PanificaB2B.Web.Controllers;

public sealed class ContaController(IHttpClientFactory factory) : Controller
{
    HttpClient Api => factory.CreateClient("Acesso");
    [AllowAnonymous, HttpGet]
    public async Task<IActionResult> Entrar()
    {
        try
        {
            var config = await Api.GetFromJsonAsync<ConfiguracaoAcessoDto>("api/auth/configuracao");
            if (config?.Configurado == false) return RedirectToAction(nameof(Configurar));
        }
        catch (HttpRequestException) { ModelState.AddModelError("", "Inicie a API para entrar no sistema."); }
        return View(new CredenciaisEntrada());
    }
    [AllowAnonymous, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Entrar(CredenciaisEntrada entrada)
    {
        if (!ModelState.IsValid) return View(entrada);
        try
        {
            using var response = await Api.PostAsJsonAsync("api/auth/entrar", entrada);
            if (!response.IsSuccessStatusCode) { ModelState.AddModelError("", response.StatusCode == System.Net.HttpStatusCode.TooManyRequests ? "Muitas tentativas. Aguarde um minuto." : "Login ou senha inválidos."); return View(entrada); }
            var token = (await response.Content.ReadFromJsonAsync<TokenResposta>())!;
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/auth/eu");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
            using var userResponse = await Api.SendAsync(request);
            userResponse.EnsureSuccessStatusCode();
            var user = (await userResponse.Content.ReadFromJsonAsync<UsuarioDto>())!;
            if (!Permissoes.PodeUsarSite(user.Perfil))
            {
                using var sair = new HttpRequestMessage(HttpMethod.Post, "api/auth/sair");
                sair.Headers.Authorization = request.Headers.Authorization;
                using var result = await Api.SendAsync(sair);
                ModelState.AddModelError("", "O site é destinado à equipe da distribuidora. Vendedores utilizam o aplicativo.");
                return View(entrada);
            }
            HttpContext.Session.SetString("ApiToken", token.AccessToken);
            var principal = new ClaimsPrincipal(new ClaimsIdentity([new(ClaimTypes.NameIdentifier, user.Id.ToString()), new(ClaimTypes.Name, user.Login), new(ClaimTypes.Role, user.Perfil.ToString())], CookieAuthenticationDefaults.AuthenticationScheme));
            await HttpContext.SignInAsync(principal, new AuthenticationProperties { IsPersistent = false, ExpiresUtc = DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn) });
            return RedirectToAction("Index", "Home");
        }
        catch (HttpRequestException) { ModelState.AddModelError("", "Não foi possível comunicar com a API."); return View(entrada); }
    }
    [AllowAnonymous, HttpGet]
    public async Task<IActionResult> Configurar()
    {
        try
        {
            if ((await Api.GetFromJsonAsync<ConfiguracaoAcessoDto>("api/auth/configuracao"))?.Configurado == true) return RedirectToAction(nameof(Entrar));
        }
        catch (HttpRequestException) { ModelState.AddModelError("", "Inicie a API para configurar o acesso."); }
        return View(new CredenciaisEntrada());
    }
    [AllowAnonymous, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Configurar(CredenciaisEntrada entrada)
    {
        if (!ModelState.IsValid) return View(entrada);
        try
        {
            using var response = await Api.PostAsJsonAsync("api/auth/configurar", entrada);
            if (response.IsSuccessStatusCode) return RedirectToAction(nameof(Entrar));
            ModelState.AddModelError("", "A configuração não pôde ser concluída. Se já existe administrador, utilize Entrar.");
        }
        catch (HttpRequestException) { ModelState.AddModelError("", "Não foi possível comunicar com a API."); }
        return View(entrada);
    }
    [AllowAnonymous, HttpGet]
    public IActionResult AcessoNegado() { Response.StatusCode = 403; return View(); }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Sair()
    {
        try { using var response = await factory.CreateClient("Distribuidora").PostAsync("api/auth/sair", null); }
        catch (HttpRequestException) { }
        finally { HttpContext.Session.Clear(); await HttpContext.SignOutAsync(); }
        return RedirectToAction(nameof(Entrar));
    }
}

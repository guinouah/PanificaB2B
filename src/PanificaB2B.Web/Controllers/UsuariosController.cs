using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PanificaB2B.Contratos;
using PanificaB2B.Web.Models;

namespace PanificaB2B.Web.Controllers;

[Authorize(Roles = "Administrador")]
public sealed class UsuariosController(IHttpClientFactory factory) : Controller
{
    HttpClient Api => factory.CreateClient("Distribuidora");
    async Task<FormUsuario> Formulario(NovoUsuarioEntrada? entrada = null) => new()
    {
        Entrada = entrada ?? new() { Perfil = PerfilUsuario.Vendedor },
        Usuarios = await Api.GetFromJsonAsync<List<UsuarioDto>>("api/auth/usuarios") ?? [],
        Vendedores = (await Api.GetFromJsonAsync<List<VendedorDto>>("api/vendedores") ?? []).Where(x => x.Ativo).ToList()
    };
    public async Task<IActionResult> Index() => View(await Formulario());
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Index([Bind(Prefix = "Entrada")] NovoUsuarioEntrada entrada)
    {
        if (!ModelState.IsValid) return View(await Formulario(entrada));
        using var response = await Api.PostAsJsonAsync("api/auth/usuarios", entrada);
        if (response.IsSuccessStatusCode) return RedirectToAction(nameof(Index));
        ModelState.AddModelError("", "Não foi possível cadastrar. Confira o login, o perfil e o vínculo do vendedor; logins e vínculos não podem se repetir.");
        return View(await Formulario(entrada));
    }
}

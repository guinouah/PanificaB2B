using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using PanificaB2B.Web.Models;

namespace PanificaB2B.Web.Controllers;

public class HomeController(IHttpClientFactory clients) : Controller
{
    public async Task<IActionResult> Index()
    {
        try
        {
            var api = clients.CreateClient("Distribuidora");
            return View(await api.GetFromJsonAsync<PanificaB2B.Contratos.PainelDto>("api/painel"));
        }
        catch (HttpRequestException ex) when (ex is not PanificaB2B.Web.Seguranca.SessaoApiExpiradaException) { ViewBag.Erro = "Inicie a API em http://localhost:5187 para consultar os dados."; return View(new PanificaB2B.Contratos.PainelDto(0, 0, 0, 0, 0, 0)); }
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [AllowAnonymous, ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}

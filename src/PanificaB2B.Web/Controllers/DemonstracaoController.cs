using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PanificaB2B.Contratos;
namespace PanificaB2B.Web.Controllers;
[Authorize(Roles=nameof(PerfilUsuario.Administrador))]
public sealed class DemonstracaoController(IHttpClientFactory factory):ApiMvcController(factory)
{
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> Preparar(DemonstracaoEntrada entrada)
    {
        if(!ModelState.IsValid){TempData["Erro"]="Defina uma senha com pelo menos 12 caracteres para os acessos de demonstração.";return RedirectToAction("Index","Home");}
        using var resposta=await Api.PostAsJsonAsync("api/demonstracao/preparar",entrada);
        if(resposta.IsSuccessStatusCode)Sucesso((await resposta.Content.ReadFromJsonAsync<DemonstracaoDto>())!.Mensagem);
        else{var detalhe=await resposta.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();TempData["Erro"]=detalhe.TryGetProperty("mensagem",out var m)?m.GetString():"Não foi possível preparar a demonstração.";}
        return RedirectToAction("Index","Pedidos");
    }
}

using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace PanificaB2B.Web.Controllers;

public abstract class ApiMvcController(IHttpClientFactory factory) : Controller
{
    protected HttpClient Api => factory.CreateClient("Distribuidora");
    protected async Task<bool> Gravar(string rota, object entrada, Guid? id = null)
    {
        using var resposta = id is null ? await Api.PostAsJsonAsync(rota, entrada) : await Api.PutAsJsonAsync($"{rota}/{id}", entrada);
        if (resposta.IsSuccessStatusCode) return true;
        var texto = await resposta.Content.ReadAsStringAsync();
        var mensagem = "Não foi possível concluir. Confira os dados e tente novamente.";
        try
        {
            var json = JsonDocument.Parse(texto).RootElement;
            if (json.TryGetProperty("mensagem", out var m)) mensagem = m.GetString() ?? mensagem;
            else if (json.TryGetProperty("errors", out var erros)) mensagem = string.Join(" ", erros.EnumerateObject().SelectMany(x => x.Value.EnumerateArray()).Select(x => x.GetString()));
        }
        catch (JsonException) { }
        ModelState.AddModelError("", mensagem); return false;
    }
    protected void Sucesso(string texto) => TempData["Sucesso"] = texto;
    protected void GuardarErro() => TempData["Erro"] = string.Join(" ", ModelState.Values.SelectMany(x => x.Errors).Select(x => x.ErrorMessage));
}

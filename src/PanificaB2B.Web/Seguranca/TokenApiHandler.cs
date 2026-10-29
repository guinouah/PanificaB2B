using System.Net;
using System.Net.Http.Headers;

namespace PanificaB2B.Web.Seguranca;

public sealed class SessaoApiExpiradaException() : HttpRequestException("Entre novamente para continuar.");

public sealed class TokenApiHandler(IHttpContextAccessor accessor) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = accessor.HttpContext?.Session.GetString("ApiToken");
        if (string.IsNullOrWhiteSpace(token)) throw new SessaoApiExpiradaException();
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized) { response.Dispose(); throw new SessaoApiExpiradaException(); }
        return response;
    }
}

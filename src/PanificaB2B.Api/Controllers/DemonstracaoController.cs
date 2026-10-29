using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PanificaB2B.Api.Seguranca;
using PanificaB2B.Api.Servicos;
using PanificaB2B.Contratos;
namespace PanificaB2B.Api.Controllers;
[ApiController,Authorize(Roles=nameof(PerfilUsuario.Administrador)),Route("api/demonstracao")]
public sealed class DemonstracaoController(DemonstracaoService demonstracao):ControllerBase
{
    [HttpPost("preparar")] public async Task<IActionResult> Preparar(DemonstracaoEntrada entrada)=>
        Ok(await demonstracao.Preparar(User.Identidade().UsuarioId,entrada.SenhaAcessos));
}

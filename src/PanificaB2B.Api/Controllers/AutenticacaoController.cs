using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PanificaB2B.Api.Dados;
using PanificaB2B.Api.Modelos;
using PanificaB2B.Api.Seguranca;
using PanificaB2B.Contratos;

namespace PanificaB2B.Api.Controllers;

[ApiController, Route("api/auth")]
public sealed class AutenticacaoController(DistribuidoraDbContext db, SenhasService senhas) : ControllerBase
{
    public static UsuarioDto Dto(Usuario x) => new(x.Id, x.Login, x.Perfil, x.VendedorId);
    static Usuario Criar(CredenciaisEntrada r, PerfilUsuario perfil, Guid? vendedor, SenhasService senhas)
    {
        var usuario = new Usuario { Login = r.Login.Trim(), LoginNormalizado = r.Login.Trim().ToUpperInvariant(), Perfil = perfil, VendedorId = vendedor };
        usuario.SenhaHash = senhas.Gerar(usuario, r.Senha);
        return usuario;
    }
    [AllowAnonymous, HttpGet("configuracao")]
    public async Task<IActionResult> Configuracao() => Ok(new ConfiguracaoAcessoDto(await db.Usuarios.AnyAsync()));

    [AllowAnonymous, EnableRateLimiting("login"), HttpPost("configurar")]
    public async Task<IActionResult> Configurar(CredenciaisEntrada r)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        if (await db.Usuarios.AnyAsync()) return Conflict(new { mensagem = "O administrador inicial já foi configurado." });
        if (r.Senha.Length < 12) return BadRequest(new { mensagem = "Novos acessos exigem senha com pelo menos 12 caracteres." });
        var usuario = Criar(r, PerfilUsuario.Administrador, null, senhas);
        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync(); await tx.CommitAsync();
        return Ok(Dto(usuario));
    }
    [AllowAnonymous, EnableRateLimiting("login"), HttpPost("entrar")]
    public async Task<IActionResult> Entrar(CredenciaisEntrada r)
    {
        var normalizado = r.Login.Trim().ToUpperInvariant();
        var usuario = await db.Usuarios.SingleOrDefaultAsync(x => x.LoginNormalizado == normalizado);
        var senhaValida = senhas.Conferir(usuario, r.Senha);
        if (!senhaValida || usuario is not { Ativo: true } || !Enum.IsDefined(usuario.Perfil) || usuario.Perfil == PerfilUsuario.Vendedor && !await db.Vendedores.AnyAsync(v => v.Id == usuario.VendedorId && v.Ativo))
            return Unauthorized(new { mensagem = "Login ou senha inválidos." });
        var sessao = new SessaoAcesso { UsuarioId = usuario.Id, ExpiraEm = DateTimeOffset.UtcNow.AddMinutes(30) };
        db.SessoesAcesso.Add(sessao); await db.SaveChangesAsync();
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, usuario.Id.ToString()), new(ClaimTypes.Name, usuario.Login), new(ClaimTypes.Role, usuario.Perfil.ToString()), new("sessaoId", sessao.Id.ToString()) };
        if (usuario.VendedorId is { } id) claims.Add(new("vendedorId", id.ToString()));
        return SignIn(new ClaimsPrincipal(new ClaimsIdentity(claims, BearerTokenDefaults.AuthenticationScheme)), BearerTokenDefaults.AuthenticationScheme);
    }
    [HttpGet("eu")]
    public async Task<IActionResult> Eu() => Ok(Dto((await db.Usuarios.FindAsync(User.Identidade().UsuarioId))!));

    [HttpPost("sair")]
    public async Task<IActionResult> Sair()
    {
        var id = Guid.Parse(User.FindFirstValue("sessaoId")!);
        var sessao = (await db.SessoesAcesso.FindAsync(id))!;
        sessao.Ativa = false; await db.SaveChangesAsync();
        return NoContent();
    }
    [Authorize(Roles = nameof(PerfilUsuario.Administrador)), HttpGet("usuarios")]
    public async Task<IActionResult> Usuarios() => Ok((await db.Usuarios.AsNoTracking().OrderBy(x => x.Login).ToListAsync()).Select(Dto));

    [Authorize(Roles = nameof(PerfilUsuario.Administrador)), HttpPost("usuarios")]
    public async Task<IActionResult> NovoUsuario(NovoUsuarioEntrada r)
    {
        if (r.Senha.Length < 12) return BadRequest(new { mensagem = "Novos acessos exigem senha com pelo menos 12 caracteres." });
        if (r.Perfil == PerfilUsuario.Vendedor && !await db.Vendedores.AnyAsync(x => x.Id == r.VendedorId && x.Ativo)) return BadRequest(new { mensagem = "Vincule um vendedor ativo." });
        if (!Enum.IsDefined(r.Perfil)) return BadRequest(new { mensagem = "Perfil inválido." });
        if (r.Perfil != PerfilUsuario.Vendedor && r.VendedorId is not null) return BadRequest(new { mensagem = "Somente o perfil Vendedor possui vínculo de vendedor." });
        var usuario = Criar(r, r.Perfil, r.VendedorId, senhas);
        db.Usuarios.Add(usuario); await db.SaveChangesAsync();
        return Ok(Dto(usuario));
    }
}

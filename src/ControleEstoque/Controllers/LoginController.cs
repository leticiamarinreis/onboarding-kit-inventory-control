using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ControleEstoque.DAO;
using ControleEstoque.Models;

namespace ControleEstoque.Controllers;

/// <summary>Tela de login (cookie de autenticação) e saída do sistema.</summary>
[AllowAnonymous]
public class LoginController : Controller
{
    private readonly UsuarioDAO _usuarioDAO;
    private readonly ILogger<LoginController> _logger;

    public LoginController(UsuarioDAO usuarioDAO, ILogger<LoginController> logger)
    {
        _usuarioDAO = usuarioDAO;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Index(string returnUrl)
    {
        if (User.Identity != null && User.Identity.IsAuthenticated)
            return RedirectToAction("Index", "Home");

        ViewBag.ReturnUrl = returnUrl;
        return View(new Usuario());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index([Bind("NomeUsuario,Senha")] Usuario dados, string returnUrl)
    {
        ViewBag.ReturnUrl = returnUrl;

        if (!ModelState.IsValid)
            return View(dados);

        var usuario = _usuarioDAO.BuscarPorNome(dados.NomeUsuario.Trim());

        // A mesma mensagem para "usuário não existe" e "senha errada": não revela quais usuários existem.
        if (usuario == null || !usuario.SenhaConfere(dados.Senha))
        {
            _logger.LogWarning("Tentativa de login recusada para o usuário '{Usuario}'.", dados.NomeUsuario);
            ModelState.AddModelError(string.Empty, "Usuário ou senha inválidos.");
            dados.Senha = null;
            return View(dados);
        }

        var identidade = new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
                new Claim(ClaimTypes.Name, usuario.NomeUsuario)
            },
            CookieAuthenticationDefaults.AuthenticationScheme);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identidade));

        // Só volta para endereços do próprio site (evita redirecionar para um site de fora).
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);

        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> Sair()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Index));
    }
}

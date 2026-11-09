using ControleEstoque.DAO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ControleEstoque.Controllers;

/// <summary>Tela principal: é aqui que o estoque é controlado (entrada, saída, reservas e bloqueios).</summary>
public class HomeController : BaseController
{
    private readonly EstoqueDAO _dao;

    public HomeController(EstoqueDAO dao)
    {
        _dao = dao;
    }

    public IActionResult Index()
    {
        var kits = _dao.ListarKits();

        ViewBag.Estoque = _dao.ConsultarEstoque();
        ViewBag.Retencoes = _dao.ListarRetencoes(ativas: true);
        ViewBag.TotalKits = kits.Count;

        return View(kits.Take(5).ToList());
    }

    [AllowAnonymous]
    public IActionResult Error(int? codigo)
    {
        ViewBag.Codigo = codigo;
        ViewBag.IdRequisicao = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        return View();
    }
}

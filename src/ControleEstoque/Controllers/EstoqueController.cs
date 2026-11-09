using ControleEstoque.DAO;
using Microsoft.AspNetCore.Mvc;

namespace ControleEstoque.Controllers;

/// <summary>Entrada e Saída do estoque. Os formulários ficam na tela principal.</summary>
public class EstoqueController : BaseController
{
    private readonly EstoqueDAO _dao;

    public EstoqueController(EstoqueDAO dao)
    {
        _dao = dao;
    }

    [HttpPost]
    public IActionResult Entrada(int quantidade)
    {
        Tentar(() =>
        {
            _dao.Entrada(quantidade);
            return $"Entrada de {quantidade} unidade(s) registrada.";
        });
        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    public IActionResult Saida(int quantidade)
    {
        Tentar(() =>
        {
            _dao.Saida(quantidade);
            return $"Saída de {quantidade} unidade(s) registrada.";
        });
        return RedirectToAction("Index", "Home");
    }
}

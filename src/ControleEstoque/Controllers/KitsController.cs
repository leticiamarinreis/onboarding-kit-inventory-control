using ControleEstoque.DAO;
using ControleEstoque.Models;
using Microsoft.AspNetCore.Mvc;

namespace ControleEstoque.Controllers;

/// <summary>Os kits só são cadastrados e listados. A quantidade do estoque é controlada na tela principal.</summary>
public class KitsController : BaseController
{
    private readonly EstoqueDAO _dao;

    public KitsController(EstoqueDAO dao)
    {
        _dao = dao;
    }

    public IActionResult Index()
    {
        ViewBag.Estoque = _dao.ConsultarEstoque();
        return View(_dao.ListarKits());
    }

    public IActionResult Create()
    {
        ViewBag.Estoque = _dao.ConsultarEstoque();
        return View(new Kit());
    }

    // Só o funcional do colaborador é informado. IdKit e data não estão no Bind.
    [HttpPost]
    public IActionResult Create([Bind("Funcional")] Kit kit)
    {
        if (ModelState.IsValid)
        {
            try
            {
                int idKit = _dao.CriarKit(kit.Funcional);

                TempData["Sucesso"] = $"Kit cadastrado com sucesso. IdKit gerado: {idKit}. Foi descontada 1 unidade do estoque.";
                return RedirectToAction(nameof(Index));
            }
            catch (EstoqueException erro)
            {
                ModelState.AddModelError(string.Empty, erro.Message);
            }
        }

        ViewBag.Estoque = _dao.ConsultarEstoque();
        return View(kit);
    }
}

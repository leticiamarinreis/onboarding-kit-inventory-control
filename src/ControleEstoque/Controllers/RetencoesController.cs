using ControleEstoque.DAO;
using Microsoft.AspNetCore.Mvc;

namespace ControleEstoque.Controllers;

/// <summary>Reservas e bloqueios (a mesma estrutura, separada pelo Tipo).</summary>
public class RetencoesController : BaseController
{
    private readonly EstoqueDAO _dao;

    public RetencoesController(EstoqueDAO dao)
    {
        _dao = dao;
    }

    // Consulta por identificação (ex.: RES-0003 ou só 3) e por situação.
    public IActionResult Index(string codigo, string situacao = "ativas")
    {
        int? id = null;
        string digitos = new string((codigo ?? "").Where(char.IsDigit).ToArray());
        if (digitos.Length > 0 && digitos.Length <= 9)
            id = int.Parse(digitos);

        bool? ativas = situacao == "ativas" ? true : situacao == "liberadas" ? false : (bool?)null;

        ViewBag.Codigo = codigo;
        ViewBag.Situacao = situacao;
        return View(_dao.ListarRetencoes(ativas, id));
    }

    [HttpPost]
    public IActionResult Reservar(int quantidade, string solicitacao)
    {
        Tentar(() =>
        {
            int id = _dao.Reservar(quantidade, solicitacao);
            return $"Reserva RES-{id:0000} criada.";
        });
        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    public IActionResult Bloquear(int quantidade, string motivo)
    {
        Tentar(() =>
        {
            int id = _dao.Bloquear(quantidade, motivo);
            return $"Bloqueio BLQ-{id:0000} criado.";
        });
        return RedirectToAction("Index", "Home");
    }

    // "voltarParaHome" vem do formulário da tela principal; na lista, volta para a lista.
    [HttpPost]
    public IActionResult Liberar(int id, bool voltarParaHome = false)
    {
        Tentar(() =>
        {
            _dao.Liberar(id);
            return "Liberado: as unidades voltaram a ficar disponíveis.";
        });

        return voltarParaHome
            ? RedirectToAction("Index", "Home")
            : RedirectToAction(nameof(Index));
    }
}

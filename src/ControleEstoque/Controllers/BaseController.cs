using ControleEstoque.Models;
using Microsoft.AspNetCore.Mvc;

namespace ControleEstoque.Controllers;

public abstract class BaseController : Controller
{
    /// <summary>
    /// Executa a operação, que devolve a mensagem de sucesso. Erro de regra (EstoqueException)
    /// vira mensagem vermelha na tela; qualquer outro erro continua subindo (é um problema do sistema).
    /// </summary>
    protected bool Tentar(Func<string> operacao)
    {
        try
        {
            TempData["Sucesso"] = operacao();
            return true;
        }
        catch (EstoqueException erro)
        {
            TempData["Erro"] = erro.Message;
            return false;
        }
    }
}

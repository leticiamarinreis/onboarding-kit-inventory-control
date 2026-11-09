namespace ControleEstoque.Models;

/// <summary>
/// Erro de regra de negócio (ex.: "quantidade maior que a disponível").
/// A mensagem é escrita para o usuário e é mostrada na tela.
/// </summary>
public class EstoqueException : Exception
{
    public EstoqueException(string mensagem) : base(mensagem) { }
}

namespace ControleEstoque.Models;

/// <summary>
/// O estoque de kits (um só, o estoque geral). TODAS as regras de quantidade estão aqui, em métodos pequenos.
///
/// Guardamos Total, Reservada e Bloqueada. A Disponível é sempre calculada:
///     Disponivel = Total - Reservada - Bloqueada
/// Assim a regra "Total = Disponível + Reservada + Bloqueada" não tem como ser quebrada.
///
/// Todo método valida ANTES de alterar. Se lançar EstoqueException, o objeto não mudou.
/// </summary>
public class Estoque
{
    public int Total { get; set; }
    public int Reservada { get; set; }
    public int Bloqueada { get; set; }

    public int Disponivel => Total - Reservada - Bloqueada;

    /// <summary>Entrada: adiciona unidades ao estoque.</summary>
    public void Entrada(int quantidade)
    {
        ValidarQuantidade(quantidade);

        if (quantidade > int.MaxValue - Total)
            throw new EstoqueException("A quantidade informada ultrapassa o limite do estoque.");

        Total += quantidade;
    }

    /// <summary>Saída: retira unidades. Só do que está disponível.</summary>
    public void Saida(int quantidade)
    {
        GarantirDisponivel(quantidade, "retirar");
        Total -= quantidade;
    }

    /// <summary>Cadastro de um kit: cada kit cadastrado leva 1 unidade do estoque disponível.</summary>
    public void RetirarParaKit()
    {
        if (Disponivel < 1)
            throw new EstoqueException(
                "Não há kit disponível no estoque para este cadastro. Registre uma entrada na tela inicial.");

        Total -= 1;
    }

    /// <summary>Reservar: separa unidades disponíveis para uma solicitação/pedido.</summary>
    public void Reservar(int quantidade)
    {
        GarantirDisponivel(quantidade, "reservar");
        Reservada += quantidade;
    }

    /// <summary>Bloquear: torna unidades disponíveis indisponíveis para uso.</summary>
    public void Bloquear(int quantidade)
    {
        GarantirDisponivel(quantidade, "bloquear");
        Bloqueada += quantidade;
    }

    /// <summary>Liberar reserva: as unidades voltam a ser disponíveis.</summary>
    public void LiberarReserva(int quantidade)
    {
        ValidarQuantidade(quantidade);

        if (quantidade > Reservada)
            throw new EstoqueException(
                $"Não é possível liberar {quantidade} unidade(s): só há {Reservada} reservada(s).");

        Reservada -= quantidade;
    }

    /// <summary>Liberar bloqueio: as unidades voltam a ser disponíveis.</summary>
    public void LiberarBloqueio(int quantidade)
    {
        ValidarQuantidade(quantidade);

        if (quantidade > Bloqueada)
            throw new EstoqueException(
                $"Não é possível liberar {quantidade} unidade(s): só há {Bloqueada} bloqueada(s).");

        Bloqueada -= quantidade;
    }

    private static void ValidarQuantidade(int quantidade)
    {
        if (quantidade <= 0)
            throw new EstoqueException("A quantidade deve ser maior que zero.");
    }

    // Regra central: nenhuma retirada/reserva/bloqueio pode passar do disponível.
    private void GarantirDisponivel(int quantidade, string acao)
    {
        ValidarQuantidade(quantidade);

        if (quantidade > Disponivel)
            throw new EstoqueException(
                $"Não é possível {acao} {quantidade} unidade(s) do estoque: só há {Disponivel} disponível(is).");
    }
}

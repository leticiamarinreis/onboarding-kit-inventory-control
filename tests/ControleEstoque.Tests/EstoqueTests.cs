using ControleEstoque.Models;
using Xunit;

namespace ControleEstoque.Tests;

/// <summary>Regras de estoque no Model (Estoque). Rápidos: não usam banco.</summary>
public class EstoqueTests
{
    private static Estoque NovoEstoque(int total = 100, int reservada = 0, int bloqueada = 0) =>
        new Estoque { Total = total, Reservada = reservada, Bloqueada = bloqueada };

    private static void ExigirConsistente(Estoque p)
    {
        Assert.Equal(p.Total, p.Disponivel + p.Reservada + p.Bloqueada);
        Assert.True(p.Disponivel >= 0 && p.Reservada >= 0 && p.Bloqueada >= 0);
    }

    [Fact]
    public void Entrada_aumenta_total_e_disponivel()
    {
        var p = NovoEstoque(total: 10);
        p.Entrada(5);

        Assert.Equal(15, p.Total);
        Assert.Equal(15, p.Disponivel);
        ExigirConsistente(p);
    }

    [Fact]
    public void Saida_diminui_total_e_disponivel()
    {
        var p = NovoEstoque();
        p.Saida(30);

        Assert.Equal(70, p.Total);
        Assert.Equal(70, p.Disponivel);
        ExigirConsistente(p);
    }

    [Fact]
    public void Saida_pode_retirar_exatamente_o_disponivel()
    {
        var p = NovoEstoque();
        p.Saida(100);

        Assert.Equal(0, p.Total);
        ExigirConsistente(p);
    }

    [Fact]
    public void Saida_acima_do_disponivel_e_recusada_e_nada_muda()
    {
        var p = NovoEstoque();

        Assert.Throws<EstoqueException>(() => p.Saida(101));

        Assert.Equal(100, p.Total);
        ExigirConsistente(p);
    }

    [Fact]
    public void Cadastrar_kit_leva_1_unidade_do_disponivel()
    {
        var p = NovoEstoque(total: 10, reservada: 3);
        p.RetirarParaKit();

        Assert.Equal(9, p.Total);
        Assert.Equal(6, p.Disponivel);
        ExigirConsistente(p);
    }

    [Fact]
    public void Cadastrar_kit_sem_disponivel_e_recusado_e_nada_muda()
    {
        var p = NovoEstoque(total: 5, reservada: 2, bloqueada: 3); // nada disponível

        Assert.Throws<EstoqueException>(() => p.RetirarParaKit());

        Assert.Equal(5, p.Total);
        ExigirConsistente(p);
    }

    [Fact]
    public void Reservar_move_do_disponivel_para_reservada()
    {
        var p = NovoEstoque();
        p.Reservar(40);

        Assert.Equal(100, p.Total);
        Assert.Equal(60, p.Disponivel);
        Assert.Equal(40, p.Reservada);
        ExigirConsistente(p);
    }

    [Fact]
    public void Reservar_acima_do_disponivel_e_recusado()
    {
        var p = NovoEstoque();

        Assert.Throws<EstoqueException>(() => p.Reservar(101));

        Assert.Equal(0, p.Reservada);
        Assert.Equal(100, p.Disponivel);
    }

    [Fact]
    public void Bloquear_move_do_disponivel_para_bloqueada()
    {
        var p = NovoEstoque();
        p.Bloquear(25);

        Assert.Equal(75, p.Disponivel);
        Assert.Equal(25, p.Bloqueada);
        ExigirConsistente(p);
    }

    [Fact]
    public void Bloquear_acima_do_disponivel_e_recusado()
    {
        var p = NovoEstoque();

        Assert.Throws<EstoqueException>(() => p.Bloquear(101));

        Assert.Equal(0, p.Bloqueada);
        Assert.Equal(100, p.Disponivel);
    }

    [Fact]
    public void Disponivel_ja_desconta_reservas_e_bloqueios()
    {
        // 100 no total, 30 reservadas e 50 bloqueadas: sobram 20.
        var p = NovoEstoque(total: 100, reservada: 30, bloqueada: 50);

        Assert.Equal(20, p.Disponivel);
        Assert.Throws<EstoqueException>(() => p.Saida(21));
        Assert.Throws<EstoqueException>(() => p.Reservar(21));
        Assert.Throws<EstoqueException>(() => p.Bloquear(21));

        p.Saida(20);
        Assert.Equal(0, p.Disponivel);
        ExigirConsistente(p);
    }

    [Fact]
    public void Varias_reservas_e_bloqueios_somam_nos_totais()
    {
        var p = NovoEstoque();
        p.Reservar(10);
        p.Reservar(15);
        p.Bloquear(5);
        p.Bloquear(7);

        Assert.Equal(25, p.Reservada);
        Assert.Equal(12, p.Bloqueada);
        Assert.Equal(63, p.Disponivel);
        ExigirConsistente(p);
    }

    [Fact]
    public void Liberar_reserva_devolve_ao_disponivel()
    {
        var p = NovoEstoque(reservada: 40);
        p.LiberarReserva(15);

        Assert.Equal(25, p.Reservada);
        Assert.Equal(75, p.Disponivel);
        ExigirConsistente(p);
    }

    [Fact]
    public void Liberar_bloqueio_devolve_ao_disponivel()
    {
        var p = NovoEstoque(bloqueada: 40);
        p.LiberarBloqueio(40);

        Assert.Equal(0, p.Bloqueada);
        Assert.Equal(100, p.Disponivel);
        ExigirConsistente(p);
    }

    [Fact]
    public void Nao_libera_mais_do_que_esta_reservado_ou_bloqueado()
    {
        var p = NovoEstoque(reservada: 10, bloqueada: 5);

        Assert.Throws<EstoqueException>(() => p.LiberarReserva(11));
        Assert.Throws<EstoqueException>(() => p.LiberarBloqueio(6));

        Assert.Equal(10, p.Reservada);
        Assert.Equal(5, p.Bloqueada);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Quantidade_precisa_ser_maior_que_zero_em_todas_as_operacoes(int quantidade)
    {
        var p = NovoEstoque(reservada: 10, bloqueada: 10);

        Assert.Throws<EstoqueException>(() => p.Entrada(quantidade));
        Assert.Throws<EstoqueException>(() => p.Saida(quantidade));
        Assert.Throws<EstoqueException>(() => p.Reservar(quantidade));
        Assert.Throws<EstoqueException>(() => p.Bloquear(quantidade));
        Assert.Throws<EstoqueException>(() => p.LiberarReserva(quantidade));
        Assert.Throws<EstoqueException>(() => p.LiberarBloqueio(quantidade));

        Assert.Equal(100, p.Total);
        Assert.Equal(10, p.Reservada);
        Assert.Equal(10, p.Bloqueada);
    }

    [Fact]
    public void Entrada_que_estouraria_o_limite_e_recusada()
    {
        var p = NovoEstoque(total: int.MaxValue - 1);

        Assert.Throws<EstoqueException>(() => p.Entrada(2));
        Assert.Equal(int.MaxValue - 1, p.Total);
    }

    [Fact]
    public void Mensagem_de_erro_diz_quanto_ha_disponivel()
    {
        var p = NovoEstoque(total: 10, reservada: 4);

        var erro = Assert.Throws<EstoqueException>(() => p.Reservar(7));

        Assert.Contains("só há 6 disponível", erro.Message);
    }
}

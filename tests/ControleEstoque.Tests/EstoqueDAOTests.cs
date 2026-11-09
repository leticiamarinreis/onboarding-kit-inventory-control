using ControleEstoque.DAO;
using ControleEstoque.Models;
using Xunit;

namespace ControleEstoque.Tests;

/// <summary>
/// Testes de integração: usam um SQLite de verdade, em um arquivo temporário (criado e apagado por teste).
/// Provam o que o Model sozinho não prova: transação, rollback, liberação e concorrência.
/// </summary>
public class EstoqueDAOTests : IDisposable
{
    private readonly string _arquivo = Path.Combine(Path.GetTempPath(), "estoque-teste-" + Guid.NewGuid().ToString("N") + ".db");
    private readonly ConexaoDAO _conexao;
    private readonly EstoqueDAO _dao;

    public EstoqueDAOTests()
    {
        _conexao = new ConexaoDAO("Data Source=" + _arquivo);
        _conexao.CriarBancoSeNecessario();
        _dao = new EstoqueDAO(_conexao);
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        foreach (string sufixo in new[] { "", "-wal", "-shm" })
        {
            try { File.Delete(_arquivo + sufixo); } catch (IOException) { }
        }
    }

    // Coloca unidades no estoque (quem faz isso é o usuário, na tela principal).
    private void ComEstoque(int quantidade) => _dao.Entrada(quantidade);

    // Confere a regra principal E que Reservada/Bloqueada batem com as reservas/bloqueios ativos.
    private void ExigirEstoqueConsistente()
    {
        var e = _dao.ConsultarEstoque();
        var ativas = _dao.ListarRetencoes(ativas: true);

        Assert.Equal(e.Total, e.Disponivel + e.Reservada + e.Bloqueada);
        Assert.True(e.Disponivel >= 0);
        Assert.Equal(ativas.Where(r => r.Tipo == TipoRetencao.Reserva).Sum(r => r.Quantidade), e.Reservada);
        Assert.Equal(ativas.Where(r => r.Tipo == TipoRetencao.Bloqueio).Sum(r => r.Quantidade), e.Bloqueada);
    }

    // ------------------------------------------------------------------ Estoque

    [Fact]
    public void Estoque_comeca_zerado()
    {
        var e = _dao.ConsultarEstoque();

        Assert.Equal(0, e.Total);
        Assert.Equal(0, e.Disponivel);
        Assert.Equal(0, e.Reservada);
        Assert.Equal(0, e.Bloqueada);
    }

    [Fact]
    public void Criar_o_banco_de_novo_nao_zera_o_estoque()
    {
        ComEstoque(7);

        _conexao.CriarBancoSeNecessario();

        Assert.Equal(7, _dao.ConsultarEstoque().Total);
    }

    [Fact]
    public void Entrada_e_saida_persistem()
    {
        ComEstoque(10);

        _dao.Entrada(5);
        _dao.Saida(3);

        Assert.Equal(12, _dao.ConsultarEstoque().Total);
        ExigirEstoqueConsistente();
    }

    [Fact]
    public void Saida_acima_do_disponivel_nao_altera_nada()
    {
        ComEstoque(10);
        _dao.Reservar(4, "PED-1");

        Assert.Throws<EstoqueException>(() => _dao.Saida(7)); // só 6 disponíveis

        var e = _dao.ConsultarEstoque();
        Assert.Equal(10, e.Total);
        Assert.Equal(4, e.Reservada);
        ExigirEstoqueConsistente();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Quantidade_zero_ou_negativa_e_recusada(int quantidade)
    {
        ComEstoque(10);

        Assert.Throws<EstoqueException>(() => _dao.Entrada(quantidade));
        Assert.Throws<EstoqueException>(() => _dao.Saida(quantidade));
        Assert.Throws<EstoqueException>(() => _dao.Reservar(quantidade, "PED-1"));
        Assert.Throws<EstoqueException>(() => _dao.Bloquear(quantidade, "Defeito"));

        Assert.Equal(10, _dao.ConsultarEstoque().Total);
        Assert.Empty(_dao.ListarRetencoes());
    }

    // ------------------------------------------------------------------ Cadastro do kit

    [Fact]
    public void Cadastro_so_com_funcional_gera_IdKit()
    {
        ComEstoque(5);

        int id = _dao.CriarKit("1234567");

        var k = _dao.BuscarKit(id);
        Assert.Equal("1234567", k.Funcional);
        Assert.Single(_dao.ListarKits());
    }

    [Fact]
    public void IdKit_e_unico_e_incremental()
    {
        ComEstoque(5);

        int a = _dao.CriarKit("1111111");
        int b = _dao.CriarKit("2222222");
        int c = _dao.CriarKit("3333333");

        Assert.True(a < b && b < c);
        Assert.Equal(3, _dao.ListarKits().Select(k => k.IdKit).Distinct().Count());
    }

    [Fact]
    public void Consultar_kit_inexistente_devolve_null()
    {
        Assert.Null(_dao.BuscarKit(999));
    }

    [Fact]
    public void Cada_kit_cadastrado_desconta_1_unidade_do_estoque()
    {
        ComEstoque(3);

        _dao.CriarKit("1111111");
        Assert.Equal(2, _dao.ConsultarEstoque().Disponivel);

        _dao.CriarKit("2222222");
        _dao.CriarKit("3333333");

        var e = _dao.ConsultarEstoque();
        Assert.Equal(0, e.Total);
        Assert.Equal(0, e.Disponivel);
        ExigirEstoqueConsistente();
    }

    [Fact]
    public void Sem_estoque_disponivel_o_kit_nao_e_cadastrado()
    {
        // estoque zerado
        var erro = Assert.Throws<EstoqueException>(() => _dao.CriarKit("1234567"));

        Assert.Contains("Registre uma entrada", erro.Message);
        Assert.Empty(_dao.ListarKits());
        Assert.Equal(0, _dao.ConsultarEstoque().Total);
    }

    [Fact]
    public void Unidades_reservadas_ou_bloqueadas_nao_servem_para_cadastrar_kit()
    {
        ComEstoque(2);
        _dao.Reservar(1, "PED-1");
        _dao.Bloquear(1, "Defeito");

        Assert.Throws<EstoqueException>(() => _dao.CriarKit("1234567"));

        Assert.Empty(_dao.ListarKits());
        ExigirEstoqueConsistente();

        // liberando uma, o cadastro passa a ser possível
        _dao.Liberar(_dao.ListarRetencoes(ativas: true).First(r => r.Tipo == TipoRetencao.Reserva).Id);
        _dao.CriarKit("1234567");
        Assert.Single(_dao.ListarKits());
        ExigirEstoqueConsistente();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("123456")]
    [InlineData("12345678")]
    [InlineData("12345a7")]
    [InlineData("abcdefg")]
    [InlineData("12 4567")]
    public void Funcional_invalido_e_recusado_e_o_estoque_nao_muda(string funcional)
    {
        ComEstoque(5);

        Assert.Throws<EstoqueException>(() => _dao.CriarKit(funcional));

        Assert.Empty(_dao.ListarKits());
        Assert.Equal(5, _dao.ConsultarEstoque().Disponivel);
    }

    [Fact]
    public void Funcional_com_espacos_nas_pontas_e_aceito_sem_eles()
    {
        ComEstoque(1);

        int id = _dao.CriarKit("  1234567 ");

        Assert.Equal("1234567", _dao.BuscarKit(id).Funcional);
    }

    [Fact]
    public void Um_colaborador_so_pode_ter_um_kit_e_o_estoque_nao_e_descontado_de_novo()
    {
        ComEstoque(5);
        int primeiro = _dao.CriarKit("1234567");

        var erro = Assert.Throws<EstoqueException>(() => _dao.CriarKit("1234567"));

        Assert.Contains("já tem um kit", erro.Message);
        Assert.Single(_dao.ListarKits());
        Assert.Equal(primeiro, _dao.ListarKits().Single().IdKit);
        Assert.Equal(4, _dao.ConsultarEstoque().Disponivel);
    }

    [Fact]
    public void Funcionais_diferentes_podem_ter_kits_diferentes()
    {
        ComEstoque(5);

        _dao.CriarKit("1234567");
        _dao.CriarKit("7654321");

        Assert.Equal(2, _dao.ListarKits().Count);
    }

    // ------------------------------------------------------------------ Reservar e bloquear

    [Fact]
    public void Reserva_gera_identificacao_e_pode_ser_consultada()
    {
        ComEstoque(100);

        int reservaId = _dao.Reservar(30, "PED-1");

        var r = _dao.ListarRetencoes(id: reservaId).Single();
        Assert.Equal(TipoRetencao.Reserva, r.Tipo);
        Assert.Equal(30, r.Quantidade);
        Assert.Equal("PED-1", r.Referencia);
        Assert.True(r.Ativa);
        Assert.StartsWith("RES-", r.Codigo);
        Assert.Equal(70, _dao.ConsultarEstoque().Disponivel);
        ExigirEstoqueConsistente();
    }

    [Fact]
    public void Bloqueio_gera_identificacao_e_tira_do_disponivel()
    {
        ComEstoque(100);

        int bloqueioId = _dao.Bloquear(20, "Lote com defeito");

        var r = _dao.ListarRetencoes(id: bloqueioId).Single();
        Assert.Equal(TipoRetencao.Bloqueio, r.Tipo);
        Assert.StartsWith("BLQ-", r.Codigo);
        Assert.Equal(80, _dao.ConsultarEstoque().Disponivel);
        Assert.Equal(20, _dao.ConsultarEstoque().Bloqueada);
        ExigirEstoqueConsistente();
    }

    [Fact]
    public void Reservar_e_bloquear_acima_do_disponivel_nao_deixam_rastro()
    {
        ComEstoque(10);

        Assert.Throws<EstoqueException>(() => _dao.Reservar(11, "PED-1"));
        Assert.Throws<EstoqueException>(() => _dao.Bloquear(11, "Defeito"));

        Assert.Equal(10, _dao.ConsultarEstoque().Disponivel);
        Assert.Empty(_dao.ListarRetencoes());
    }

    [Fact]
    public void Reserva_sem_solicitacao_e_bloqueio_sem_motivo_sao_recusados()
    {
        ComEstoque(100);

        Assert.Throws<EstoqueException>(() => _dao.Reservar(1, "  "));
        Assert.Throws<EstoqueException>(() => _dao.Bloquear(1, null));

        Assert.Empty(_dao.ListarRetencoes());
        Assert.Equal(100, _dao.ConsultarEstoque().Disponivel);
    }

    [Fact]
    public void Varias_reservas_e_bloqueios_de_solicitacoes_diferentes_convivem()
    {
        ComEstoque(100);

        _dao.Reservar(10, "PED-1");
        _dao.Reservar(20, "PED-2");
        _dao.Bloquear(5, "Defeito A");
        _dao.Bloquear(5, "Defeito B");

        var e = _dao.ConsultarEstoque();
        Assert.Equal(30, e.Reservada);
        Assert.Equal(10, e.Bloqueada);
        Assert.Equal(60, e.Disponivel);
        Assert.Equal(4, _dao.ListarRetencoes(ativas: true).Count);
        ExigirEstoqueConsistente();
    }

    // ------------------------------------------------------------------ Liberar

    [Fact]
    public void Liberar_reserva_devolve_ao_disponivel_e_so_ela()
    {
        ComEstoque(100);
        int r1 = _dao.Reservar(10, "PED-1");
        _dao.Reservar(20, "PED-2");

        _dao.Liberar(r1);

        var e = _dao.ConsultarEstoque();
        Assert.Equal(20, e.Reservada);
        Assert.Equal(80, e.Disponivel);
        Assert.False(_dao.ListarRetencoes(id: r1).Single().Ativa);
        ExigirEstoqueConsistente();
    }

    [Fact]
    public void Liberar_bloqueio_devolve_ao_disponivel()
    {
        ComEstoque(100);
        int b = _dao.Bloquear(40, "Defeito");

        _dao.Liberar(b);

        Assert.Equal(0, _dao.ConsultarEstoque().Bloqueada);
        Assert.Equal(100, _dao.ConsultarEstoque().Disponivel);
        ExigirEstoqueConsistente();
    }

    [Fact]
    public void Nao_libera_duas_vezes_e_o_saldo_nao_muda_na_segunda_tentativa()
    {
        ComEstoque(100);
        int r = _dao.Reservar(10, "PED-1");
        _dao.Reservar(5, "PED-2");
        _dao.Liberar(r);

        Assert.Throws<EstoqueException>(() => _dao.Liberar(r));

        Assert.Equal(5, _dao.ConsultarEstoque().Reservada);
        ExigirEstoqueConsistente();
    }

    [Fact]
    public void Liberar_identificacao_inexistente_e_recusado()
    {
        Assert.Throws<EstoqueException>(() => _dao.Liberar(12345));
    }

    [Fact]
    public void Apos_liberar_o_estoque_pode_ser_usado_de_novo()
    {
        ComEstoque(10);
        int r = _dao.Reservar(10, "PED-1");

        Assert.Throws<EstoqueException>(() => _dao.Saida(1)); // tudo reservado

        _dao.Liberar(r);
        _dao.Saida(10);

        Assert.Equal(0, _dao.ConsultarEstoque().Total);
    }

    // ------------------------------------------------------------------ Transação e banco

    [Fact]
    public void Falha_no_meio_da_operacao_desfaz_tudo()
    {
        ComEstoque(10);

        // Simula um erro DEPOIS de o saldo do estoque ser gravado: um trigger faz o INSERT
        // da reserva falhar sempre.
        using (var conexao = _conexao.Abrir())
        using (var comando = new Microsoft.Data.Sqlite.SqliteCommand(
            "CREATE TRIGGER falha BEFORE INSERT ON Retencao BEGIN SELECT RAISE(ABORT, 'falha simulada'); END;", conexao))
        {
            comando.ExecuteNonQuery();
        }

        Assert.ThrowsAny<Exception>(() => _dao.Reservar(4, "PED-1"));

        // O UPDATE do estoque (Reservada = 4) já tinha rodado; a transação precisa tê-lo desfeito.
        var e = _dao.ConsultarEstoque();
        Assert.Equal(0, e.Reservada);
        Assert.Equal(10, e.Disponivel);
    }

    [Fact]
    public void Falha_ao_gravar_o_kit_devolve_a_unidade_ao_estoque()
    {
        ComEstoque(10);

        // O estoque é descontado antes do INSERT do kit; se o INSERT falhar, tudo é desfeito.
        using (var conexao = _conexao.Abrir())
        using (var comando = new Microsoft.Data.Sqlite.SqliteCommand(
            "CREATE TRIGGER falha BEFORE INSERT ON Kit BEGIN SELECT RAISE(ABORT, 'falha simulada'); END;", conexao))
        {
            comando.ExecuteNonQuery();
        }

        Assert.ThrowsAny<Exception>(() => _dao.CriarKit("1234567"));

        Assert.Equal(10, _dao.ConsultarEstoque().Total);
        Assert.Empty(_dao.ListarKits());
    }

    [Fact]
    public void O_proprio_banco_recusa_estado_invalido()
    {
        ComEstoque(10);

        using var conexao = _conexao.Abrir();
        using var comando = new Microsoft.Data.Sqlite.SqliteCommand(
            "UPDATE Estoque SET Reservada = 11", conexao);

        Assert.ThrowsAny<Exception>(() => comando.ExecuteNonQuery());
    }

    [Theory]
    [InlineData("123456")]
    [InlineData("12345a7")]
    [InlineData("12345678")]
    public void O_proprio_banco_recusa_funcional_fora_do_padrao(string funcional)
    {
        using var conexao = _conexao.Abrir();
        using var comando = new Microsoft.Data.Sqlite.SqliteCommand(
            "INSERT INTO Kit (Funcional, CriadoEm) VALUES (@Funcional, '2026-01-01')", conexao);
        comando.Parameters.AddWithValue("@Funcional", funcional);

        Assert.ThrowsAny<Exception>(() => comando.ExecuteNonQuery());
    }

    [Fact]
    public void O_proprio_banco_recusa_funcional_repetido()
    {
        ComEstoque(5);
        _dao.CriarKit("1234567");

        using var conexao = _conexao.Abrir();
        using var comando = new Microsoft.Data.Sqlite.SqliteCommand(
            "INSERT INTO Kit (Funcional, CriadoEm) VALUES ('1234567', '2026-01-01')", conexao);

        Assert.ThrowsAny<Exception>(() => comando.ExecuteNonQuery());
    }

    // ------------------------------------------------------------------ Concorrência

    [Fact]
    public void Reservas_simultaneas_nunca_passam_do_disponivel()
    {
        ComEstoque(10);
        int sucessos = 0, recusadas = 0;

        // 30 threads tentam reservar 1 unidade cada, ao mesmo tempo, de um estoque de 10.
        var tarefas = Enumerable.Range(1, 30).Select(n => Task.Run(() =>
        {
            try
            {
                _dao.Reservar(1, "PED-" + n);
                Interlocked.Increment(ref sucessos);
            }
            catch (EstoqueException)
            {
                Interlocked.Increment(ref recusadas);
            }
        })).ToArray();

        Task.WaitAll(tarefas);

        Assert.Equal(10, sucessos);
        Assert.Equal(20, recusadas);
        Assert.Equal(10, _dao.ConsultarEstoque().Reservada);
        Assert.Equal(0, _dao.ConsultarEstoque().Disponivel);
        ExigirEstoqueConsistente();
    }

    [Fact]
    public void Cadastros_simultaneos_de_kits_nunca_passam_do_estoque()
    {
        ComEstoque(5);
        int criados = 0, recusados = 0;

        // 20 funcionais diferentes disputando 5 unidades.
        var tarefas = Enumerable.Range(1, 20).Select(n => Task.Run(() =>
        {
            try
            {
                _dao.CriarKit((2000000 + n).ToString());
                Interlocked.Increment(ref criados);
            }
            catch (EstoqueException)
            {
                Interlocked.Increment(ref recusados);
            }
        })).ToArray();

        Task.WaitAll(tarefas);

        Assert.Equal(5, criados);
        Assert.Equal(15, recusados);
        Assert.Equal(5, _dao.ListarKits().Count);
        Assert.Equal(0, _dao.ConsultarEstoque().Total);
        ExigirEstoqueConsistente();
    }

    [Fact]
    public void Cadastros_simultaneos_do_mesmo_funcional_criam_um_unico_kit()
    {
        ComEstoque(20);
        int criados = 0, recusados = 0;

        var tarefas = Enumerable.Range(1, 20).Select(_ => Task.Run(() =>
        {
            try
            {
                _dao.CriarKit("1234567");
                Interlocked.Increment(ref criados);
            }
            catch (EstoqueException)
            {
                Interlocked.Increment(ref recusados);
            }
        })).ToArray();

        Task.WaitAll(tarefas);

        Assert.Equal(1, criados);
        Assert.Equal(19, recusados);
        Assert.Single(_dao.ListarKits());
        Assert.Equal(19, _dao.ConsultarEstoque().Total); // só uma unidade foi descontada
    }

    [Fact]
    public void Operacoes_mistas_simultaneas_mantem_o_estoque_consistente()
    {
        ComEstoque(50);

        var tarefas = Enumerable.Range(1, 40).Select(n => Task.Run(() =>
        {
            try
            {
                switch (n % 5)
                {
                    case 0: _dao.Reservar(3, "PED-" + n); break;
                    case 1: _dao.Bloquear(2, "Motivo " + n); break;
                    case 2: _dao.Saida(1); break;
                    case 3: _dao.CriarKit((3000000 + n).ToString()); break;
                    default: _dao.Entrada(2); break;
                }
            }
            catch (EstoqueException) { }
        })).ToArray();

        Task.WaitAll(tarefas);

        ExigirEstoqueConsistente();
    }
}

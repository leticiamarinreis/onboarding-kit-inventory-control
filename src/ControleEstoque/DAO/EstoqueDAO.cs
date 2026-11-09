using ControleEstoque.Models;
using Microsoft.Data.Sqlite;

namespace ControleEstoque.DAO;

/// <summary>
/// Acesso ao banco: estoque geral (Entrada, Saída, Reservar, Bloquear, Liberar, Consultar) e cadastro de kits.
///
/// Padrão de TODA operação que altera dados:
///   1. abre uma transação com BEGIN IMMEDIATE (uma escrita por vez no banco inteiro);
///   2. lê o estoque já dentro da transação (saldo atualizado, sem "leitura velha");
///   3. aplica a regra no Model (Estoque.Reservar, Estoque.Saida...), que valida antes de alterar;
///   4. grava; só então faz Commit.
/// Se qualquer passo lançar exceção, a transação não recebe Commit e é desfeita no Dispose:
/// uma operação inválida nunca deixa o estoque pela metade.
/// </summary>
public class EstoqueDAO
{
    private readonly ConexaoDAO _conexao;

    public EstoqueDAO(ConexaoDAO conexao)
    {
        _conexao = conexao;
    }

    // ------------------------------------------------------------------ Consultar

    /// <summary>Consultar: total, disponível, reservada e bloqueada do estoque.</summary>
    public Estoque ConsultarEstoque()
    {
        using var conexao = _conexao.Abrir();
        return LerEstoque(conexao, null);
    }

    public List<Kit> ListarKits()
    {
        using var conexao = _conexao.Abrir();
        using var comando = new SqliteCommand(
            "SELECT IdKit, Funcional, CriadoEm FROM Kit ORDER BY IdKit DESC", conexao);
        using var leitor = comando.ExecuteReader();

        var lista = new List<Kit>();
        while (leitor.Read())
            lista.Add(LerKit(leitor));
        return lista;
    }

    public Kit BuscarKit(int idKit)
    {
        using var conexao = _conexao.Abrir();
        using var comando = new SqliteCommand(
            "SELECT IdKit, Funcional, CriadoEm FROM Kit WHERE IdKit = @IdKit", conexao);
        comando.Parameters.AddWithValue("@IdKit", idKit);
        using var leitor = comando.ExecuteReader();
        return leitor.Read() ? LerKit(leitor) : null;
    }

    /// <summary>Lista reservas/bloqueios. Filtros opcionais: ativas ou liberadas, e identificação.</summary>
    public List<Retencao> ListarRetencoes(bool? ativas = null, int? id = null)
    {
        const string sql = @"
            SELECT Id, Tipo, Quantidade, Referencia, Ativa, CriadaEm, LiberadaEm
            FROM Retencao
            WHERE (@Ativa IS NULL OR Ativa = @Ativa)
              AND (@Id IS NULL OR Id = @Id)
            ORDER BY Id DESC";

        using var conexao = _conexao.Abrir();
        using var comando = new SqliteCommand(sql, conexao);
        comando.Parameters.AddWithValue("@Ativa", ativas.HasValue ? (ativas.Value ? 1 : 0) : DBNull.Value);
        comando.Parameters.AddWithValue("@Id", id.HasValue ? id.Value : DBNull.Value);
        using var leitor = comando.ExecuteReader();

        var lista = new List<Retencao>();
        while (leitor.Read())
            lista.Add(LerRetencao(leitor));
        return lista;
    }

    // ------------------------------------------------------------------ Cadastro do kit

    /// <summary>
    /// Cadastra um kit só com o funcional do colaborador (exatamente 7 números).
    /// Um colaborador só pode ter um kit. O kit "conversa" com o estoque: leva 1 unidade do
    /// disponível; se não houver, o cadastro é recusado e nada muda. Devolve o IdKit gerado.
    /// </summary>
    public int CriarKit(string funcional)
    {
        funcional = (funcional ?? "").Trim();

        if (funcional.Length != 7 || !funcional.All(c => c >= '0' && c <= '9'))
            throw new EstoqueException("O funcional deve ter exatamente 7 números.");

        return Executar((conexao, transacao) =>
        {
            using (var existe = new SqliteCommand("SELECT IdKit FROM Kit WHERE Funcional = @Funcional", conexao, transacao))
            {
                existe.Parameters.AddWithValue("@Funcional", funcional);
                object idExistente = existe.ExecuteScalar();

                if (idExistente != null)
                    throw new EstoqueException($"O colaborador com o funcional {funcional} já tem um kit (IdKit {idExistente}).");
            }

            var estoque = LerEstoque(conexao, transacao);
            estoque.RetirarParaKit();
            SalvarEstoque(conexao, transacao, estoque);

            using var inserir = new SqliteCommand(
                "INSERT INTO Kit (Funcional, CriadoEm) VALUES (@Funcional, @Agora); SELECT last_insert_rowid();",
                conexao, transacao);
            inserir.Parameters.AddWithValue("@Funcional", funcional);
            inserir.Parameters.AddWithValue("@Agora", DateTime.Now);
            return Convert.ToInt32(inserir.ExecuteScalar());
        });
    }

    // ------------------------------------------------------------------ Entrada e Saída

    public void Entrada(int quantidade) =>
        AlterarEstoque(estoque => estoque.Entrada(quantidade));

    public void Saida(int quantidade) =>
        AlterarEstoque(estoque => estoque.Saida(quantidade));

    // ------------------------------------------------------------------ Reservar e Bloquear

    /// <summary>Reserva para uma solicitação/pedido. Devolve a identificação (Id) da reserva.</summary>
    public int Reservar(int quantidade, string solicitacao) =>
        Reter(TipoRetencao.Reserva, quantidade, solicitacao,
              "Informe a solicitação/pedido da reserva.");

    /// <summary>Bloqueio com um motivo. Devolve a identificação (Id) do bloqueio.</summary>
    public int Bloquear(int quantidade, string motivo) =>
        Reter(TipoRetencao.Bloqueio, quantidade, motivo,
              "Informe o motivo do bloqueio.");

    private int Reter(TipoRetencao tipo, int quantidade, string referencia, string mensagemSemReferencia)
    {
        referencia = (referencia ?? "").Trim();

        if (referencia.Length == 0 || referencia.Length > 100)
            throw new EstoqueException(mensagemSemReferencia + " (até 100 caracteres)");

        return Executar((conexao, transacao) =>
        {
            var estoque = LerEstoque(conexao, transacao);

            if (tipo == TipoRetencao.Reserva)
                estoque.Reservar(quantidade);
            else
                estoque.Bloquear(quantidade);

            SalvarEstoque(conexao, transacao, estoque);

            using var inserir = new SqliteCommand(@"
                INSERT INTO Retencao (Tipo, Quantidade, Referencia, Ativa, CriadaEm)
                VALUES (@Tipo, @Quantidade, @Referencia, 1, @Agora);
                SELECT last_insert_rowid();", conexao, transacao);
            inserir.Parameters.AddWithValue("@Tipo", tipo == TipoRetencao.Reserva ? "RESERVA" : "BLOQUEIO");
            inserir.Parameters.AddWithValue("@Quantidade", quantidade);
            inserir.Parameters.AddWithValue("@Referencia", referencia);
            inserir.Parameters.AddWithValue("@Agora", DateTime.Now);
            return Convert.ToInt32(inserir.ExecuteScalar());
        });
    }

    // ------------------------------------------------------------------ Liberar

    /// <summary>Libera uma reserva ou um bloqueio pela identificação. Não libera duas vezes.</summary>
    public void Liberar(int retencaoId)
    {
        Executar((conexao, transacao) =>
        {
            Retencao retencao = BuscarRetencao(conexao, transacao, retencaoId)
                ?? throw new EstoqueException("Reserva/bloqueio não encontrado.");

            if (!retencao.Ativa)
                throw new EstoqueException($"{retencao.Codigo} já foi liberado.");

            var estoque = LerEstoque(conexao, transacao);

            if (retencao.Tipo == TipoRetencao.Reserva)
                estoque.LiberarReserva(retencao.Quantidade);
            else
                estoque.LiberarBloqueio(retencao.Quantidade);

            SalvarEstoque(conexao, transacao, estoque);

            // "AND Ativa = 1" é uma segunda proteção contra liberar duas vezes.
            using var atualizar = new SqliteCommand(
                "UPDATE Retencao SET Ativa = 0, LiberadaEm = @Agora WHERE Id = @Id AND Ativa = 1",
                conexao, transacao);
            atualizar.Parameters.AddWithValue("@Agora", DateTime.Now);
            atualizar.Parameters.AddWithValue("@Id", retencaoId);

            if (atualizar.ExecuteNonQuery() != 1)
                throw new EstoqueException($"{retencao.Codigo} já foi liberado.");

            return 0;
        });
    }

    // ------------------------------------------------------------------ Apoio

    private void AlterarEstoque(Action<Estoque> regra)
    {
        Executar((conexao, transacao) =>
        {
            var estoque = LerEstoque(conexao, transacao);
            regra(estoque);
            SalvarEstoque(conexao, transacao, estoque);
            return 0;
        });
    }

    /// <summary>Roda a ação numa transação. Sem Commit (por erro), o Dispose desfaz tudo.</summary>
    private T Executar<T>(Func<SqliteConnection, SqliteTransaction, T> acao)
    {
        using var conexao = _conexao.Abrir();
        // deferred: false => "BEGIN IMMEDIATE": quem começa primeiro escreve primeiro.
        using var transacao = conexao.BeginTransaction(deferred: false);

        T resultado = acao(conexao, transacao);
        transacao.Commit();
        return resultado;
    }

    private static Estoque LerEstoque(SqliteConnection conexao, SqliteTransaction transacao)
    {
        using var comando = new SqliteCommand(
            "SELECT Total, Reservada, Bloqueada FROM Estoque WHERE Id = 1", conexao, transacao);
        using var leitor = comando.ExecuteReader();

        if (!leitor.Read())
            throw new InvalidOperationException("O estoque não foi criado no banco.");

        return new Estoque
        {
            Total = leitor.GetInt32(0),
            Reservada = leitor.GetInt32(1),
            Bloqueada = leitor.GetInt32(2)
        };
    }

    private static void SalvarEstoque(SqliteConnection conexao, SqliteTransaction transacao, Estoque estoque)
    {
        using var comando = new SqliteCommand(
            "UPDATE Estoque SET Total = @Total, Reservada = @Reservada, Bloqueada = @Bloqueada WHERE Id = 1",
            conexao, transacao);
        comando.Parameters.AddWithValue("@Total", estoque.Total);
        comando.Parameters.AddWithValue("@Reservada", estoque.Reservada);
        comando.Parameters.AddWithValue("@Bloqueada", estoque.Bloqueada);
        comando.ExecuteNonQuery();
    }

    private static Retencao BuscarRetencao(SqliteConnection conexao, SqliteTransaction transacao, int id)
    {
        using var comando = new SqliteCommand(@"
            SELECT Id, Tipo, Quantidade, Referencia, Ativa, CriadaEm, LiberadaEm
            FROM Retencao WHERE Id = @Id", conexao, transacao);
        comando.Parameters.AddWithValue("@Id", id);
        using var leitor = comando.ExecuteReader();
        return leitor.Read() ? LerRetencao(leitor) : null;
    }

    private static Kit LerKit(SqliteDataReader leitor) => new Kit
    {
        IdKit = leitor.GetInt32(0),
        Funcional = leitor.GetString(1),
        CriadoEm = leitor.GetDateTime(2)
    };

    private static Retencao LerRetencao(SqliteDataReader leitor) => new Retencao
    {
        Id = leitor.GetInt32(0),
        Tipo = leitor.GetString(1) == "RESERVA" ? TipoRetencao.Reserva : TipoRetencao.Bloqueio,
        Quantidade = leitor.GetInt32(2),
        Referencia = leitor.GetString(3),
        Ativa = leitor.GetInt32(4) == 1,
        CriadaEm = leitor.GetDateTime(5),
        LiberadaEm = leitor.IsDBNull(6) ? null : leitor.GetDateTime(6)
    };
}

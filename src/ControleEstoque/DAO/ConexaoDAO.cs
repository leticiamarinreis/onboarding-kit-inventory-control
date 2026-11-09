using Microsoft.Data.Sqlite;

namespace ControleEstoque.DAO;

/// <summary>
/// Abre conexões com o SQLite e cria as tabelas na primeira execução.
/// Recebe a connection string pronta (assim os testes usam um banco temporário).
/// </summary>
public class ConexaoDAO
{
    private readonly string _stringConexao;

    public ConexaoDAO(string stringConexao)
    {
        var construtor = new SqliteConnectionStringBuilder(stringConexao)
        {
            // O SQLite só respeita chaves estrangeiras quando isso é ligado em cada conexão.
            ForeignKeys = true
        };

        _stringConexao = construtor.ToString();

        string pasta = Path.GetDirectoryName(Path.GetFullPath(construtor.DataSource));
        if (!string.IsNullOrEmpty(pasta))
            Directory.CreateDirectory(pasta);
    }

    /// <summary>Quem chamar fecha a conexão (using).</summary>
    public SqliteConnection Abrir()
    {
        var conexao = new SqliteConnection(_stringConexao);
        conexao.Open();
        return conexao;
    }

    // Os CHECKs são a "última linha de defesa": mesmo que um bug no código tente gravar
    // um estado inválido, o próprio banco recusa e a transação é desfeita.
    private const string Esquema = @"
-- O estoque geral de kits: uma única linha (Id = 1).
CREATE TABLE IF NOT EXISTS Estoque (
    Id        INTEGER PRIMARY KEY CHECK (Id = 1),
    Total     INTEGER NOT NULL DEFAULT 0 CHECK (Total >= 0),
    Reservada INTEGER NOT NULL DEFAULT 0 CHECK (Reservada >= 0),
    Bloqueada INTEGER NOT NULL DEFAULT 0 CHECK (Bloqueada >= 0),
    CHECK (Reservada + Bloqueada <= Total)
);

INSERT OR IGNORE INTO Estoque (Id, Total, Reservada, Bloqueada) VALUES (1, 0, 0, 0);

-- Os kits só são cadastrados: IdKit automático + funcional do colaborador (7 números, único).
CREATE TABLE IF NOT EXISTS Kit (
    IdKit     INTEGER PRIMARY KEY AUTOINCREMENT,
    Funcional TEXT    NOT NULL UNIQUE CHECK (length(Funcional) = 7 AND Funcional NOT GLOB '*[^0-9]*'),
    CriadoEm  TEXT    NOT NULL
);

CREATE TABLE IF NOT EXISTS Retencao (
    Id         INTEGER PRIMARY KEY AUTOINCREMENT,
    Tipo       TEXT    NOT NULL CHECK (Tipo IN ('RESERVA', 'BLOQUEIO')),
    Quantidade INTEGER NOT NULL CHECK (Quantidade > 0),
    Referencia TEXT    NOT NULL,
    Ativa      INTEGER NOT NULL DEFAULT 1 CHECK (Ativa IN (0, 1)),
    CriadaEm   TEXT    NOT NULL,
    LiberadaEm TEXT    NULL
);

CREATE INDEX IF NOT EXISTS IX_Retencao_Ativa ON Retencao (Ativa);

CREATE TABLE IF NOT EXISTS Usuario (
    Id          INTEGER PRIMARY KEY AUTOINCREMENT,
    NomeUsuario TEXT NOT NULL UNIQUE COLLATE NOCASE,
    SenhaHash   TEXT NOT NULL,
    CriadoEm    TEXT NOT NULL
);";

    /// <summary>Cria as tabelas se ainda não existirem. Pode ser chamado várias vezes.</summary>
    public void CriarBancoSeNecessario()
    {
        using var conexao = Abrir();

        using (var wal = new SqliteCommand("PRAGMA journal_mode = WAL;", conexao))
            wal.ExecuteNonQuery();

        using var comando = new SqliteCommand(Esquema, conexao);
        comando.ExecuteNonQuery();
    }
}

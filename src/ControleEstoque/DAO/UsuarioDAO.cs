using ControleEstoque.Models;
using Microsoft.Data.Sqlite;

namespace ControleEstoque.DAO;

/// <summary>Acesso à tabela Usuario (login). Somente SQL, sem regras.</summary>
public class UsuarioDAO
{
    private readonly ConexaoDAO _conexao;

    public UsuarioDAO(ConexaoDAO conexao)
    {
        _conexao = conexao;
    }

    /// <summary>Devolve null se o usuário não existir.</summary>
    public Usuario BuscarPorNome(string nomeUsuario)
    {
        using var conexao = _conexao.Abrir();
        using var comando = new SqliteCommand(
            "SELECT Id, NomeUsuario, SenhaHash, CriadoEm FROM Usuario WHERE NomeUsuario = @NomeUsuario", conexao);
        comando.Parameters.AddWithValue("@NomeUsuario", nomeUsuario);
        using var leitor = comando.ExecuteReader();

        if (!leitor.Read())
            return null;

        return new Usuario
        {
            Id = leitor.GetInt32(0),
            NomeUsuario = leitor.GetString(1),
            SenhaHash = leitor.GetString(2),
            CriadoEm = leitor.GetDateTime(3)
        };
    }

    public int Contar()
    {
        using var conexao = _conexao.Abrir();
        using var comando = new SqliteCommand("SELECT COUNT(1) FROM Usuario", conexao);
        return Convert.ToInt32(comando.ExecuteScalar());
    }

    public void Inserir(Usuario usuario)
    {
        using var conexao = _conexao.Abrir();
        using var comando = new SqliteCommand(
            "INSERT INTO Usuario (NomeUsuario, SenhaHash, CriadoEm) VALUES (@NomeUsuario, @SenhaHash, @CriadoEm)", conexao);
        comando.Parameters.AddWithValue("@NomeUsuario", usuario.NomeUsuario);
        comando.Parameters.AddWithValue("@SenhaHash", usuario.SenhaHash);
        comando.Parameters.AddWithValue("@CriadoEm", usuario.CriadoEm);
        comando.ExecuteNonQuery();
    }
}

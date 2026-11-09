using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;

namespace ControleEstoque.Models;

/// <summary>
/// Usuário do sistema. Este mesmo Model é usado no formulário de login:
/// NomeUsuario e Senha vêm da tela; SenhaHash é o que fica gravado no banco (a senha nunca é guardada).
/// </summary>
public class Usuario
{
    private const int Iteracoes = 100000;
    private const int TamanhoSal = 16;
    private const int TamanhoHash = 32;

    public int Id { get; set; }

    [Required(ErrorMessage = "Informe o usuário.")]
    [StringLength(50, ErrorMessage = "O usuário deve ter no máximo 50 caracteres.")]
    [Display(Name = "Usuário")]
    public string NomeUsuario { get; set; }

    /// <summary>Só existe durante o login; não é gravada no banco.</summary>
    [Required(ErrorMessage = "Informe a senha.")]
    [DataType(DataType.Password)]
    public string Senha { get; set; }

    /// <summary>Formato gravado: iterações.sal.hash (sal e hash em Base64).</summary>
    public string SenhaHash { get; set; }

    public DateTime CriadoEm { get; set; }

    /// <summary>Gera o hash (PBKDF2 com sal aleatório) da senha informada.</summary>
    public void DefinirSenha(string senha)
    {
        byte[] sal = RandomNumberGenerator.GetBytes(TamanhoSal);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(senha, sal, Iteracoes, HashAlgorithmName.SHA256, TamanhoHash);
        SenhaHash = $"{Iteracoes}.{Convert.ToBase64String(sal)}.{Convert.ToBase64String(hash)}";
    }

    /// <summary>Confere a senha digitada com o hash gravado.</summary>
    public bool SenhaConfere(string senha)
    {
        if (string.IsNullOrEmpty(senha) || string.IsNullOrEmpty(SenhaHash))
            return false;

        string[] partes = SenhaHash.Split('.');

        if (partes.Length != 3 || !int.TryParse(partes[0], out int iteracoes))
            return false;

        try
        {
            byte[] sal = Convert.FromBase64String(partes[1]);
            byte[] esperado = Convert.FromBase64String(partes[2]);
            byte[] calculado = Rfc2898DeriveBytes.Pbkdf2(senha, sal, iteracoes, HashAlgorithmName.SHA256, esperado.Length);
            return CryptographicOperations.FixedTimeEquals(calculado, esperado);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

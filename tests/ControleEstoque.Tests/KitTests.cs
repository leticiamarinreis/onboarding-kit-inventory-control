using ControleEstoque.Models;
using Xunit;

namespace ControleEstoque.Tests;

/// <summary>O único dado do cadastro do kit: o funcional do colaborador (exatamente 7 números).</summary>
public class KitTests
{
    // ---- Funcional: exatamente 7 números ----

    private static IList<System.ComponentModel.DataAnnotations.ValidationResult> Validar(Kit kit)
    {
        var resultados = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
            kit, new System.ComponentModel.DataAnnotations.ValidationContext(kit), resultados, validateAllProperties: true);
        return resultados;
    }

    [Theory]
    [InlineData("1234567")]
    [InlineData("0000001")]
    [InlineData("9999999")]
    public void Funcional_com_7_numeros_e_valido(string funcional)
    {
        var kit = new Kit { Funcional = funcional };

        Assert.Empty(Validar(kit));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("123456")]
    [InlineData("12345678")]
    [InlineData("12345a7")]
    [InlineData("abcdefg")]
    [InlineData("123 567")]
    [InlineData("-123456")]
    public void Funcional_fora_do_padrao_e_invalido(string funcional)
    {
        var kit = new Kit { Funcional = funcional };

        Assert.NotEmpty(Validar(kit));
    }
}

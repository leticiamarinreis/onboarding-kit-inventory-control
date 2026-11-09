using System.ComponentModel.DataAnnotations;

namespace ControleEstoque.Models;

/// <summary>
/// Um kit cadastrado. A única informação do cadastro é o funcional do colaborador
/// (exatamente 7 números); cada colaborador tem no máximo UM kit. O IdKit é gerado pelo banco.
/// Quem controla as quantidades é o <see cref="Estoque"/>.
/// </summary>
public class Kit
{
    public int IdKit { get; set; }

    [Required(ErrorMessage = "Informe o funcional do colaborador.")]
    [RegularExpression(@"^\d{7}$", ErrorMessage = "O funcional deve ter exatamente 7 números.")]
    [Display(Name = "Funcional do colaborador")]
    public string Funcional { get; set; }

    public DateTime CriadoEm { get; set; }
}

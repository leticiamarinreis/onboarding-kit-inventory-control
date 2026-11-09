namespace ControleEstoque.Models;

public enum TipoRetencao
{
    Reserva,
    Bloqueio
}

/// <summary>
/// Uma reserva ou um bloqueio. É o registro que tem identificação própria,
/// para ser consultado e liberado depois. Os dois são iguais na estrutura,
/// por isso usam a mesma classe (e a mesma tabela), separados pelo Tipo.
/// </summary>
public class Retencao
{
    public int Id { get; set; }
    public TipoRetencao Tipo { get; set; }
    public int Quantidade { get; set; }

    /// <summary>Reserva: solicitação/pedido. Bloqueio: motivo.</summary>
    public string Referencia { get; set; }

    public bool Ativa { get; set; }
    public DateTime CriadaEm { get; set; }
    public DateTime? LiberadaEm { get; set; }

    /// <summary>Identificação mostrada ao usuário, ex.: RES-0003 ou BLQ-0001.</summary>
    public string Codigo => (Tipo == TipoRetencao.Reserva ? "RES-" : "BLQ-") + Id.ToString("0000");
}

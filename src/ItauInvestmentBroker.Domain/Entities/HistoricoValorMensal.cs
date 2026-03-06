namespace ItauInvestmentBroker.Domain.Entities;

public class HistoricoValorMensal : BaseEntity
{
    public long ClienteId { get; set; }
    public decimal ValorAnterior { get; set; }
    public decimal ValorNovo { get; set; }
    public DateTime DataAlteracao { get; set; } = DateTime.UtcNow;

    public Cliente? Cliente { get; set; }
}

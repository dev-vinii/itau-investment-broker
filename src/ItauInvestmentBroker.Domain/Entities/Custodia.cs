namespace ItauInvestmentBroker.Domain.Entities;

public class Custodia : BaseEntity
{
    public required string Ticker { get; set; }
    public int Quantidade { get; set; }
    public decimal PrecoMedio { get; set; }
    public DateTime DataUltimaAtualizacao { get; set; } = DateTime.UtcNow;

    public long ContaGraficaId { get; set; }
    public ContaGrafica? ContaGrafica { get; set; }
}

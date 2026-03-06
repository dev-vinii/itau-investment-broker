namespace ItauInvestmentBroker.Domain.Entities;

public class VendaRebalanceamento : BaseEntity
{
    public long ClienteId { get; set; }
    public required string Ticker { get; set; }
    public int Quantidade { get; set; }
    public decimal PrecoVenda { get; set; }
    public decimal PrecoMedio { get; set; }
    public decimal ValorVenda { get; set; }
    public decimal Lucro { get; set; }
    public DateTime DataVenda { get; set; } = DateTime.UtcNow;

    public Cliente? Cliente { get; set; }
}

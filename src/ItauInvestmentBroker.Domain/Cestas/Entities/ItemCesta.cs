using ItauInvestmentBroker.Domain.Common;

namespace ItauInvestmentBroker.Domain.Cestas.Entities;

public class ItemCesta : BaseEntity
{
    public required string Ticker { get; set; }
    public decimal Percentual { get; set; }

    public long CestaId { get; set; }
    public Cesta? Cesta { get; set; }
}

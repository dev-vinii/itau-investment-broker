using ItauInvestmentBroker.Domain.Common;
using ItauInvestmentBroker.Domain.Motor.Enums;

namespace ItauInvestmentBroker.Domain.Motor.Entities;

public class ItemOrdemCompra : BaseEntity
{
    public required string Ticker { get; set; }
    public int Quantidade { get; set; }
    public decimal PrecoUnitario { get; set; }
    public decimal ValorTotal { get; set; }
    public TipoMercado TipoMercado { get; set; }

    public long OrdemCompraId { get; set; }
    public OrdemCompra? OrdemCompra { get; set; }
}

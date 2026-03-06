using ItauInvestmentBroker.Domain.Enums;

namespace ItauInvestmentBroker.Domain.Entities;

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

using ItauInvestmentBroker.Domain.Common;

namespace ItauInvestmentBroker.Domain.Motor.Entities;

public class Distribuicao : BaseEntity
{
    public DateTime DataDistribuicao { get; set; } = DateTime.UtcNow;

    public long OrdemCompraId { get; set; }
    public OrdemCompra? OrdemCompra { get; set; }

    public ICollection<ItemDistribuicao> Itens { get; set; } = new List<ItemDistribuicao>();
}

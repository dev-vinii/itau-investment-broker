using ItauInvestmentBroker.Domain.Common;

namespace ItauInvestmentBroker.Domain.Cestas.Entities;

public class Cesta : BaseEntity
{
    public required string Nome { get; set; }
    public bool Ativa { get; set; } = true;
    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;
    public DateTime? DataDesativacao { get; set; }

    public ICollection<ItemCesta> Itens { get; set; } = new List<ItemCesta>();
}

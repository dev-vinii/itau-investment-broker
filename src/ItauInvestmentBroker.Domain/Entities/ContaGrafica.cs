using ItauInvestmentBroker.Domain.Enums;

namespace ItauInvestmentBroker.Domain.Entities;

public class ContaGrafica : BaseEntity
{
    public required string NumeroConta { get; set; }
    public TipoConta Tipo { get; set; }

    public long ClienteId { get; set; }
    public Cliente? Cliente { get; set; }

    public ICollection<Custodia> Custodias { get; set; } = new List<Custodia>();
    public ICollection<OrdemCompra> OrdensCompra { get; set; } = new List<OrdemCompra>();
}

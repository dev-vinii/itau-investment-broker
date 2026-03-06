using ItauInvestmentBroker.Domain.Cestas.Entities;
using ItauInvestmentBroker.Domain.Clientes.Entities;
using ItauInvestmentBroker.Domain.Common;
using ItauInvestmentBroker.Domain.Motor.Enums;

namespace ItauInvestmentBroker.Domain.Motor.Entities;

public class OrdemCompra : BaseEntity
{
    public DateTime DataExecucao { get; set; }
    public StatusOrdemCompra Status { get; set; } = StatusOrdemCompra.PENDENTE;
    public decimal ValorTotal { get; set; }

    public long ContaGraficaId { get; set; }
    public ContaGrafica? ContaGrafica { get; set; }

    public long CestaId { get; set; }
    public Cesta? Cesta { get; set; }

    public ICollection<ItemOrdemCompra> Itens { get; set; } = new List<ItemOrdemCompra>();
}

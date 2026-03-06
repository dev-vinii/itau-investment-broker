using ItauInvestmentBroker.Domain.Common;
using ItauInvestmentBroker.Domain.Motor.Entities;

namespace ItauInvestmentBroker.Domain.Motor.Repositories;

public interface IVendaRebalanceamentoRepository : IBaseRepository<VendaRebalanceamento>
{
    Task<decimal> SomarVendasMes(long clienteId, int ano, int mes, CancellationToken cancellationToken = default);
    Task<decimal> SomarLucroMes(long clienteId, int ano, int mes, CancellationToken cancellationToken = default);
}

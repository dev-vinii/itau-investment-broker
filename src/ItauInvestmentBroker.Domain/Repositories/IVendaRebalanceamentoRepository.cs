using ItauInvestmentBroker.Domain.Entities;

namespace ItauInvestmentBroker.Domain.Repositories;

public interface IVendaRebalanceamentoRepository : IBaseRepository<VendaRebalanceamento>
{
    Task<decimal> SomarVendasMes(long clienteId, int ano, int mes, CancellationToken cancellationToken = default);
    Task<decimal> SomarLucroMes(long clienteId, int ano, int mes, CancellationToken cancellationToken = default);
}

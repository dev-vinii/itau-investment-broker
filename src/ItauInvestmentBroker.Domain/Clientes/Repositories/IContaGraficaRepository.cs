using ItauInvestmentBroker.Domain.Clientes.Entities;
using ItauInvestmentBroker.Domain.Common;

namespace ItauInvestmentBroker.Domain.Clientes.Repositories;

public interface IContaGraficaRepository : IBaseRepository<ContaGrafica>
{
    Task<ContaGrafica?> FindByClienteId(long clienteId, CancellationToken cancellationToken = default);
    Task<IEnumerable<ContaGrafica>> FindMaster(CancellationToken cancellationToken = default);
    Task<ContaGrafica?> FindByNumeroConta(string numeroConta, CancellationToken cancellationToken = default);
}

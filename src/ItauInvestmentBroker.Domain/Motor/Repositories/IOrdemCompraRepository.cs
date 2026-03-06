using ItauInvestmentBroker.Domain.Common;
using ItauInvestmentBroker.Domain.Motor.Entities;

namespace ItauInvestmentBroker.Domain.Motor.Repositories;

public interface IOrdemCompraRepository : IBaseRepository<OrdemCompra>
{
    Task<OrdemCompra?> FindByIdWithItens(long id, CancellationToken cancellationToken = default);
    Task<IEnumerable<OrdemCompra>> FindByContaGraficaId(long contaGraficaId, CancellationToken cancellationToken = default);
}

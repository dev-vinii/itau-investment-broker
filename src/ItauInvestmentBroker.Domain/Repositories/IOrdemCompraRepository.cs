using ItauInvestmentBroker.Domain.Entities;

namespace ItauInvestmentBroker.Domain.Repositories;

public interface IOrdemCompraRepository : IBaseRepository<OrdemCompra>
{
    Task<OrdemCompra?> FindByIdWithItens(long id, CancellationToken cancellationToken = default);
    Task<IEnumerable<OrdemCompra>> FindByContaGraficaId(long contaGraficaId, CancellationToken cancellationToken = default);
}

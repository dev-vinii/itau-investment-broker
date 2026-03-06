using ItauInvestmentBroker.Domain.Entities;

namespace ItauInvestmentBroker.Domain.Repositories;

public interface IDistribuicaoRepository : IBaseRepository<Distribuicao>
{
    Task<Distribuicao?> FindByOrdemCompraId(long ordemCompraId, CancellationToken cancellationToken = default);
}

using ItauInvestmentBroker.Domain.Common;
using ItauInvestmentBroker.Domain.Motor.Entities;

namespace ItauInvestmentBroker.Domain.Motor.Repositories;

public interface IDistribuicaoRepository : IBaseRepository<Distribuicao>
{
    Task<Distribuicao?> FindByOrdemCompraId(long ordemCompraId, CancellationToken cancellationToken = default);
}

using ItauInvestmentBroker.Domain.Cestas.Entities;
using ItauInvestmentBroker.Domain.Common;

namespace ItauInvestmentBroker.Domain.Cestas.Repositories;

public interface ICestaRepository : IBaseRepository<Cesta>
{
    Task<Cesta?> FindAtiva(CancellationToken cancellationToken = default);
    Task<IEnumerable<Cesta>> FindAllWithItens(CancellationToken cancellationToken = default);
}

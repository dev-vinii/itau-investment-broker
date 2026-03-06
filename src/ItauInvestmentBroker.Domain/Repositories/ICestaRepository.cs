using ItauInvestmentBroker.Domain.Entities;

namespace ItauInvestmentBroker.Domain.Repositories;

public interface ICestaRepository : IBaseRepository<Cesta>
{
    Task<Cesta?> FindAtiva(CancellationToken cancellationToken = default);
    Task<IEnumerable<Cesta>> FindAllWithItens(CancellationToken cancellationToken = default);
}

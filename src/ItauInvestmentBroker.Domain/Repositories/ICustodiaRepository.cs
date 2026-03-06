using ItauInvestmentBroker.Domain.Entities;

namespace ItauInvestmentBroker.Domain.Repositories;

public interface ICustodiaRepository : IBaseRepository<Custodia>
{
    Task<IEnumerable<Custodia>> FindByContaGraficaId(long contaGraficaId, CancellationToken cancellationToken = default);
    Task<Custodia?> FindByContaGraficaIdAndTicker(long contaGraficaId, string ticker, CancellationToken cancellationToken = default);
}

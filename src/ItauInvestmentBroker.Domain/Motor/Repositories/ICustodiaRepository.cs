using ItauInvestmentBroker.Domain.Common;
using ItauInvestmentBroker.Domain.Motor.Entities;

namespace ItauInvestmentBroker.Domain.Motor.Repositories;

public interface ICustodiaRepository : IBaseRepository<Custodia>
{
    Task<IEnumerable<Custodia>> FindByContaGraficaId(long contaGraficaId, CancellationToken cancellationToken = default);
    Task<Custodia?> FindByContaGraficaIdAndTicker(long contaGraficaId, string ticker, CancellationToken cancellationToken = default);
    Task<IEnumerable<Custodia>> FindByContaGraficaIdsAndTickers(
        IEnumerable<long> contaGraficaIds,
        IEnumerable<string> tickers,
        CancellationToken cancellationToken = default);
}

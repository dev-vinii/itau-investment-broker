using ItauInvestmentBroker.Domain.Entities;
using ItauInvestmentBroker.Domain.Repositories;
using ItauInvestmentBroker.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace ItauInvestmentBroker.Infrastructure.Repositories;

public class CustodiaRepository(AppDbContext context) : BaseRepository<Custodia>(context), ICustodiaRepository
{
    public async Task<IEnumerable<Custodia>> FindByContaGraficaId(long contaGraficaId, CancellationToken cancellationToken = default)
    {
        return await DbSet.Where(c => c.ContaGraficaId == contaGraficaId).ToListAsync(cancellationToken);
    }

    public async Task<Custodia?> FindByContaGraficaIdAndTicker(long contaGraficaId, string ticker, CancellationToken cancellationToken = default)
    {
        return await DbSet.FirstOrDefaultAsync(c => c.ContaGraficaId == contaGraficaId && c.Ticker == ticker, cancellationToken);
    }
}

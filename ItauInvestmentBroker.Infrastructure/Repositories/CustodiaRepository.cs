using ItauInvestmentBroker.Domain.Entities;
using ItauInvestmentBroker.Domain.Repositories;
using ItauInvestmentBroker.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace ItauInvestmentBroker.Infrastructure.Repositories;

public class CustodiaRepository : BaseRepository<Custodia>, ICustodiaRepository
{
    public CustodiaRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Custodia>> FindByContaGraficaId(long contaGraficaId)
    {
        return await DbSet.Where(c => c.ContaGraficaId == contaGraficaId).ToListAsync();
    }

    public async Task<Custodia?> FindByContaGraficaIdAndTicker(long contaGraficaId, string ticker)
    {
        return await DbSet.FirstOrDefaultAsync(c => c.ContaGraficaId == contaGraficaId && c.Ticker == ticker);
    }
}

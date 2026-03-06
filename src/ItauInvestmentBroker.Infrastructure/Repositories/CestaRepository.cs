using ItauInvestmentBroker.Domain.Entities;
using ItauInvestmentBroker.Domain.Repositories;
using ItauInvestmentBroker.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace ItauInvestmentBroker.Infrastructure.Repositories;

public class CestaRepository(AppDbContext context) : BaseRepository<Cesta>(context), ICestaRepository
{
    public async Task<Cesta?> FindAtiva(CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(c => c.Itens)
            .FirstOrDefaultAsync(c => c.Ativa, cancellationToken);
    }

    public async Task<IEnumerable<Cesta>> FindAllWithItens(CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(c => c.Itens)
            .OrderByDescending(c => c.DataCriacao)
            .ToListAsync(cancellationToken);
    }
}

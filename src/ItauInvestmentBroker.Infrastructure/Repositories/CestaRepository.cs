using ItauInvestmentBroker.Domain.Cestas.Entities;
using ItauInvestmentBroker.Domain.Clientes.Entities;
using ItauInvestmentBroker.Domain.Motor.Entities;
using ItauInvestmentBroker.Domain.Cestas.Repositories;
using ItauInvestmentBroker.Domain.Clientes.Repositories;
using ItauInvestmentBroker.Domain.Common;
using ItauInvestmentBroker.Domain.Motor.Repositories;
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

    public async Task<Cesta?> FindByIdWithItens(long id, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(c => c.Itens)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<Cesta>> FindAllWithItens(CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(c => c.Itens)
            .OrderByDescending(c => c.DataCriacao)
            .ToListAsync(cancellationToken);
    }
}

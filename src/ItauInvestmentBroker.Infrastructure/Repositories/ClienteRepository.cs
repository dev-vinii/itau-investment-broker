using ItauInvestmentBroker.Domain.Entities;
using ItauInvestmentBroker.Domain.Repositories;
using ItauInvestmentBroker.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace ItauInvestmentBroker.Infrastructure.Repositories;

public class ClienteRepository(AppDbContext context) : BaseRepository<Cliente>(context), IClienteRepository
{
    public async Task<Cliente?> FindByCpf(string cpf, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(c => c.ContaGrafica)
            .FirstOrDefaultAsync(c => c.Cpf == cpf, cancellationToken);
    }

    public async Task<IEnumerable<Cliente>> FindAtivos(CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(c => c.ContaGrafica)
            .Where(c => c.Ativo)
            .ToListAsync(cancellationToken);
    }
}

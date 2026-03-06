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

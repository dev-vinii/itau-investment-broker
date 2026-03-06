using ItauInvestmentBroker.Domain.Entities;
using ItauInvestmentBroker.Domain.Enums;
using ItauInvestmentBroker.Domain.Repositories;
using ItauInvestmentBroker.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace ItauInvestmentBroker.Infrastructure.Repositories;

public class ContaGraficaRepository(AppDbContext context) : BaseRepository<ContaGrafica>(context), IContaGraficaRepository
{
    public async Task<ContaGrafica?> FindByClienteId(long clienteId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(cg => cg.Custodias)
            .FirstOrDefaultAsync(cg => cg.ClienteId == clienteId, cancellationToken);
    }

    public async Task<IEnumerable<ContaGrafica>> FindMaster(CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(cg => cg.Custodias)
            .Where(cg => cg.Tipo == TipoConta.MASTER)
            .ToListAsync(cancellationToken);
    }

    public async Task<ContaGrafica?> FindByNumeroConta(string numeroConta, CancellationToken cancellationToken = default)
    {
        return await DbSet.FirstOrDefaultAsync(cg => cg.NumeroConta == numeroConta, cancellationToken);
    }
}

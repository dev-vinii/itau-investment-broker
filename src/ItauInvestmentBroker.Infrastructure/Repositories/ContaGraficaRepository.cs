using ItauInvestmentBroker.Domain.Cestas.Entities;
using ItauInvestmentBroker.Domain.Clientes.Entities;
using ItauInvestmentBroker.Domain.Motor.Entities;
using ItauInvestmentBroker.Domain.Clientes.Enums;
using ItauInvestmentBroker.Domain.Motor.Enums;
using ItauInvestmentBroker.Domain.Cestas.Repositories;
using ItauInvestmentBroker.Domain.Clientes.Repositories;
using ItauInvestmentBroker.Domain.Common;
using ItauInvestmentBroker.Domain.Motor.Repositories;
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

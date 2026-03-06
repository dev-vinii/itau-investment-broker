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

public class OrdemCompraRepository(AppDbContext context) : BaseRepository<OrdemCompra>(context), IOrdemCompraRepository
{
    public async Task<OrdemCompra?> FindByIdWithItens(long id, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(o => o.Itens)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<OrdemCompra>> FindByContaGraficaId(long contaGraficaId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(o => o.Itens)
            .Where(o => o.ContaGraficaId == contaGraficaId)
            .OrderByDescending(o => o.DataExecucao)
            .ToListAsync(cancellationToken);
    }
}

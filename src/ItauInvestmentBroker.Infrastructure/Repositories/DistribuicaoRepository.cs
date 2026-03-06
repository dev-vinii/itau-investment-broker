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

public class DistribuicaoRepository(AppDbContext context) : BaseRepository<Distribuicao>(context), IDistribuicaoRepository
{
    public async Task<Distribuicao?> FindByOrdemCompraId(long ordemCompraId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(d => d.Itens)
            .FirstOrDefaultAsync(d => d.OrdemCompraId == ordemCompraId, cancellationToken);
    }
}

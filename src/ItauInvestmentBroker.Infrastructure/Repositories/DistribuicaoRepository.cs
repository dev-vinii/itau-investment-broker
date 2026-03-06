using ItauInvestmentBroker.Domain.Entities;
using ItauInvestmentBroker.Domain.Repositories;
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

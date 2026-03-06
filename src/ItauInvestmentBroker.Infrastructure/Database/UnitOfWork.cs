using ItauInvestmentBroker.Domain.Repositories;

namespace ItauInvestmentBroker.Infrastructure.Database;

public class UnitOfWork(AppDbContext context) : IUnitOfWork
{
    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        await context.SaveChangesAsync(cancellationToken);
    }
}

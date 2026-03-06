using ItauInvestmentBroker.Domain.Entities;
using ItauInvestmentBroker.Domain.Repositories;
using ItauInvestmentBroker.Infrastructure.Database;

namespace ItauInvestmentBroker.Infrastructure.Repositories;

public class HistoricoValorMensalRepository(AppDbContext context)
    : BaseRepository<HistoricoValorMensal>(context), IHistoricoValorMensalRepository
{
}

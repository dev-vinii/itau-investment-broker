using ItauInvestmentBroker.Domain.Cestas.Entities;
using ItauInvestmentBroker.Domain.Clientes.Entities;
using ItauInvestmentBroker.Domain.Motor.Entities;
using ItauInvestmentBroker.Domain.Cestas.Repositories;
using ItauInvestmentBroker.Domain.Clientes.Repositories;
using ItauInvestmentBroker.Domain.Common;
using ItauInvestmentBroker.Domain.Motor.Repositories;
using ItauInvestmentBroker.Infrastructure.Database;

namespace ItauInvestmentBroker.Infrastructure.Repositories;

public class HistoricoValorMensalRepository(AppDbContext context)
    : BaseRepository<HistoricoValorMensal>(context), IHistoricoValorMensalRepository
{
}

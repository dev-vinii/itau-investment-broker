using ItauInvestmentBroker.Domain.Entities;
using ItauInvestmentBroker.Domain.Enums;
using ItauInvestmentBroker.Domain.Repositories;
using ItauInvestmentBroker.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace ItauInvestmentBroker.Infrastructure.Repositories;

public class ContaGraficaRepository : BaseRepository<ContaGrafica>, IContaGraficaRepository
{
    public ContaGraficaRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<ContaGrafica?> FindByClienteId(long clienteId)
    {
        return await DbSet.FirstOrDefaultAsync(cg => cg.ClienteId == clienteId);
    }

    public async Task<IEnumerable<ContaGrafica>> FindMaster()
    {
        return await DbSet.Where(cg => cg.Tipo == TipoConta.MASTER).ToListAsync();
    }

    public async Task<ContaGrafica?> FindByNumeroConta(string numeroConta)
    {
        return await DbSet.FirstOrDefaultAsync(cg => cg.NumeroConta == numeroConta);
    }
}

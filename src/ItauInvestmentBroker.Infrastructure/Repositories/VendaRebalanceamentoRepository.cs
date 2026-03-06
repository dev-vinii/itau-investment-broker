using ItauInvestmentBroker.Domain.Entities;
using ItauInvestmentBroker.Domain.Repositories;
using ItauInvestmentBroker.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace ItauInvestmentBroker.Infrastructure.Repositories;

public class VendaRebalanceamentoRepository(AppDbContext context)
    : BaseRepository<VendaRebalanceamento>(context), IVendaRebalanceamentoRepository
{
    public async Task<decimal> SomarVendasMes(long clienteId, int ano, int mes, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(v => v.ClienteId == clienteId && v.DataVenda.Year == ano && v.DataVenda.Month == mes)
            .SumAsync(v => v.ValorVenda, cancellationToken);
    }

    public async Task<decimal> SomarLucroMes(long clienteId, int ano, int mes, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(v => v.ClienteId == clienteId && v.DataVenda.Year == ano && v.DataVenda.Month == mes)
            .SumAsync(v => v.Lucro, cancellationToken);
    }
}

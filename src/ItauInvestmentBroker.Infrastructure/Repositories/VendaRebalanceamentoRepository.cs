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

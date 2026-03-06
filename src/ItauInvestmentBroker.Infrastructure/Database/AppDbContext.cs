using ItauInvestmentBroker.Domain.Cestas.Entities;
using ItauInvestmentBroker.Domain.Clientes.Entities;
using ItauInvestmentBroker.Domain.Common;
using ItauInvestmentBroker.Domain.Motor.Entities;
using Microsoft.EntityFrameworkCore;

namespace ItauInvestmentBroker.Infrastructure.Database;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<ContaGrafica> ContasGraficas => Set<ContaGrafica>();
    public DbSet<Custodia> Custodias => Set<Custodia>();
    public DbSet<Cesta> Cestas => Set<Cesta>();
    public DbSet<ItemCesta> ItensCesta => Set<ItemCesta>();
    public DbSet<OrdemCompra> OrdensCompra => Set<OrdemCompra>();
    public DbSet<ItemOrdemCompra> ItensOrdemCompra => Set<ItemOrdemCompra>();
    public DbSet<Distribuicao> Distribuicoes => Set<Distribuicao>();
    public DbSet<ItemDistribuicao> ItensDistribuicao => Set<ItemDistribuicao>();
    public DbSet<HistoricoValorMensal> HistoricoValorMensal => Set<HistoricoValorMensal>();
    public DbSet<VendaRebalanceamento> VendasRebalanceamento => Set<VendaRebalanceamento>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.UpdatedAt = now;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    break;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}

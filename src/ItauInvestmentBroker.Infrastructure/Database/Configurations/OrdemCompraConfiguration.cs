using ItauInvestmentBroker.Domain.Cestas.Entities;
using ItauInvestmentBroker.Domain.Clientes.Entities;
using ItauInvestmentBroker.Domain.Motor.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ItauInvestmentBroker.Infrastructure.Database.Configurations;

public class OrdemCompraConfiguration : IEntityTypeConfiguration<OrdemCompra>
{
    public void Configure(EntityTypeBuilder<OrdemCompra> builder)
    {
        builder.ToTable("ordens_compra");

        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedOnAdd();

        builder.Property(o => o.DataExecucao);
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(o => o.ValorTotal).HasPrecision(18, 2);

        builder.HasOne(o => o.ContaGrafica)
            .WithMany(cg => cg.OrdensCompra)
            .HasForeignKey(o => o.ContaGraficaId);

        builder.HasOne(o => o.Cesta)
            .WithMany()
            .HasForeignKey(o => o.CestaId);

        builder.HasMany(o => o.Itens)
            .WithOne(i => i.OrdemCompra)
            .HasForeignKey(i => i.OrdemCompraId);
    }
}

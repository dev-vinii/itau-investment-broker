using ItauInvestmentBroker.Domain.Cestas.Entities;
using ItauInvestmentBroker.Domain.Clientes.Entities;
using ItauInvestmentBroker.Domain.Motor.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ItauInvestmentBroker.Infrastructure.Database.Configurations;

public class VendaRebalanceamentoConfiguration : IEntityTypeConfiguration<VendaRebalanceamento>
{
    public void Configure(EntityTypeBuilder<VendaRebalanceamento> builder)
    {
        builder.ToTable("vendas_rebalanceamento");

        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedOnAdd();

        builder.Property(v => v.Ticker).IsRequired().HasMaxLength(20);
        builder.Property(v => v.Quantidade);
        builder.Property(v => v.PrecoVenda).HasColumnType("decimal(18,4)");
        builder.Property(v => v.PrecoMedio).HasColumnType("decimal(18,4)");
        builder.Property(v => v.ValorVenda).HasColumnType("decimal(18,2)");
        builder.Property(v => v.Lucro).HasColumnType("decimal(18,2)");
        builder.Property(v => v.DataVenda);

        builder.HasOne(v => v.Cliente)
            .WithMany()
            .HasForeignKey(v => v.ClienteId);

        builder.HasIndex(v => new { v.ClienteId, v.DataVenda });
    }
}

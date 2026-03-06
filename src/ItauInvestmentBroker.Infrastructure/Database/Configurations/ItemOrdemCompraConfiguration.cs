using ItauInvestmentBroker.Domain.Cestas.Entities;
using ItauInvestmentBroker.Domain.Clientes.Entities;
using ItauInvestmentBroker.Domain.Motor.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ItauInvestmentBroker.Infrastructure.Database.Configurations;

public class ItemOrdemCompraConfiguration : IEntityTypeConfiguration<ItemOrdemCompra>
{
    public void Configure(EntityTypeBuilder<ItemOrdemCompra> builder)
    {
        builder.ToTable("itens_ordem_compra");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedOnAdd();

        builder.Property(i => i.Ticker).IsRequired().HasMaxLength(12);
        builder.Property(i => i.Quantidade);
        builder.Property(i => i.PrecoUnitario).HasPrecision(18, 2);
        builder.Property(i => i.ValorTotal).HasPrecision(18, 2);
        builder.Property(i => i.TipoMercado).HasConversion<string>().HasMaxLength(20);
    }
}

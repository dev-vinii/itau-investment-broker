using ItauInvestmentBroker.Domain.Cestas.Entities;
using ItauInvestmentBroker.Domain.Clientes.Entities;
using ItauInvestmentBroker.Domain.Motor.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ItauInvestmentBroker.Infrastructure.Database.Configurations;

public class ItemDistribuicaoConfiguration : IEntityTypeConfiguration<ItemDistribuicao>
{
    public void Configure(EntityTypeBuilder<ItemDistribuicao> builder)
    {
        builder.ToTable("itens_distribuicao");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedOnAdd();

        builder.Property(i => i.Ticker).IsRequired().HasMaxLength(12);
        builder.Property(i => i.Quantidade);

        builder.HasOne(i => i.ContaGrafica)
            .WithMany()
            .HasForeignKey(i => i.ContaGraficaId);
    }
}

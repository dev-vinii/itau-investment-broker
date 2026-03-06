using ItauInvestmentBroker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ItauInvestmentBroker.Infrastructure.Database.Configurations;

public class CustodiaConfiguration : IEntityTypeConfiguration<Custodia>
{
    public void Configure(EntityTypeBuilder<Custodia> builder)
    {
        builder.ToTable("custodias");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedOnAdd();

        builder.Property(c => c.Ticker).IsRequired().HasMaxLength(20);
        builder.Property(c => c.Quantidade);
        builder.Property(c => c.PrecoMedio).HasColumnType("decimal(18,4)");
        builder.Property(c => c.DataUltimaAtualizacao);
    }
}

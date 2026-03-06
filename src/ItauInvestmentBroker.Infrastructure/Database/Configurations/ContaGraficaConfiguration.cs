using ItauInvestmentBroker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ItauInvestmentBroker.Infrastructure.Database.Configurations;

public class ContaGraficaConfiguration : IEntityTypeConfiguration<ContaGrafica>
{
    public void Configure(EntityTypeBuilder<ContaGrafica> builder)
    {
        builder.ToTable("contas_graficas");

        builder.HasKey(cg => cg.Id);
        builder.Property(cg => cg.Id).ValueGeneratedOnAdd();

        builder.Property(cg => cg.NumeroConta).IsRequired().HasMaxLength(50);
        builder.Property(cg => cg.Tipo).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(cg => cg.NumeroConta).IsUnique();

        builder.HasMany(cg => cg.Custodias)
            .WithOne(c => c.ContaGrafica)
            .HasForeignKey(c => c.ContaGraficaId);
    }
}

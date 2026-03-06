using ItauInvestmentBroker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ItauInvestmentBroker.Infrastructure.Database.Configurations;

public class CestaConfiguration : IEntityTypeConfiguration<Cesta>
{
    public void Configure(EntityTypeBuilder<Cesta> builder)
    {
        builder.ToTable("cestas");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedOnAdd();

        builder.Property(c => c.Nome).IsRequired().HasMaxLength(100);
        builder.Property(c => c.Ativa).HasDefaultValue(true);
        builder.Property(c => c.DataCriacao);
        builder.Property(c => c.DataDesativacao);

        builder.HasMany(c => c.Itens)
            .WithOne(i => i.Cesta)
            .HasForeignKey(i => i.CestaId);
    }
}

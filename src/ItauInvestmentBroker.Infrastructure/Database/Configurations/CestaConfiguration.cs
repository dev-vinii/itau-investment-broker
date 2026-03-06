using ItauInvestmentBroker.Domain.Cestas.Entities;
using ItauInvestmentBroker.Domain.Clientes.Entities;
using ItauInvestmentBroker.Domain.Motor.Entities;
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

        // RN-018: MySQL-safe - permite varias inativas (NULL) e no maximo uma ativa (1).
        builder.Property<int?>("AtivaUnica")
            .HasColumnName("ativa_unica")
            .HasComputedColumnSql("CASE WHEN `Ativa` = 1 THEN 1 ELSE NULL END", stored: true);

        builder.HasIndex("AtivaUnica")
            .IsUnique();

        builder.HasMany(c => c.Itens)
            .WithOne(i => i.Cesta)
            .HasForeignKey(i => i.CestaId);
    }
}

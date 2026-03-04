using ItauInvestmentBroker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ItauInvestmentBroker.Infrastructure.Database.Configurations;

public class ClienteConfiguration : IEntityTypeConfiguration<Cliente>
{
    public void Configure(EntityTypeBuilder<Cliente> builder)
    {
        builder.ToTable("clientes");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedOnAdd();

        builder.Property(c => c.Nome).IsRequired().HasMaxLength(200);
        builder.Property(c => c.Cpf).IsRequired().HasMaxLength(14);
        builder.Property(c => c.Email).IsRequired().HasMaxLength(200);
        builder.Property(c => c.ValorMensal).HasColumnType("decimal(18,2)");
        builder.Property(c => c.Ativo).HasDefaultValue(true);
        builder.Property(c => c.DataAdesao);

        builder.HasIndex(c => c.Cpf).IsUnique();

        builder.HasOne(c => c.ContaGrafica)
            .WithOne(cg => cg.Cliente)
            .HasForeignKey<ContaGrafica>(cg => cg.ClienteId);
    }
}

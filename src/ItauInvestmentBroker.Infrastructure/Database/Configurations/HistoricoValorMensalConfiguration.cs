using ItauInvestmentBroker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ItauInvestmentBroker.Infrastructure.Database.Configurations;

public class HistoricoValorMensalConfiguration : IEntityTypeConfiguration<HistoricoValorMensal>
{
    public void Configure(EntityTypeBuilder<HistoricoValorMensal> builder)
    {
        builder.ToTable("historico_valor_mensal");

        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).ValueGeneratedOnAdd();

        builder.Property(h => h.ValorAnterior).HasColumnType("decimal(18,2)");
        builder.Property(h => h.ValorNovo).HasColumnType("decimal(18,2)");
        builder.Property(h => h.DataAlteracao);

        builder.HasOne(h => h.Cliente)
            .WithMany()
            .HasForeignKey(h => h.ClienteId);

        builder.HasIndex(h => h.ClienteId);
    }
}

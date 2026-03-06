using ItauInvestmentBroker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ItauInvestmentBroker.Infrastructure.Database.Configurations;

public class DistribuicaoConfiguration : IEntityTypeConfiguration<Distribuicao>
{
    public void Configure(EntityTypeBuilder<Distribuicao> builder)
    {
        builder.ToTable("distribuicoes");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedOnAdd();

        builder.Property(d => d.DataDistribuicao);

        builder.HasOne(d => d.OrdemCompra)
            .WithMany()
            .HasForeignKey(d => d.OrdemCompraId);

        builder.HasMany(d => d.Itens)
            .WithOne(i => i.Distribuicao)
            .HasForeignKey(i => i.DistribuicaoId);
    }
}

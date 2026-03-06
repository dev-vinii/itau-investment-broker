using ItauInvestmentBroker.Domain.Cestas.Entities;
using ItauInvestmentBroker.Domain.Clientes.Entities;
using ItauInvestmentBroker.Domain.Motor.Entities;
using ItauInvestmentBroker.Domain.Clientes.Enums;
using ItauInvestmentBroker.Domain.Motor.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ItauInvestmentBroker.Infrastructure.Database;

public static class DatabaseSeeder
{
    private const string MasterCpf = "00000000000";
    private const string MasterNumeroConta = "MST-000001";

    public static async Task SeedAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseSeeder");

        await context.Database.MigrateAsync(cancellationToken);

        var masterExists = await context.ContasGraficas
            .AnyAsync(c => c.Tipo == TipoConta.MASTER, cancellationToken);
        if (masterExists)
            return;

        try
        {
            var clienteTecnico = new Cliente
            {
                Nome = "Conta Master Corretora",
                Cpf = MasterCpf,
                Email = "conta.master@itaubroker.local",
                ValorMensal = 0,
                Ativo = false,
                DataAdesao = DateTime.UtcNow
            };

            clienteTecnico.ContaGrafica = new ContaGrafica
            {
                NumeroConta = MasterNumeroConta,
                Tipo = TipoConta.MASTER
            };

            context.Clientes.Add(clienteTecnico);
            await context.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Conta master criada automaticamente na inicializacao.");
        }
        catch (DbUpdateException ex)
        {
            logger.LogWarning(ex, "Conta master ja existe (criada por outra instancia). Ignorando.");
        }
    }
}

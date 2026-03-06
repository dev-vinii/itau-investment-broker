using ItauInvestmentBroker.Domain.Entities;
using ItauInvestmentBroker.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ItauInvestmentBroker.Infrastructure.Database;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseSeeder");

        await context.Database.MigrateAsync(cancellationToken);

        var masterExists = await context.ContasGraficas.AnyAsync(c => c.Tipo == TipoConta.MASTER, cancellationToken);
        if (masterExists)
            return;

        var cpfBase = 90000000000L;
        string cpf;
        do
        {
            cpf = cpfBase.ToString();
            cpfBase++;
        } while (await context.Clientes.AnyAsync(c => c.Cpf == cpf, cancellationToken));

        var clienteTecnico = new Cliente
        {
            Nome = "Conta Master Corretora",
            Cpf = cpf,
            Email = "conta.master@itaubroker.local",
            ValorMensal = 0,
            Ativo = false,
            DataAdesao = DateTime.UtcNow
        };

        clienteTecnico.ContaGrafica = new ContaGrafica
        {
            // Seed idempotente para garantir funcionamento do motor de compra.
            NumeroConta = "MST-000001",
            Tipo = TipoConta.MASTER
        };

        context.Clientes.Add(clienteTecnico);

        await context.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Conta master criada automaticamente na inicializacao.");
    }
}

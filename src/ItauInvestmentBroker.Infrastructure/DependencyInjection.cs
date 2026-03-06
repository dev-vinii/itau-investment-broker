using ItauInvestmentBroker.Application.Interfaces;
using ItauInvestmentBroker.Domain.Repositories;
using ItauInvestmentBroker.Infrastructure.Database;
using ItauInvestmentBroker.Infrastructure.Kafka;
using ItauInvestmentBroker.Infrastructure.Repositories;
using ItauInvestmentBroker.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ItauInvestmentBroker.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // DbContext
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' nao configurada.");
        services.AddDbContext<AppDbContext>(options =>
            options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

        // Unit of Work
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Repositories
        services.AddScoped<IClienteRepository, ClienteRepository>();
        services.AddScoped<IContaGraficaRepository, ContaGraficaRepository>();
        services.AddScoped<ICustodiaRepository, CustodiaRepository>();
        services.AddScoped<ICestaRepository, CestaRepository>();
        services.AddScoped<IOrdemCompraRepository, OrdemCompraRepository>();
        services.AddScoped<IDistribuicaoRepository, DistribuicaoRepository>();
        services.AddScoped<IHistoricoValorMensalRepository, HistoricoValorMensalRepository>();
        services.AddScoped<IVendaRebalanceamentoRepository, VendaRebalanceamentoRepository>();

        // Services
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddSingleton<ICotacaoService, CotacaoService>();

        // Kafka
        services.AddSingleton<IKafkaProducer, KafkaProducer>();
        services.AddHostedService<KafkaConsumerService>();

        // Health Checks
        services.AddHealthChecks()
            .AddDbContextCheck<AppDbContext>("database");

        return services;
    }
}

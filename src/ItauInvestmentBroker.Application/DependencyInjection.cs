using FluentValidation;
using ItauInvestmentBroker.Application.Configuration;
using ItauInvestmentBroker.Application.Interfaces;
using ItauInvestmentBroker.Application.Services;
using Mapster;
using MapsterMapper;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ItauInvestmentBroker.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        // Configuration
        services.Configure<MotorSettings>(configuration.GetSection(MotorSettings.SectionName));

        // Mapster
        var config = TypeAdapterConfig.GlobalSettings;
        config.Scan(assembly);
        services.AddSingleton(config);
        services.AddScoped<IMapper, ServiceMapper>();

        // FluentValidation
        services.AddValidatorsFromAssembly(assembly);

        // Application Services
        services.AddScoped<CustodiaAppService>();
        services.AddScoped<IrCalculationService>();
        services.AddScoped<KafkaEventPublisher>();

        // Use Cases (auto-scan)
        var useCaseTypes = assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false }
                        && t.Name.EndsWith("UseCase"));

        foreach (var useCaseType in useCaseTypes)
            services.AddScoped(useCaseType);

        // Kafka Handlers (auto-scan)
        var handlerTypes = assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false }
                        && typeof(IKafkaMessageHandler).IsAssignableFrom(t));

        foreach (var handlerType in handlerTypes)
            services.AddScoped(typeof(IKafkaMessageHandler), handlerType);

        return services;
    }
}

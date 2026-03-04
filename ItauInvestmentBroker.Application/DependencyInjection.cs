using ItauInvestmentBroker.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace ItauInvestmentBroker.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        var handlerTypes = assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false }
                        && typeof(IKafkaMessageHandler).IsAssignableFrom(t));

        foreach (var handlerType in handlerTypes) services.AddScoped(typeof(IKafkaMessageHandler), handlerType);

        return services;
    }
}
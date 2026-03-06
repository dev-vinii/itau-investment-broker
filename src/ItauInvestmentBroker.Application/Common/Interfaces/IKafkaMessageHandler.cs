namespace ItauInvestmentBroker.Application.Common.Interfaces;

public interface IKafkaMessageHandler
{
    string Topic { get; }
    Task HandleAsync(string key, string message, CancellationToken cancellationToken);
}

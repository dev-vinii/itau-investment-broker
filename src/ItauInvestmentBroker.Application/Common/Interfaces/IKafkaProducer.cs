namespace ItauInvestmentBroker.Application.Common.Interfaces;

public interface IKafkaProducer
{
    Task ProduceAsync<T>(string topic, string key, T message);
}

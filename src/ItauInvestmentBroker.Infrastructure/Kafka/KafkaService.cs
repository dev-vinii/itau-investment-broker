using System.Text.Json;
using Confluent.Kafka;
using ItauInvestmentBroker.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ItauInvestmentBroker.Infrastructure.Kafka;

public class KafkaProducer : IKafkaProducer, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaProducer> _logger;
    private const int MaxRetries = 3;

    public KafkaProducer(IConfiguration configuration, ILogger<KafkaProducer> logger)
    {
        _logger = logger;

        var config = new ProducerConfig
        {
            BootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:9092",
            MessageSendMaxRetries = MaxRetries,
            RetryBackoffMs = 500,
            Acks = Acks.All
        };

        _producer = new ProducerBuilder<string, string>(config).Build();
    }

    public async Task ProduceAsync<T>(string topic, string key, T message)
    {
        var json = JsonSerializer.Serialize(message);

        try
        {
            var result = await _producer.ProduceAsync(topic, new Message<string, string>
            {
                Key = key,
                Value = json
            });

            _logger.LogInformation(
                "Mensagem enviada para o topico {Topic} com chave {Key}, offset {Offset}",
                topic, key, result.Offset);
        }
        catch (ProduceException<string, string> ex)
        {
            _logger.LogError(ex,
                "Falha ao enviar mensagem para o topico {Topic} com chave {Key}: {Reason}",
                topic, key, ex.Error.Reason);
            throw;
        }
    }

    public void Dispose()
    {
        _producer?.Dispose();
        GC.SuppressFinalize(this);
    }
}

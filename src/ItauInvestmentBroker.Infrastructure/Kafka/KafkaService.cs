using System.Text.Json;
using Confluent.Kafka;
using ItauInvestmentBroker.Application.Common.Interfaces;
using ItauInvestmentBroker.Infrastructure.Common.Constants;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ItauInvestmentBroker.Infrastructure.Kafka;

public class KafkaProducer : IKafkaProducer, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaProducer> _logger;

    public KafkaProducer(IConfiguration configuration, ILogger<KafkaProducer> logger)
    {
        _logger = logger;

        var maxRetries = GetInt(configuration, KafkaConstants.ProducerMessageSendMaxRetriesKey, KafkaConstants.DefaultMessageSendMaxRetries);
        var retryBackoffMs = GetInt(configuration, KafkaConstants.ProducerRetryBackoffMsKey, KafkaConstants.DefaultRetryBackoffMs);
        var messageTimeoutMs = GetInt(configuration, KafkaConstants.ProducerMessageTimeoutMsKey, KafkaConstants.DefaultMessageTimeoutMs);
        var requestTimeoutMs = GetInt(configuration, KafkaConstants.ProducerRequestTimeoutMsKey, KafkaConstants.DefaultRequestTimeoutMs);
        var enableIdempotence = GetBool(configuration, KafkaConstants.ProducerEnableIdempotenceKey, true);

        var config = new ProducerConfig
        {
            BootstrapServers = configuration[KafkaConstants.BootstrapServersKey] ?? KafkaConstants.DefaultBootstrapServers,
            Acks = Acks.All,
            EnableIdempotence = enableIdempotence,
            MessageSendMaxRetries = maxRetries,
            RetryBackoffMs = retryBackoffMs,
            MessageTimeoutMs = messageTimeoutMs,
            RequestTimeoutMs = requestTimeoutMs,
            MaxInFlight = KafkaConstants.MaxInFlight,
            CompressionType = CompressionType.Snappy,
            SocketKeepaliveEnable = true
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

    private static int GetInt(IConfiguration configuration, string key, int defaultValue)
    {
        var raw = configuration[key];
        return int.TryParse(raw, out var value) ? value : defaultValue;
    }

    private static bool GetBool(IConfiguration configuration, string key, bool defaultValue)
    {
        var raw = configuration[key];
        return bool.TryParse(raw, out var value) ? value : defaultValue;
    }
}

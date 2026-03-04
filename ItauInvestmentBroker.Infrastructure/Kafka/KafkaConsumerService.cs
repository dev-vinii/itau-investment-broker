using Confluent.Kafka;
using ItauInvestmentBroker.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ItauInvestmentBroker.Infrastructure.Kafka;

public class KafkaConsumerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConsumer<string, string> _consumer;
    private readonly ILogger<KafkaConsumerService> _logger;
    private readonly string[] _topics;

    public KafkaConsumerService(
        IConfiguration configuration,
        IServiceScopeFactory scopeFactory,
        IEnumerable<IKafkaMessageHandler> handlers,
        ILogger<KafkaConsumerService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _topics = handlers.Select(h => h.Topic).Distinct().ToArray();

        var config = new ConsumerConfig
        {
            BootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:9092",
            GroupId = configuration["Kafka:GroupId"] ?? "itau-investment-broker",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = true
        };

        _consumer = new ConsumerBuilder<string, string>(config).Build();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_topics.Length == 0)
        {
            _logger.LogWarning("Nenhum handler de Kafka registrado. Consumer não será iniciado.");
            return;
        }

        _consumer.Subscribe(_topics);
        _logger.LogInformation("Kafka consumer inscrito nos tópicos: {Topics}", string.Join(", ", _topics));

        await Task.Run(() => ConsumeLoop(stoppingToken), stoppingToken);
    }

    private void ConsumeLoop(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = _consumer.Consume(stoppingToken);

                _logger.LogInformation(
                    "Mensagem recebida do tópico {Topic} com chave {Key}",
                    result.Topic, result.Message.Key);

                using var scope = _scopeFactory.CreateScope();
                var handlers = scope.ServiceProvider.GetServices<IKafkaMessageHandler>();
                var handler = handlers.FirstOrDefault(h => h.Topic == result.Topic);

                handler?.HandleAsync(result.Message.Key, result.Message.Value, stoppingToken)
                    .GetAwaiter().GetResult();
            }
            catch (ConsumeException ex)
            {
                _logger.LogError(ex, "Erro ao consumir mensagem do Kafka");
            }
        }
    }

    public override void Dispose()
    {
        _consumer.Close();
        _consumer.Dispose();
        base.Dispose();
    }
}

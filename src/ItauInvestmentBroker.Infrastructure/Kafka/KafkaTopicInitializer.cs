using Confluent.Kafka;
using Confluent.Kafka.Admin;
using ItauInvestmentBroker.Application.Common.Constants;
using ItauInvestmentBroker.Infrastructure.Common.Constants;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ItauInvestmentBroker.Infrastructure.Kafka;

public class KafkaTopicInitializer(
    IConfiguration configuration,
    ILogger<KafkaTopicInitializer> logger) : IHostedService
{
    private static readonly string[] Topics =
    [
        KafkaTopicNames.IrDedoDuro,
        KafkaTopicNames.IrVenda,
        KafkaTopicNames.OrdemCompraExecutada,
        KafkaTopicNames.MotorExecucaoFalhou,
        KafkaTopicNames.RebalanceamentoCarteira
    ];

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var bootstrapServers = configuration[KafkaConstants.BootstrapServersKey]
                               ?? KafkaConstants.DefaultBootstrapServers;

        using var adminClient = new AdminClientBuilder(
            new AdminClientConfig { BootstrapServers = bootstrapServers }).Build();

        var metadata = adminClient.GetMetadata(TimeSpan.FromSeconds(10));
        var existingTopics = metadata.Topics.Select(t => t.Topic).ToHashSet();

        var topicsToCreate = Topics
            .Where(t => !existingTopics.Contains(t))
            .Select(t => new TopicSpecification { Name = t, NumPartitions = 2, ReplicationFactor = 1 })
            .ToList();

        if (topicsToCreate.Count == 0)
        {
            logger.LogInformation("Todos os topicos Kafka ja existem");
            return;
        }

        await adminClient.CreateTopicsAsync(topicsToCreate);
        logger.LogInformation("Topicos Kafka criados: {Topics}",
            string.Join(", ", topicsToCreate.Select(t => t.Name)));
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

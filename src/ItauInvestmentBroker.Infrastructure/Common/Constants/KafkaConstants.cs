namespace ItauInvestmentBroker.Infrastructure.Common.Constants;

public static class KafkaConstants
{
    public const string BootstrapServersKey = "Kafka:BootstrapServers";
    public const string GroupIdKey = "Kafka:GroupId";
    public const string ProducerMessageSendMaxRetriesKey = "Kafka:Producer:MessageSendMaxRetries";
    public const string ProducerRetryBackoffMsKey = "Kafka:Producer:RetryBackoffMs";
    public const string ProducerMessageTimeoutMsKey = "Kafka:Producer:MessageTimeoutMs";
    public const string ProducerRequestTimeoutMsKey = "Kafka:Producer:RequestTimeoutMs";
    public const string ProducerEnableIdempotenceKey = "Kafka:Producer:EnableIdempotence";

    public const string DefaultBootstrapServers = "localhost:9092";
    public const string DefaultGroupId = "itau-investment-broker";

    public const int DefaultMessageSendMaxRetries = 10;
    public const int DefaultRetryBackoffMs = 1000;
    public const int DefaultMessageTimeoutMs = 120000;
    public const int DefaultRequestTimeoutMs = 30000;
    public const int MaxInFlight = 5;
}

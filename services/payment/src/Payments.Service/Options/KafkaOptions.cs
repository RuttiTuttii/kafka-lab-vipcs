namespace Payments.Service.Options;

// настройки брокера, секция Kafka в appsettings
public sealed class KafkaOptions
{
    public string BootstrapServers { get; init; } = "10.9.2.80:9092";
    public string TopicSuffix { get; init; } = "2";
    public string GroupId { get; init; } = "service2";

    // имя топика всегда собирается тут, суффикс в коде не хардкодим
    public string Topic(string baseName) => baseName + TopicSuffix;
}

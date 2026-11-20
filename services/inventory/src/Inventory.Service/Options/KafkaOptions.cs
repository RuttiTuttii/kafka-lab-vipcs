namespace Inventory.Service.Options;

// настройки брокера, секция Kafka в appsettings
public sealed class KafkaOptions
{
    public string BootstrapServers { get; init; } = "localhost:9092";
    public string TopicSuffix { get; init; } = "2";
    public string GroupId { get; init; } = "inventory-service2";

    // имя топика всегда собирается тут, в коде суффикс не хардкодим
    public string Topic(string baseName) => baseName + TopicSuffix;
}

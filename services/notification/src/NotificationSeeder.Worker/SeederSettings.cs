namespace NotificationSeeder.Worker;

public class SeederSettings
{
    public const string SectionName = "Kafka";

    public string BootstrapServers { get; set; } = "10.9.2.80:9092";
    public string TopicSuffix { get; set; } = "";
    public int DelayBetweenStepsMs { get; set; } = 1000;

    public string ResolveTopic(string baseTopic)
    {
        var suffix = TopicSuffix?.Trim() ?? string.Empty;
        if (!string.IsNullOrEmpty(suffix) && !baseTopic.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            return $"{baseTopic}{suffix}";
        }
        return baseTopic;
    }
}

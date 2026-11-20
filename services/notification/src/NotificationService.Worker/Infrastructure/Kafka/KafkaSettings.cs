namespace NotificationService.Worker.Infrastructure.Kafka;

public class KafkaSettings
{
    public const string SectionName = "Kafka";

    public string BootstrapServers { get; set; } = "10.9.2.80:9092";
    public string GroupId { get; set; } = "notification-service";
    public string TopicSuffix { get; set; } = "";
    public string AutoOffsetReset { get; set; } = "Earliest";
    public bool EnableAutoCommit { get; set; } = false;
    public List<string> Topics { get; set; } = new()
    {
        "order.created",
        "payment.completed",
        "payment.failed",
        "order.cancelled"
    };

    public IEnumerable<string> ResolveTopics()
    {
        var suffix = TopicSuffix?.Trim() ?? string.Empty;
        var resolved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var topic in Topics)
        {
            var baseName = topic.Trim();
            if (!string.IsNullOrEmpty(suffix) && !baseName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                resolved.Add($"{baseName}{suffix}");
            }
            else
            {
                resolved.Add(baseName);
            }
        }

        return resolved;
    }

    public string ResolveGroupId()
    {
        var suffix = TopicSuffix?.Trim() ?? string.Empty;
        var baseGroup = string.IsNullOrWhiteSpace(GroupId) ? "notification-service" : GroupId.Trim();
        if (!string.IsNullOrEmpty(suffix) && !baseGroup.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            return $"{baseGroup}{suffix}";
        }
        return baseGroup;
    }
}

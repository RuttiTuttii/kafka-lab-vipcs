namespace NotificationService.Worker.Domain;

public class Notification
{
    public Guid NotificationId { get; init; } = Guid.NewGuid();
    public Guid EventId { get; init; }
    public Guid UserId { get; init; }
    public Guid OrderId { get; init; }
    public NotificationType Type { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public string DeliveryStatus { get; init; } = "Delivered";

    // Parameterless constructor for serialization
    public Notification() { }

    public Notification(
        Guid eventId,
        Guid userId,
        Guid orderId,
        NotificationType type,
        string title,
        string message,
        DateTimeOffset? createdAt = null,
        string deliveryStatus = "Delivered")
    {
        NotificationId = Guid.NewGuid();
        EventId = eventId;
        UserId = userId;
        OrderId = orderId;
        Type = type;
        Title = title;
        Message = message;
        CreatedAt = createdAt ?? DateTimeOffset.UtcNow;
        DeliveryStatus = deliveryStatus;
    }
}

using System.Text.Json.Serialization;

namespace NotificationService.Worker.Domain;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NotificationType
{
    OrderCreated,
    PaymentCompleted,
    PaymentFailed,
    OrderCancelled,
    OrderStatusChanged
}

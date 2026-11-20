using NotificationService.Worker.Domain;

namespace NotificationService.Worker.Application;

public interface INotificationProcessor
{
    Task<Notification?> ProcessMessageAsync(
        string topic,
        string? messageKey,
        string jsonPayload,
        CancellationToken cancellationToken = default);
}

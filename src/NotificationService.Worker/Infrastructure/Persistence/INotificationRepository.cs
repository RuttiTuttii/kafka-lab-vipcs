using NotificationService.Worker.Domain;

namespace NotificationService.Worker.Infrastructure.Persistence;

public record NotificationSummaryStats(
    int TotalCount,
    int UniqueUsersCount,
    int UniqueOrdersCount,
    IReadOnlyDictionary<NotificationType, int> TypeBreakdown,
    DateTimeOffset? OldestNotification,
    DateTimeOffset? LatestNotification
);

public interface INotificationRepository
{
    Task<bool> HasEventBeenProcessedAsync(Guid eventId, CancellationToken cancellationToken = default);
    Task<bool> AddAsync(Notification notification, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Notification>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Notification>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Notification>> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task<NotificationSummaryStats> GetStatsAsync(CancellationToken cancellationToken = default);
}

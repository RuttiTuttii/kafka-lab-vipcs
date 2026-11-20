using NotificationService.Worker.Domain;
using NotificationService.Worker.Infrastructure.Persistence;

namespace NotificationService.Worker.Application;

public interface INotificationHistoryService
{
    Task<IReadOnlyList<Notification>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Notification>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Notification>> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task<NotificationSummaryStats> GetStatsAsync(CancellationToken cancellationToken = default);
    Task LogSummaryStatisticsAsync(CancellationToken cancellationToken = default);
}

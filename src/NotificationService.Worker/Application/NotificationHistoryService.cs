using Microsoft.Extensions.Logging;
using NotificationService.Worker.Domain;
using NotificationService.Worker.Infrastructure.Persistence;

namespace NotificationService.Worker.Application;

public class NotificationHistoryService : INotificationHistoryService
{
    private readonly INotificationRepository _repository;
    private readonly ILogger<NotificationHistoryService> _logger;

    public NotificationHistoryService(
        INotificationRepository repository,
        ILogger<NotificationHistoryService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public Task<IReadOnlyList<Notification>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return _repository.GetAllAsync(cancellationToken);
    }

    public Task<IReadOnlyList<Notification>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return _repository.GetByUserIdAsync(userId, cancellationToken);
    }

    public Task<IReadOnlyList<Notification>> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        return _repository.GetByOrderIdAsync(orderId, cancellationToken);
    }

    public Task<NotificationSummaryStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        return _repository.GetStatsAsync(cancellationToken);
    }

    public async Task LogSummaryStatisticsAsync(CancellationToken cancellationToken = default)
    {
        var stats = await _repository.GetStatsAsync(cancellationToken);

        var breakdownStr = stats.TypeBreakdown.Count > 0
            ? string.Join(", ", stats.TypeBreakdown.Select(kv => $"{kv.Key}: {kv.Value}"))
            : "None";

        _logger.LogInformation(
            "\n" +
            "--------------------------- [NOTIFICATION STATS] ---------------------------\n" +
            " Total Notifications: {TotalCount}\n" +
            " Unique Users:         {UniqueUsers}\n" +
            " Unique Orders:        {UniqueOrders}\n" +
            " Type Breakdown:       {Breakdown}\n" +
            " Latest Activity:      {Latest}\n" +
            "----------------------------------------------------------------------------",
            stats.TotalCount,
            stats.UniqueUsersCount,
            stats.UniqueOrdersCount,
            breakdownStr,
            stats.LatestNotification?.ToString("u") ?? "No records yet");
    }
}

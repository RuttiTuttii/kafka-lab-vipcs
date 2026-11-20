using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NotificationService.Worker.Contracts;
using NotificationService.Worker.Domain;

namespace NotificationService.Worker.Infrastructure.Persistence;

public class JsonNotificationRepository : INotificationRepository
{
    private readonly string _filePath;
    private readonly ILogger<JsonNotificationRepository> _logger;
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    // Fast in-memory thread-safe indexes
    private readonly ConcurrentDictionary<Guid, Notification> _byId = new();
    private readonly ConcurrentDictionary<Guid, byte> _processedEventIds = new();
    private readonly ConcurrentDictionary<Guid, ConcurrentBag<Guid>> _byUserIdIndex = new();
    private readonly ConcurrentDictionary<Guid, ConcurrentBag<Guid>> _byOrderIdIndex = new();

    public JsonNotificationRepository(
        IConfiguration configuration,
        ILogger<JsonNotificationRepository> _logger)
    {
        this._logger = _logger;
        var configPath = configuration.GetValue<string>("Persistence:StoragePath") 
                         ?? Path.Combine("data", "notifications-history.json");

        _filePath = Path.IsPathRooted(configPath)
            ? configPath
            : Path.Combine(AppContext.BaseDirectory, configPath);

        EnsureDirectoryExists();
        LoadExistingData();
    }

    private void EnsureDirectoryExists()
    {
        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }

    private void LoadExistingData()
    {
        if (!File.Exists(_filePath))
        {
            _logger.LogInformation("No existing notifications history file found at '{FilePath}'. Starting fresh.", _filePath);
            return;
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            var loaded = JsonSerializer.Deserialize<List<Notification>>(json, JsonDefaults.Options);
            if (loaded == null)
            {
                return;
            }

            foreach (var item in loaded)
            {
                _byId[item.NotificationId] = item;
                if (item.EventId != Guid.Empty)
                {
                    _processedEventIds.TryAdd(item.EventId, 0);
                }

                _byUserIdIndex.GetOrAdd(item.UserId, _ => new ConcurrentBag<Guid>()).Add(item.NotificationId);
                _byOrderIdIndex.GetOrAdd(item.OrderId, _ => new ConcurrentBag<Guid>()).Add(item.NotificationId);
            }

            _logger.LogInformation("Successfully restored {Count} notifications from history store '{FilePath}'", _byId.Count, _filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load notification history from '{FilePath}'. Starting with empty cache.", _filePath);
        }
    }

    public Task<bool> HasEventBeenProcessedAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        if (eventId == Guid.Empty)
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(_processedEventIds.ContainsKey(eventId));
    }

    public async Task<bool> AddAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        // Check deduplication
        if (notification.EventId != Guid.Empty)
        {
            if (!_processedEventIds.TryAdd(notification.EventId, 0))
            {
                _logger.LogWarning("Duplicate event detected! EventId={EventId} for OrderId={OrderId} has already been processed.", 
                    notification.EventId, notification.OrderId);
                return false;
            }
        }

        _byId[notification.NotificationId] = notification;
        _byUserIdIndex.GetOrAdd(notification.UserId, _ => new ConcurrentBag<Guid>()).Add(notification.NotificationId);
        _byOrderIdIndex.GetOrAdd(notification.OrderId, _ => new ConcurrentBag<Guid>()).Add(notification.NotificationId);

        // Durable persistence to JSON file
        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            EnsureDirectoryExists();
            var allNotifications = _byId.Values.OrderBy(n => n.CreatedAt).ToList();
            var tempPath = _filePath + ".tmp";
            var json = JsonSerializer.Serialize(allNotifications, JsonDefaults.Options);

            await File.WriteAllTextAsync(tempPath, json, cancellationToken);
            File.Move(tempPath, _filePath, overwrite: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error persisting notification history to '{FilePath}'", _filePath);
            throw;
        }
        finally
        {
            _fileLock.Release();
        }

        return true;
    }

    public Task<IReadOnlyList<Notification>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Notification> list = _byId.Values.OrderByDescending(n => n.CreatedAt).ToList();
        return Task.FromResult(list);
    }

    public Task<IReadOnlyList<Notification>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (!_byUserIdIndex.TryGetValue(userId, out var notificationIds))
        {
            return Task.FromResult<IReadOnlyList<Notification>>(Array.Empty<Notification>());
        }

        var list = notificationIds
            .Select(id => _byId.TryGetValue(id, out var n) ? n : null)
            .Where(n => n != null)
            .Cast<Notification>()
            .OrderByDescending(n => n.CreatedAt)
            .ToList();

        return Task.FromResult<IReadOnlyList<Notification>>(list);
    }

    public Task<IReadOnlyList<Notification>> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        if (!_byOrderIdIndex.TryGetValue(orderId, out var notificationIds))
        {
            return Task.FromResult<IReadOnlyList<Notification>>(Array.Empty<Notification>());
        }

        var list = notificationIds
            .Select(id => _byId.TryGetValue(id, out var n) ? n : null)
            .Where(n => n != null)
            .Cast<Notification>()
            .OrderByDescending(n => n.CreatedAt)
            .ToList();

        return Task.FromResult<IReadOnlyList<Notification>>(list);
    }

    public Task<NotificationSummaryStats> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        var items = _byId.Values.ToList();
        var total = items.Count;
        var uniqueUsers = items.Select(x => x.UserId).Distinct().Count();
        var uniqueOrders = items.Select(x => x.OrderId).Distinct().Count();

        var breakdown = items
            .GroupBy(x => x.Type)
            .ToDictionary(g => g.Key, g => g.Count());

        DateTimeOffset? oldest = items.Count > 0 ? items.Min(x => x.CreatedAt) : null;
        DateTimeOffset? latest = items.Count > 0 ? items.Max(x => x.CreatedAt) : null;

        var stats = new NotificationSummaryStats(
            TotalCount: total,
            UniqueUsersCount: uniqueUsers,
            UniqueOrdersCount: uniqueOrders,
            TypeBreakdown: breakdown,
            OldestNotification: oldest,
            LatestNotification: latest
        );

        return Task.FromResult(stats);
    }
}

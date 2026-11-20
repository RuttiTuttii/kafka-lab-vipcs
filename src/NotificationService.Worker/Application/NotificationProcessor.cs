using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NotificationService.Worker.Contracts;
using NotificationService.Worker.Domain;
using NotificationService.Worker.Infrastructure.Persistence;

namespace NotificationService.Worker.Application;

public class NotificationProcessor : INotificationProcessor
{
    private readonly INotificationRepository _repository;
    private readonly ILogger<NotificationProcessor> _logger;

    public NotificationProcessor(
        INotificationRepository repository,
        ILogger<NotificationProcessor> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Notification?> ProcessMessageAsync(
        string topic,
        string? messageKey,
        string jsonPayload,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jsonPayload))
        {
            _logger.LogWarning("Received empty or whitespace payload on topic {Topic}", topic);
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(jsonPayload);
            var root = doc.RootElement;

            // Extract EventId from either envelope or root
            var eventId = ExtractEventId(root, jsonPayload);

            // Check deduplication early
            if (await _repository.HasEventBeenProcessedAsync(eventId, cancellationToken))
            {
                _logger.LogInformation(
                    "[IDEMPOTENCY SKIP] EventId={EventId} on topic={Topic} has already been processed. Ignoring duplicate message.",
                    eventId, topic);
                return null;
            }

            // Normalize topic (stripping suffix like '2' for matching)
            var cleanTopic = NormalizeTopic(topic);

            var notification = cleanTopic switch
            {
                "order.created" => CreateOrderCreatedNotification(root, eventId, messageKey),
                "payment.completed" => CreatePaymentCompletedNotification(root, eventId, messageKey),
                "payment.failed" => CreatePaymentFailedNotification(root, eventId, messageKey),
                "order.cancelled" => CreateOrderCancelledNotification(root, eventId, messageKey),
                "order.status.changed" => CreateOrderStatusChangedNotification(root, eventId, messageKey),
                _ => throw new InvalidOperationException($"Unsupported topic: {topic}")
            };

            if (notification == null)
            {
                _logger.LogWarning("Failed to create notification from payload on topic {Topic}", topic);
                return null;
            }

            // Persist to history repository
            var added = await _repository.AddAsync(notification, cancellationToken);
            if (!added)
            {
                _logger.LogInformation(
                    "[IDEMPOTENCY SKIP] Race condition resolved: EventId={EventId} was just stored by another thread.",
                    eventId);
                return null;
            }

            // Structured console output simulating dispatch to the user
            _logger.LogInformation(
                "\n" +
                "==========================================================================================\n" +
                " [NOTIFICATION DISPATCHED TO USER]\n" +
                " NotificationId: {NotificationId}\n" +
                " EventId:        {EventId}\n" +
                " Recipient:      User #{UserId}\n" +
                " Target Order:   Order #{OrderId}\n" +
                " Type:           {Type}\n" +
                " Title:          \"{Title}\"\n" +
                " Message:        \"{Message}\"\n" +
                " DeliveryStatus: {DeliveryStatus}\n" +
                " Timestamp:      {CreatedAt:u}\n" +
                "==========================================================================================",
                notification.NotificationId,
                notification.EventId,
                notification.UserId,
                notification.OrderId,
                notification.Type,
                notification.Title,
                notification.Message,
                notification.DeliveryStatus,
                notification.CreatedAt);

            return notification;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing event from topic {Topic}. Content: {Json}", topic, jsonPayload);
            throw;
        }
    }

    private static string NormalizeTopic(string topic)
    {
        var clean = topic.Trim();
        if (clean.EndsWith("2", StringComparison.OrdinalIgnoreCase))
        {
            clean = clean[..^1];
        }
        return clean.ToLowerInvariant();
    }

    private static Guid ExtractEventId(JsonElement root, string rawJson)
    {
        if (root.TryGetProperty("eventId", out var eventIdProp) && eventIdProp.TryGetGuid(out var id))
        {
            return id;
        }

        if (root.TryGetProperty("payload", out var payloadElem) &&
            payloadElem.TryGetProperty("eventId", out var innerIdProp) &&
            innerIdProp.TryGetGuid(out var innerId))
        {
            return innerId;
        }

        // Fallback: create deterministic Guid from content hash
        using var md5 = System.Security.Cryptography.MD5.Create();
        var hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(rawJson));
        return new Guid(hash);
    }

    private static JsonElement GetEffectiveElement(JsonElement root)
    {
        if (root.TryGetProperty("payload", out var payloadElem) && payloadElem.ValueKind == JsonValueKind.Object)
        {
            return payloadElem;
        }
        return root;
    }

    private static Notification? CreateOrderCreatedNotification(JsonElement root, Guid eventId, string? key)
    {
        var elem = GetEffectiveElement(root);
        var payload = JsonSerializer.Deserialize<OrderCreatedPayload>(elem.GetRawText(), JsonDefaults.Options);
        if (payload == null) return null;

        var orderId = payload.OrderId != Guid.Empty ? payload.OrderId : ParseKeyOrRandom(key);
        var amount = payload.EffectiveTotalAmount;
        var formattedAmount = amount.ToString("F2", CultureInfo.InvariantCulture);

        var title = "Order Created";
        var message = $"Order #{orderId} has been created for a total of {formattedAmount}. Awaiting inventory and payment.";

        return new Notification(
            eventId: eventId,
            userId: payload.UserId,
            orderId: orderId,
            type: NotificationType.OrderCreated,
            title: title,
            message: message,
            createdAt: payload.EffectiveCreatedAt
        );
    }

    private static Notification? CreatePaymentCompletedNotification(JsonElement root, Guid eventId, string? key)
    {
        var elem = GetEffectiveElement(root);
        var payload = JsonSerializer.Deserialize<PaymentCompletedPayload>(elem.GetRawText(), JsonDefaults.Options);
        if (payload == null) return null;

        var orderId = payload.OrderId != Guid.Empty ? payload.OrderId : ParseKeyOrRandom(key);
        var userId = payload.UserId ?? Guid.Empty;
        var amount = payload.EffectiveAmount;
        var formattedAmount = amount.ToString("F2", CultureInfo.InvariantCulture);
        var txnRef = payload.EffectiveReference;

        var title = "Payment Succeeded";
        var message = $"Payment of {formattedAmount} for Order #{orderId} succeeded (Ref: {txnRef}).";

        return new Notification(
            eventId: eventId,
            userId: userId,
            orderId: orderId,
            type: NotificationType.PaymentCompleted,
            title: title,
            message: message,
            createdAt: payload.EffectiveCompletedAt
        );
    }

    private static Notification? CreatePaymentFailedNotification(JsonElement root, Guid eventId, string? key)
    {
        var elem = GetEffectiveElement(root);
        var payload = JsonSerializer.Deserialize<PaymentFailedPayload>(elem.GetRawText(), JsonDefaults.Options);
        if (payload == null) return null;

        var orderId = payload.OrderId != Guid.Empty ? payload.OrderId : ParseKeyOrRandom(key);
        var userId = payload.UserId ?? Guid.Empty;
        var amount = payload.EffectiveAmount;
        var formattedAmount = amount.ToString("F2", CultureInfo.InvariantCulture);
        var reason = payload.EffectiveFailureReason;

        var title = "Payment Failed";
        var message = $"Payment of {formattedAmount} for Order #{orderId} failed. Reason: {reason}.";

        return new Notification(
            eventId: eventId,
            userId: userId,
            orderId: orderId,
            type: NotificationType.PaymentFailed,
            title: title,
            message: message,
            createdAt: payload.EffectiveFailedAt
        );
    }

    private static Notification? CreateOrderCancelledNotification(JsonElement root, Guid eventId, string? key)
    {
        var elem = GetEffectiveElement(root);
        var payload = JsonSerializer.Deserialize<OrderCancelledPayload>(elem.GetRawText(), JsonDefaults.Options);
        if (payload == null) return null;

        var orderId = payload.OrderId != Guid.Empty ? payload.OrderId : ParseKeyOrRandom(key);
        var reason = payload.EffectiveReason;

        var title = "Order Cancelled";
        var message = $"Order #{orderId} has been cancelled. Reason: {reason}.";

        return new Notification(
            eventId: eventId,
            userId: payload.UserId,
            orderId: orderId,
            type: NotificationType.OrderCancelled,
            title: title,
            message: message,
            createdAt: payload.EffectiveCancelledAt
        );
    }

    private static Notification? CreateOrderStatusChangedNotification(JsonElement root, Guid eventId, string? key)
    {
        var elem = GetEffectiveElement(root);
        var payload = JsonSerializer.Deserialize<OrderStatusChangedPayload>(elem.GetRawText(), JsonDefaults.Options);
        if (payload == null) return null;

        var orderId = payload.OrderId != Guid.Empty ? payload.OrderId : ParseKeyOrRandom(key);
        var status = payload.Status;
        var prevStatus = payload.EffectivePreviousStatus;
        var reason = payload.EffectiveReason;

        var title = "Order Status Updated";
        var message = $"Order #{orderId} status changed from {prevStatus} to {status}. Reason: {reason}.";

        return new Notification(
            eventId: eventId,
            userId: payload.UserId,
            orderId: orderId,
            type: NotificationType.OrderStatusChanged,
            title: title,
            message: message,
            createdAt: payload.EffectiveChangedAt
        );
    }

    private static Guid ParseKeyOrRandom(string? key)
    {
        if (!string.IsNullOrWhiteSpace(key) && Guid.TryParse(key, out var id))
        {
            return id;
        }
        return Guid.NewGuid();
    }
}

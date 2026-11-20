namespace NotificationService.Worker.Contracts;

public record OrderCancelledPayload(
    Guid OrderId,
    Guid UserId,
    string? Reason,
    string? PreviousStatus,
    DateTimeOffset? CancelledAt,
    Guid? EventId = null
)
{
    public string EffectiveReason => !string.IsNullOrWhiteSpace(Reason) ? Reason : "Cancelled";
    public string EffectivePreviousStatus => !string.IsNullOrWhiteSpace(PreviousStatus) ? PreviousStatus : "Unknown";
    public DateTimeOffset EffectiveCancelledAt => CancelledAt ?? DateTimeOffset.UtcNow;
}

public record OrderStatusChangedPayload(
    Guid OrderId,
    Guid UserId,
    string Status,
    string? PreviousStatus,
    string? Reason,
    DateTimeOffset? ChangedAt,
    Guid? EventId = null
)
{
    public string EffectivePreviousStatus => !string.IsNullOrWhiteSpace(PreviousStatus) ? PreviousStatus : "Unknown";
    public string EffectiveReason => !string.IsNullOrWhiteSpace(Reason) ? Reason : "Status updated";
    public DateTimeOffset EffectiveChangedAt => ChangedAt ?? DateTimeOffset.UtcNow;
}

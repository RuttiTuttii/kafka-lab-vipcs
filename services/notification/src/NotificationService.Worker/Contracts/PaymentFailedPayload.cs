namespace NotificationService.Worker.Contracts;

public record PaymentFailedPayload(
    Guid? PaymentId,
    Guid OrderId,
    Guid? UserId,
    decimal Amount,
    string? FailureReason,
    DateTimeOffset? FailedAt,
    Guid? EventId = null,
    string? Reason = null
)
{
    public Guid EffectivePaymentId => PaymentId ?? Guid.NewGuid();
    public decimal EffectiveAmount => Amount;
    public string EffectiveFailureReason => !string.IsNullOrWhiteSpace(FailureReason) 
        ? FailureReason 
        : (!string.IsNullOrWhiteSpace(Reason) ? Reason : "Payment transaction declined");
    public DateTimeOffset EffectiveFailedAt => FailedAt ?? DateTimeOffset.UtcNow;
}

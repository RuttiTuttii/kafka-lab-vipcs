namespace NotificationService.Worker.Contracts;

public record PaymentCompletedPayload(
    Guid? PaymentId,
    Guid OrderId,
    Guid? UserId,
    decimal Amount,
    string? TransactionReference,
    DateTimeOffset? CompletedAt,
    Guid? EventId = null,
    DateTimeOffset? PaidAt = null
)
{
    public Guid EffectivePaymentId => PaymentId ?? Guid.NewGuid();
    public decimal EffectiveAmount => Amount;
    public string EffectiveReference => !string.IsNullOrWhiteSpace(TransactionReference) 
        ? TransactionReference 
        : (PaymentId.HasValue ? PaymentId.Value.ToString("N")[..8] : "N/A");
    public DateTimeOffset EffectiveCompletedAt => CompletedAt ?? PaidAt ?? DateTimeOffset.UtcNow;
}

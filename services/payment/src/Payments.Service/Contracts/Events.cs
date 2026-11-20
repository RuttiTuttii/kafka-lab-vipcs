namespace Payments.Service.Contracts;

// входящее order.created: нужен заказ, пользователь и сумма
public sealed record OrderCreatedIn(Guid EventId, Guid OrderId, Guid UserId, decimal TotalAmount, decimal? Amount);

public sealed record PaymentCompleted(
    Guid EventId, Guid PaymentId, Guid OrderId, Guid UserId, decimal Amount,
    string TransactionReference, DateTimeOffset CompletedAt);

public sealed record PaymentFailed(
    Guid EventId, Guid PaymentId, Guid OrderId, Guid UserId, decimal Amount,
    string FailureReason, DateTimeOffset FailedAt);

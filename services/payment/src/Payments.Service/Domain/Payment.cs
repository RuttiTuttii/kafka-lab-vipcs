namespace Payments.Service.Domain;

public enum PaymentStatus { Pending, Completed, Failed }

public sealed record PaymentView(
    Guid PaymentId, Guid OrderId, Guid UserId, decimal Amount, string Status,
    string? Reason, string? TransactionReference, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

// Changed = платёж только что перешёл из Pending в итоговый статус
public sealed record Decision(PaymentView? Payment, bool Changed);

// правило имитации: сумма должна быть положительной и не больше лимита
public static class PaymentPolicy
{
    public static (bool Ok, string? Reason) Evaluate(decimal amount, decimal limit)
    {
        if (amount <= 0) return (false, "invalid_amount");
        if (amount > limit) return (false, "limit_exceeded");
        return (true, null);
    }
}

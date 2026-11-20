using Microsoft.Extensions.Options;
using Payments.Service.Contracts;
using Payments.Service.Kafka;
using Payments.Service.Options;

namespace Payments.Service.Domain;

// решение по платежу и публикация результата, его вызывают и consumer, и API
public sealed class PaymentProcessor(
    PaymentStore store, EventPublisher publisher, IOptions<PaymentOptions> options, ILogger<PaymentProcessor> log)
{
    private readonly PaymentOptions _opt = options.Value;

    public async Task OnOrderCreatedAsync(OrderCreatedIn order, CancellationToken ct)
    {
        var amount = order.TotalAmount > 0 ? order.TotalAmount : order.Amount ?? 0m;
        var (payment, created) = store.Register(order.OrderId, order.UserId, amount);
        if (!created) { log.LogInformation("order {OrderId}: payment already exists, skipped", order.OrderId); return; }

        // в ручном режиме платёж ждёт подтверждения через API
        if (!string.Equals(_opt.Mode, "auto", StringComparison.OrdinalIgnoreCase))
        {
            log.LogInformation("order {OrderId}: payment {PaymentId} waits for manual decision", order.OrderId, payment.PaymentId);
            return;
        }

        // имитация обращения к платёжному шлюзу
        if (_opt.DelayMs > 0) await Task.Delay(_opt.DelayMs, ct);

        var (ok, reason) = PaymentPolicy.Evaluate(amount, _opt.Limit);
        if (ok) await ConfirmAsync(order.OrderId, ct);
        else await DeclineAsync(order.OrderId, reason!, ct);
    }

    public async Task<Decision> ConfirmAsync(Guid orderId, CancellationToken ct)
    {
        var d = store.Complete(orderId);
        if (!d.Changed || d.Payment is null) return d;

        var p = d.Payment;
        await publisher.PublishAsync(Topics.PaymentCompleted, orderId.ToString(),
            new PaymentCompleted(Guid.NewGuid(), p.PaymentId, p.OrderId, p.UserId, p.Amount, p.TransactionReference!, DateTimeOffset.UtcNow), ct);
        log.LogInformation("order {OrderId}: payment completed {Amount}", orderId, p.Amount);
        return d;
    }

    public async Task<Decision> DeclineAsync(Guid orderId, string reason, CancellationToken ct)
    {
        var d = store.Fail(orderId, reason);
        if (!d.Changed || d.Payment is null) return d;

        var p = d.Payment;
        await publisher.PublishAsync(Topics.PaymentFailed, orderId.ToString(),
            new PaymentFailed(Guid.NewGuid(), p.PaymentId, p.OrderId, p.UserId, p.Amount, reason, DateTimeOffset.UtcNow), ct);
        log.LogInformation("order {OrderId}: payment failed, {Reason}", orderId, reason);
        return d;
    }
}

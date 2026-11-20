using Orders.Service.Contracts;
using Orders.Service.Kafka;

namespace Orders.Service.Domain;

// бизнес-логика заказа: хранилище плюс публикация событий, её вызывают и API, и consumer
public sealed class OrderFlow(OrderStore store, EventPublisher publisher, ILogger<OrderFlow> log)
{
    public async Task<OrderView> CreateAsync(Guid userId, IReadOnlyList<OrderLine> items, CancellationToken ct)
    {
        var order = store.Create(userId, items);
        var evt = new OrderCreated(
            Guid.NewGuid(), order.Id, order.UserId,
            order.Items.Select(i => new OrderItemEvent(i.Sku, i.ProductName, i.Quantity, i.UnitPrice)).ToList(),
            order.TotalAmount, order.Currency, order.Status, order.CreatedAt);

        try { await publisher.PublishAsync(Topics.OrderCreated, order.Id.ToString(), evt, ct); }
        catch
        {
            // событие не ушло, поэтому заказ откатываем, иначе он навсегда зависнет в Created
            store.Remove(order.Id);
            throw;
        }
        log.LogInformation("order {OrderId} created, total {Total}", order.Id, order.TotalAmount);
        return order;
    }

    public async Task<Transition> CancelAsync(Guid id, string reason, CancellationToken ct)
    {
        var t = store.Cancel(id, reason);
        await PublishCancelledAsync(t, reason, ct);
        return t;
    }

    public Task<Transition> OnPaymentCompletedAsync(Guid id)
    {
        var t = store.MarkPaid(id);
        LogTransition("payment completed", id, t);
        return Task.FromResult(t);
    }

    public Task<Transition> OnInventoryReservedAsync(Guid id)
    {
        var t = store.MarkReserved(id);
        LogTransition("inventory reserved", id, t);
        return Task.FromResult(t);
    }

    public async Task OnFailureAsync(Guid id, string reason, CancellationToken ct)
    {
        var t = store.Fail(id, reason);
        LogTransition("failure " + reason, id, t);
        await PublishCancelledAsync(t, reason, ct);
    }

    private async Task PublishCancelledAsync(Transition t, string reason, CancellationToken ct)
    {
        if (!t.Changed || t.Order is null) return;
        var evt = new OrderCancelled(Guid.NewGuid(), t.Order.Id, t.Order.UserId, reason, t.PreviousStatus ?? "Unknown", DateTimeOffset.UtcNow);
        await publisher.PublishAsync(Topics.OrderCancelled, t.Order.Id.ToString(), evt, ct);
    }

    private void LogTransition(string what, Guid id, Transition t)
    {
        if (t.Order is null) { log.LogWarning("{What}: order {OrderId} is unknown here, ignored", what, id); return; }
        log.LogInformation("{What}: order {OrderId} is {Status} (paid={Paid}, reserved={Reserved})",
            what, id, t.Order.Status, t.Order.Paid, t.Order.Reserved);
    }
}

using System.Text.Json;
using Orders.Service.Contracts;
using Orders.Service.Domain;
using Orders.Service.Options;
using Microsoft.Extensions.Options;

namespace Orders.Service.Kafka;

// слушает ответы payment и inventory и двигает статус заказа
public sealed class OrderEventsConsumer(IOptions<KafkaOptions> options, TopicReadiness readiness, OrderFlow flow, ILogger<OrderEventsConsumer> log)
    : TopicConsumer(options, readiness, log)
{
    protected override IReadOnlyList<string> TopicBases =>
        [Topics.PaymentCompleted, Topics.PaymentFailed, Topics.InventoryReserved, Topics.InventoryFailed];

    protected override async Task HandleAsync(string topicBase, string json, CancellationToken ct)
    {
        var evt = JsonSerializer.Deserialize<IncomingEvent>(json, EventPublisher.Json);
        if (evt is null || evt.OrderId == Guid.Empty) { log.LogError("event without orderId skipped"); return; }

        var reason = evt.FailureReason ?? evt.Reason ?? "unknown";
        switch (topicBase)
        {
            case Topics.PaymentCompleted: await flow.OnPaymentCompletedAsync(evt.OrderId); break;
            case Topics.InventoryReserved: await flow.OnInventoryReservedAsync(evt.OrderId); break;
            case Topics.PaymentFailed: await flow.OnFailureAsync(evt.OrderId, "payment_failed: " + reason, ct); break;
            case Topics.InventoryFailed: await flow.OnFailureAsync(evt.OrderId, "inventory_failed: " + reason, ct); break;
        }
    }
}

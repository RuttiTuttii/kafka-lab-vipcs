using System.Text.Json;
using Microsoft.Extensions.Options;
using Payments.Service.Contracts;
using Payments.Service.Domain;
using Payments.Service.Options;

namespace Payments.Service.Kafka;

// слушает order.created и запускает оплату
public sealed class OrderCreatedConsumer(IOptions<KafkaOptions> options, TopicReadiness readiness, PaymentProcessor processor, ILogger<OrderCreatedConsumer> log)
    : TopicConsumer(options, readiness, log)
{
    protected override IReadOnlyList<string> TopicBases => [Topics.OrderCreated];

    protected override async Task HandleAsync(string topicBase, string json, CancellationToken ct)
    {
        var order = JsonSerializer.Deserialize<OrderCreatedIn>(json, EventPublisher.Json);
        if (order is null || order.OrderId == Guid.Empty) { log.LogError("order.created without orderId skipped"); return; }
        await processor.OnOrderCreatedAsync(order, ct);
    }
}

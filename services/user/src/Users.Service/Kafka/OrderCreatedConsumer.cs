using System.Text.Json;
using Microsoft.Extensions.Options;
using Users.Service.Contracts;
using Users.Service.Domain;
using Users.Service.Options;

namespace Users.Service.Kafka;

// слушает order.created и считает заказы пользователя
public sealed class OrderCreatedConsumer(IOptions<KafkaOptions> options, TopicReadiness readiness, UserStore store, ILogger<OrderCreatedConsumer> log)
    : TopicConsumer(options, readiness, log)
{
    protected override IReadOnlyList<string> TopicBases => [Topics.OrderCreated];

    protected override Task HandleAsync(string topicBase, string json, CancellationToken ct)
    {
        var order = JsonSerializer.Deserialize<OrderCreatedIn>(json, EventPublisher.Json);
        if (order is null || order.OrderId == Guid.Empty) { log.LogError("order.created without orderId skipped"); return Task.CompletedTask; }

        if (store.CountOrder(order.UserId, order.OrderId))
            log.LogInformation("user {UserId}: order {OrderId} counted", order.UserId, order.OrderId);
        else
            log.LogInformation("order {OrderId}: user {UserId} unknown here or order already counted", order.OrderId, order.UserId);
        return Task.CompletedTask;
    }
}

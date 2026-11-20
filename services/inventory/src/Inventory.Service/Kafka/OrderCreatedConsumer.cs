using System.Text.Json;
using Confluent.Kafka;
using Inventory.Service.Contracts;
using Inventory.Service.Domain;
using Inventory.Service.Options;
using Microsoft.Extensions.Options;

namespace Inventory.Service.Kafka;

// слушает order.created, резервирует товар и отвечает inventory.reserved или inventory.failed
public sealed class OrderCreatedConsumer(
    IOptions<KafkaOptions> options,
    InventoryStore store,
    EventPublisher publisher,
    ILogger<OrderCreatedConsumer> log) : BackgroundService
{
    private readonly KafkaOptions _options = options.Value;

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.Run(() => RunAsync(stoppingToken), stoppingToken);

    private async Task RunAsync(CancellationToken ct)
    {
        var config = new ConsumerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            GroupId = _options.GroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
        };

        using var consumer = new ConsumerBuilder<string, string>(config)
            .SetErrorHandler((_, e) => log.LogWarning("kafka: {Reason}", e.Reason))
            .Build();

        var topic = _options.Topic(TopicNames.OrderCreated);
        consumer.Subscribe(topic);
        log.LogInformation("consumer started: broker={Broker} topic={Topic} group={Group}",
            _options.BootstrapServers, topic, _options.GroupId);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(TimeSpan.FromSeconds(1));
                if (result is null) continue;

                await HandleAsync(result.Message.Value, ct);

                // коммитим только после ответа, иначе при падении событие потеряется
                consumer.Commit(result);
            }
            catch (OperationCanceledException) { break; }
            catch (ConsumeException e)
            {
                log.LogWarning("consume failed: {Reason}", e.Error.Reason);
                await Task.Delay(TimeSpan.FromSeconds(2), ct);
            }
        }

        consumer.Close();
    }

    private async Task HandleAsync(string json, CancellationToken ct)
    {
        OrderCreated? order;
        try { order = JsonSerializer.Deserialize<OrderCreated>(json, EventPublisher.Json); }
        catch (JsonException e)
        {
            // битое сообщение пропускаем, иначе оно заблокирует партицию навсегда
            log.LogError("bad order.created skipped: {Error}", e.Message);
            return;
        }
        if (order is null || order.Items is null) { log.LogError("empty order.created skipped"); return; }

        var items = order.Items.Select(i => (i.Sku, i.Quantity)).ToList();
        var result = store.Reserve(order.OrderId, items);
        var key = order.OrderId.ToString();

        if (result.Success)
        {
            var reserved = new InventoryReserved(Guid.NewGuid(), order.OrderId,
                order.Items.Select(i => new ReservedItem(i.Sku, i.Quantity)).ToList(), DateTimeOffset.UtcNow);
            await publisher.PublishAsync(TopicNames.InventoryReserved, key, reserved, ct);
            log.LogInformation("reserved order {OrderId}", order.OrderId);
        }
        else
        {
            var failed = new InventoryFailed(Guid.NewGuid(), order.OrderId, result.Reason!, DateTimeOffset.UtcNow);
            await publisher.PublishAsync(TopicNames.InventoryFailed, key, failed, ct);
            log.LogInformation("failed order {OrderId}: {Reason}", order.OrderId, result.Reason);
        }
    }
}

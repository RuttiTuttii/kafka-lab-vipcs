using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NotificationService.Worker.Contracts;

namespace NotificationSeeder.Worker;

public class SeederWorker : BackgroundService
{
    private readonly SeederSettings _settings;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<SeederWorker> _logger;

    public SeederWorker(
        IOptions<SeederSettings> settings,
        IHostApplicationLifetime lifetime,
        ILogger<SeederWorker> logger)
    {
        _settings = settings.Value;
        _lifetime = lifetime;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("====================================================================");
        _logger.LogInformation("  Starting NotificationSeeder.Worker (CLI Test Producer)");
        _logger.LogInformation("  Kafka Broker:        {BootstrapServers}", _settings.BootstrapServers);
        _logger.LogInformation("  Topic Suffix:        '{TopicSuffix}'", _settings.TopicSuffix);
        _logger.LogInformation("  Step Delay:          {Delay}ms", _settings.DelayBetweenStepsMs);
        _logger.LogInformation("====================================================================");

        var producerConfig = new ProducerConfig
        {
            BootstrapServers = _settings.BootstrapServers,
            Acks = Acks.All,
            MessageTimeoutMs = 10000,
            SocketTimeoutMs = 10000,
            EnableIdempotence = true
        };

        using var producer = new ProducerBuilder<string, string>(producerConfig)
            .SetErrorHandler((_, e) =>
            {
                _logger.LogWarning("Kafka producer warning/error: {Reason} (Code={Code})", e.Reason, e.Code);
            })
            .Build();

        try
        {
            // Give Kafka connection time to establish
            await Task.Delay(500, stoppingToken);

            // =========================================================================
            // FLOW 1: Happy Path Flow (Order #101, User A)
            // =========================================================================
            _logger.LogInformation("\n>>> [STARTING FLOW 1] Happy Path Flow for Order #101 (User A) <<<");

            var order101Id = Guid.Parse("00000101-0000-0000-0000-000000000101");
            var userAId = Guid.Parse("0000000a-0000-0000-0000-00000000000a");
            var eventOrderCreated101Id = Guid.Parse("10100001-0000-0000-0000-000000000001");
            var eventPaymentCompleted101Id = Guid.Parse("10100002-0000-0000-0000-000000000002");

            // 1.1 Publishes order.created for Order #101
            var orderCreated101Payload = new OrderCreatedPayload(
                OrderId: order101Id,
                UserId: userAId,
                Items: new List<OrderItemDto>
                {
                    new(
                        ProductId: Guid.Parse("00000001-0001-0000-0000-000000000001"),
                        ProductName: "Mechanical Keyboard RGB",
                        Quantity: 2,
                        UnitPrice: 1500.00m
                    )
                },
                TotalAmount: 3000.00m,
                Status: "Created",
                CreatedAt: DateTimeOffset.UtcNow,
                EventId: eventOrderCreated101Id
            );

            await PublishEventAsync(
                producer,
                _settings.ResolveTopic("order.created"),
                order101Id.ToString(),
                eventOrderCreated101Id,
                orderCreated101Payload,
                "Order #101 created (User A, total: 3000.00)",
                stoppingToken);

            _logger.LogInformation("Waiting {Delay}ms before sending payment.completed...", _settings.DelayBetweenStepsMs);
            await Task.Delay(_settings.DelayBetweenStepsMs, stoppingToken);

            // 1.2 Publishes payment.completed for Order #101
            var paymentCompleted101Payload = new PaymentCompletedPayload(
                PaymentId: Guid.Parse("00000101-9001-0000-0000-000000000101"),
                OrderId: order101Id,
                UserId: userAId,
                Amount: 3000.00m,
                TransactionReference: "TXN-20261120-101SUCCESS",
                CompletedAt: DateTimeOffset.UtcNow,
                EventId: eventPaymentCompleted101Id
            );

            await PublishEventAsync(
                producer,
                _settings.ResolveTopic("payment.completed"),
                order101Id.ToString(),
                eventPaymentCompleted101Id,
                paymentCompleted101Payload,
                "Payment for Order #101 completed successfully",
                stoppingToken);

            _logger.LogInformation("<<< [FLOW 1 COMPLETED] >>>\n");

            // =========================================================================
            // FLOW 2: Payment Failure & Cancellation Flow (Order #102, User B)
            // =========================================================================
            _logger.LogInformation(">>> [STARTING FLOW 2] Payment Failure & Cancellation Flow for Order #102 (User B) <<<");

            var order102Id = Guid.Parse("00000102-0000-0000-0000-000000000102");
            var userBId = Guid.Parse("0000000b-0000-0000-0000-00000000000b");
            var eventOrderCreated102Id = Guid.Parse("10200001-0000-0000-0000-000000000001");
            var eventPaymentFailed102Id = Guid.Parse("10200002-0000-0000-0000-000000000002");
            var eventOrderCancelled102Id = Guid.Parse("10200003-0000-0000-0000-000000000003");

            // 2.1 Publishes order.created for Order #102
            var orderCreated102Payload = new OrderCreatedPayload(
                OrderId: order102Id,
                UserId: userBId,
                Items: new List<OrderItemDto>
                {
                    new(
                        ProductId: Guid.Parse("00000002-0001-0000-0000-000000000002"),
                        ProductName: "Wireless Gaming Mouse",
                        Quantity: 1,
                        UnitPrice: 3000.00m
                    )
                },
                TotalAmount: 3000.00m,
                Status: "Created",
                CreatedAt: DateTimeOffset.UtcNow,
                EventId: eventOrderCreated102Id
            );

            await PublishEventAsync(
                producer,
                _settings.ResolveTopic("order.created"),
                order102Id.ToString(),
                eventOrderCreated102Id,
                orderCreated102Payload,
                "Order #102 created (User B, total: 3000.00)",
                stoppingToken);

            _logger.LogInformation("Waiting {Delay}ms before sending payment.failed...", _settings.DelayBetweenStepsMs);
            await Task.Delay(_settings.DelayBetweenStepsMs, stoppingToken);

            // 2.2 Publishes payment.failed for Order #102
            var paymentFailed102Payload = new PaymentFailedPayload(
                PaymentId: Guid.Parse("00000102-9002-0000-0000-000000000102"),
                OrderId: order102Id,
                UserId: userBId,
                Amount: 3000.00m,
                FailureReason: "Insufficient funds in bank account",
                FailedAt: DateTimeOffset.UtcNow,
                EventId: eventPaymentFailed102Id
            );

            await PublishEventAsync(
                producer,
                _settings.ResolveTopic("payment.failed"),
                order102Id.ToString(),
                eventPaymentFailed102Id,
                paymentFailed102Payload,
                "Payment for Order #102 failed: Insufficient funds in bank account",
                stoppingToken);

            _logger.LogInformation("Waiting {Delay}ms before sending order.cancelled...", _settings.DelayBetweenStepsMs);
            await Task.Delay(_settings.DelayBetweenStepsMs, stoppingToken);

            // 2.3 Publishes order.cancelled for Order #102
            var orderCancelled102Payload = new OrderCancelledPayload(
                OrderId: order102Id,
                UserId: userBId,
                Reason: "Payment failed: Insufficient funds in bank account",
                PreviousStatus: "AwaitingPayment",
                CancelledAt: DateTimeOffset.UtcNow,
                EventId: eventOrderCancelled102Id
            );

            await PublishEventAsync(
                producer,
                _settings.ResolveTopic("order.cancelled"),
                order102Id.ToString(),
                eventOrderCancelled102Id,
                orderCancelled102Payload,
                "Order #102 cancelled due to payment failure",
                stoppingToken);

            _logger.LogInformation("<<< [FLOW 2 COMPLETED] >>>\n");

            // =========================================================================
            // FLOW 3: Idempotency Check
            // =========================================================================
            _logger.LogInformation(">>> [STARTING FLOW 3] Idempotency Check (Duplicate order.created for Order #101) <<<");
            _logger.LogInformation("Waiting {Delay}ms before publishing duplicate event...", _settings.DelayBetweenStepsMs);
            await Task.Delay(_settings.DelayBetweenStepsMs, stoppingToken);

            // Re-publishes exact duplicate order.created message with same eventId
            await PublishEventAsync(
                producer,
                _settings.ResolveTopic("order.created"),
                order101Id.ToString(),
                eventOrderCreated101Id, // EXACT SAME EventId as 1.1
                orderCreated101Payload,
                "DUPLICATE order.created for Order #101 (SAME EventId: 10100001-0000-0000-0000-000000000001)",
                stoppingToken);

            _logger.LogInformation("<<< [FLOW 3 COMPLETED] NotificationService should detect duplicate EventId and skip processing. >>>\n");

            // Flush remaining messages
            _logger.LogInformation("Flushing producer buffer to Kafka broker...");
            producer.Flush(TimeSpan.FromSeconds(10));
            _logger.LogInformation("All test events published successfully!");
        }
        catch (ProduceException<string, string> ex)
        {
            _logger.LogError(
                ex,
                "Failed to produce message to Kafka broker '{Broker}': {Reason}. Note: Limits apply if running outside college network.",
                _settings.BootstrapServers,
                ex.Error.Reason);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Seeder execution was cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in SeederWorker.");
        }
        finally
        {
            _logger.LogInformation("NotificationSeeder completed its test run. Exiting seeder process.");
            _lifetime.StopApplication();
        }
    }

    private async Task PublishEventAsync<T>(
        IProducer<string, string> producer,
        string topic,
        string key,
        Guid eventId,
        T payload,
        string description,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(payload, JsonDefaults.Options);

        _logger.LogInformation(
            "==> [PUBLISHING] Topic: '{Topic}' | Key: '{Key}' | EventId: '{EventId}'\n    Description: {Description}\n    Payload: {Json}",
            topic, key, eventId, description, json);

        var message = new Message<string, string>
        {
            Key = key,
            Value = json,
            Timestamp = new Timestamp(DateTime.UtcNow)
        };

        var deliveryResult = await producer.ProduceAsync(topic, message, cancellationToken);

        _logger.LogInformation(
            "<== [DELIVERED] Topic: '{Topic}' [Partition {Partition} @ Offset {Offset}] Status: {Status}",
            deliveryResult.Topic, deliveryResult.Partition.Value, deliveryResult.Offset.Value, deliveryResult.Status);
    }
}

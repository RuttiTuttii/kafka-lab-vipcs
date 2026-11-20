using Confluent.Kafka;
using System.Text.Json;

namespace PaymentService
{
    public sealed class PaymentWorker(ILogger<PaymentWorker> logger, PaymentStore processor, IConfiguration configuration) : BackgroundService
    {
        private readonly ILogger<PaymentWorker> _logger = logger;
        private readonly PaymentStore _processor = processor;
        private readonly string _bootstrapServers = configuration["Kafka:BootstrapServers"]!;
        private readonly string _groupId = configuration["Kafka:GroupId"]!;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _processor.Seed();
            _logger.LogInformation("PaymentService запущен. Загружено записей из сидера: {Count}", _processor.GetAll().Count);

            await Task.Run(() => ConsumeLoop(stoppingToken), stoppingToken);
        }

        private async Task ConsumeLoop(CancellationToken ct)
        {
            var consumerConfig = new ConsumerConfig
            {
                BootstrapServers = _bootstrapServers,
                GroupId = _groupId,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = false
            };

            var producerConfig = new ProducerConfig
            {
                BootstrapServers = _bootstrapServers,
                Acks = Acks.All
            };

            using var consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
            using var producer = new ProducerBuilder<string, string>(producerConfig).Build();

            consumer.Subscribe("order.created2");

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    var cr = consumer.Consume(ct);
                    if (cr?.Message?.Value is null) 
                        continue;

                    var orderEvent = JsonSerializer.Deserialize<OrderCreatedEvent>(cr.Message.Value);

                    if (orderEvent is null)
                    {
                        consumer.Commit(cr);
                        continue;
                    }

                    await Task.Delay(300, ct);

                    var payment = _processor.ProcessOrderPayment(orderEvent);
                    var isSuccess = payment.Status == "Completed";
                    var resultTopic = isSuccess ? "payment.completed2" : "payment.failed2";
                    object resultEvent;

                    if (isSuccess)
                    {
                        resultEvent = new PaymentCompleteEvent(
                            EventId: Guid.NewGuid(),
                            OrderId: orderEvent.OrderId,
                            PaymentId: payment.PaymentId,
                            Amount: orderEvent.Amount,
                            PaidAt: DateTime.UtcNow
                        );
                    }
                    else
                    {
                        resultEvent = new PaymentFailedEvent(
                            EventId: Guid.NewGuid(),
                            OrderId: orderEvent.OrderId,
                            Reason: payment.Reason,
                            FailedAt: DateTime.UtcNow
                        );
                    }


                    await producer.ProduceAsync(
                        resultTopic,
                        new Message<string, string>
                        {
                            Key = orderEvent.OrderId.ToString(),
                            Value = JsonSerializer.Serialize(resultEvent)
                        },
                        ct);

                    _logger.LogInformation(
                        "Платёж {PaymentId} по заказу {OrderId} (сумма {Amount}): {Status} -> топик {Topic} ({Reason})",
                        payment.PaymentId, payment.OrderId, payment.Amount, payment.Status, resultTopic, payment.Reason);

                    consumer.Commit(cr);
                }
            }
            catch (OperationCanceledException)
            {
                consumer.Close();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка работы PaymentWorker");
            }
        }
    }
}
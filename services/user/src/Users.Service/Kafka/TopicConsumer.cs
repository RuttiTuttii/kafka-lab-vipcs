using System.Text.Json;
using Confluent.Kafka;
using Users.Service.Options;
using Microsoft.Extensions.Options;

namespace Users.Service.Kafka;

// общий consumer: подписка на несколько топиков, ручной commit после обработки
public abstract class TopicConsumer(IOptions<KafkaOptions> options, TopicReadiness readiness, ILogger logger) : BackgroundService
{
    private readonly KafkaOptions _options = options.Value;

    protected abstract IReadOnlyList<string> TopicBases { get; }

    protected abstract Task HandleAsync(string topicBase, string json, CancellationToken ct);

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.Run(() => RunAsync(stoppingToken), stoppingToken);

    private async Task RunAsync(CancellationToken ct)
    {
        // ждём, пока топики будут созданы или найдены
        try { await readiness.Ready.WaitAsync(ct); } catch (OperationCanceledException) { return; }

        var config = new ConsumerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            GroupId = _options.GroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
        };

        using var consumer = new ConsumerBuilder<string, string>(config)
            .SetErrorHandler((_, e) => logger.LogWarning("kafka: {Reason}", e.Reason))
            .Build();

        var topics = TopicBases.Select(_options.Topic).ToList();
        consumer.Subscribe(topics);
        logger.LogInformation("consumer started: broker={Broker} topics={Topics} group={Group}",
            _options.BootstrapServers, string.Join(",", topics), _options.GroupId);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(TimeSpan.FromSeconds(1));
                if (result is null) continue;

                var baseName = result.Topic.EndsWith(_options.TopicSuffix, StringComparison.Ordinal)
                    ? result.Topic[..^_options.TopicSuffix.Length]
                    : result.Topic;

                try { await HandleAsync(baseName, result.Message.Value, ct); }
                catch (JsonException e) { logger.LogError("bad message in {Topic} skipped: {Error}", result.Topic, e.Message); }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    // сообщение пропускаем, чтобы одно битое событие не блокировало партицию
                    logger.LogError(e, "handler failed for {Topic}, message skipped", result.Topic);
                }

                // коммитим только после обработки
                consumer.Commit(result);
            }
            catch (OperationCanceledException) { break; }
            catch (ConsumeException e)
            {
                logger.LogWarning("consume failed: {Reason}", e.Error.Reason);
                try { await Task.Delay(TimeSpan.FromSeconds(2), ct); } catch (OperationCanceledException) { break; }
            }
        }

        consumer.Close();
    }
}

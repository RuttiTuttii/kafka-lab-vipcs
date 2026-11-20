using Confluent.Kafka;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NotificationService.Worker.Application;

namespace NotificationService.Worker.Infrastructure.Kafka;

public class KafkaNotificationConsumerWorker : BackgroundService
{
    private readonly INotificationProcessor _processor;
    private readonly INotificationHistoryService _historyService;
    private readonly KafkaSettings _settings;
    private readonly ILogger<KafkaNotificationConsumerWorker> _logger;

    public KafkaNotificationConsumerWorker(
        INotificationProcessor processor,
        INotificationHistoryService historyService,
        IOptions<KafkaSettings> settings,
        ILogger<KafkaNotificationConsumerWorker> logger)
    {
        _processor = processor;
        _historyService = historyService;
        _settings = settings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var resolvedTopics = _settings.ResolveTopics().ToList();
        var resolvedGroupId = _settings.ResolveGroupId();

        _logger.LogInformation("====================================================================");
        _logger.LogInformation(" Starting Kafka Notification Consumer Worker");
        _logger.LogInformation(" Broker:            {BootstrapServers}", _settings.BootstrapServers);
        _logger.LogInformation(" Consumer Group ID: {GroupId}", resolvedGroupId);
        _logger.LogInformation(" Subscribed Topics: {Topics}", string.Join(", ", resolvedTopics));
        _logger.LogInformation(" AutoCommit:        {AutoCommit} (Manual commit after successful persistence)", _settings.EnableAutoCommit);
        _logger.LogInformation("====================================================================");

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = _settings.BootstrapServers,
            GroupId = resolvedGroupId,
            AutoOffsetReset = Enum.TryParse<AutoOffsetReset>(_settings.AutoOffsetReset, true, out var offsetReset) 
                ? offsetReset 
                : Confluent.Kafka.AutoOffsetReset.Earliest,
            EnableAutoCommit = false, // Enforce manual commit after persistent write
            EnableAutoOffsetStore = false,
            AllowAutoCreateTopics = true,
            SessionTimeoutMs = 10000,
            SocketTimeoutMs = 10000
        };

        var consumerBuilder = new ConsumerBuilder<string, string>(consumerConfig)
            .SetErrorHandler((_, e) =>
            {
                if (e.IsFatal)
                {
                    _logger.LogCritical("Fatal Kafka error: {Reason} (Code={Code})", e.Reason, e.Code);
                }
                else
                {
                    _logger.LogWarning("Kafka warning/error: {Reason} (Code={Code}). Broker={Broker}", 
                        e.Reason, e.Code, _settings.BootstrapServers);
                }
            })
            .SetPartitionsAssignedHandler((c, partitions) =>
            {
                _logger.LogInformation("Partitions assigned: {@Partitions}", 
                    partitions.Select(p => $"{p.Topic}[{p.Partition.Value}]"));
            })
            .SetPartitionsRevokedHandler((c, partitions) =>
            {
                _logger.LogInformation("Partitions revoked: {@Partitions}", 
                    partitions.Select(p => $"{p.Topic}[{p.Partition.Value}]"));
            });

        using var consumer = consumerBuilder.Build();
        consumer.Subscribe(resolvedTopics);

        // Start background periodic stats logger
        _ = Task.Run(() => RunPeriodicStatsLoggerAsync(stoppingToken), stoppingToken);

        await Task.Yield();

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var consumeResult = consumer.Consume(TimeSpan.FromMilliseconds(500));
                    if (consumeResult == null)
                    {
                        continue;
                    }

                    if (consumeResult.IsPartitionEOF)
                    {
                        continue;
                    }

                    _logger.LogDebug("Consumed message from {Topic} [P:{Partition} @ O:{Offset}]. Key: {Key}",
                        consumeResult.Topic, consumeResult.Partition.Value, consumeResult.Offset.Value, consumeResult.Message.Key);

                    // Process incoming event (formatting, deduplication, durable persistence)
                    await _processor.ProcessMessageAsync(
                        consumeResult.Topic,
                        consumeResult.Message.Key,
                        consumeResult.Message.Value,
                        stoppingToken);

                    // Commit offset ONLY after successful persistence
                    consumer.Commit(consumeResult);
                    _logger.LogDebug("Committed offset {Offset} for {Topic}[{Partition}]",
                        consumeResult.Offset.Value, consumeResult.Topic, consumeResult.Partition.Value);
                }
                catch (ConsumeException ex)
                {
                    _logger.LogWarning(
                        "Kafka consume error: {Reason}. Broker at '{Broker}' might be unavailable or unreachable. (Hint: Limits apply if running outside college network).",
                        ex.Error.Reason, _settings.BootstrapServers);

                    await Task.Delay(2000, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error processing message. Waiting before retrying...");
                    await Task.Delay(1000, stoppingToken);
                }
            }
        }
        finally
        {
            _logger.LogInformation("Shutting down Kafka Consumer Worker gracefully. Leaving consumer group...");
            try
            {
                consumer.Close();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error while closing consumer connection.");
            }
            _logger.LogInformation("Kafka Consumer Worker shut down successfully.");
        }
    }

    private async Task RunPeriodicStatsLoggerAsync(CancellationToken cancellationToken)
    {
        // Initial summary
        await Task.Delay(2000, cancellationToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        if (!cancellationToken.IsCancellationRequested)
        {
            await _historyService.LogSummaryStatisticsAsync(cancellationToken);
        }

        // Periodic logging every 30 seconds
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        try
        {
            while (!cancellationToken.IsCancellationRequested && await timer.WaitForNextTickAsync(cancellationToken))
            {
                await _historyService.LogSummaryStatisticsAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal exit
        }
    }
}

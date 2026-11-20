using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Payments.Service.Options;

namespace Payments.Service.Kafka;

// создаёт топики команды, если брокер не делает это сам: 1 партиция, replication factor 1
public static class TopicBootstrap
{
    public static async Task<bool> EnsureAsync(KafkaOptions options, ILogger log, CancellationToken ct)
    {
        var specs = Topics.All
            .Select(t => new TopicSpecification { Name = options.Topic(t), NumPartitions = 1, ReplicationFactor = 1 })
            .ToList();

        // ждём брокер, пока он недоступен, пробуем снова
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var admin = new AdminClientBuilder(new AdminClientConfig
                {
                    BootstrapServers = options.BootstrapServers,
                    SocketTimeoutMs = 10000,
                }).Build();

                await admin.CreateTopicsAsync(specs, new CreateTopicsOptions { RequestTimeout = TimeSpan.FromSeconds(15) });
                log.LogInformation("topics created: {Count}", specs.Count);
                return true;
            }
            catch (CreateTopicsException e)
            {
                // уже существующие топики это нормально, другие ошибки логируем
                var real = e.Results.Where(r => r.Error.Code != ErrorCode.TopicAlreadyExists).ToList();
                if (real.Count == 0) { log.LogInformation("topics already exist"); return true; }
                foreach (var r in real) log.LogWarning("topic {Topic}: {Reason}", r.Topic, r.Error.Reason);
                // создать нельзя (права), но топики могут уже быть: работаем дальше
                return true;
            }
            catch (Exception e) when (e is KafkaException or TimeoutException)
            {
                log.LogWarning("broker is not available, retry in 5 s: {Reason}", e.Message);
                try { await Task.Delay(TimeSpan.FromSeconds(5), ct); } catch (OperationCanceledException) { return false; }
            }
        }

        return false;
    }
}

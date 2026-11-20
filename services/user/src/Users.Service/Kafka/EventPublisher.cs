using System.Text.Json;
using Confluent.Kafka;
using Users.Service.Options;
using Microsoft.Extensions.Options;

namespace Users.Service.Kafka;

// общий producer: идемпотентный, acks=all, ключ сообщения задаёт вызывающий
public sealed class EventPublisher : IDisposable
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IProducer<string, string> _producer;
    private readonly KafkaOptions _options;
    private readonly TopicReadiness _readiness;

    public EventPublisher(IOptions<KafkaOptions> options, TopicReadiness readiness)
    {
        _options = options.Value;
        _readiness = readiness;
        _producer = new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            Acks = Acks.All,
            EnableIdempotence = true,
        }).Build();
    }

    public async Task PublishAsync<T>(string topicBase, string key, T payload, CancellationToken ct = default)
    {
        // ждём, пока топики будут созданы, если брокер недоступен, это TimeoutException
        await _readiness.Ready.WaitAsync(TimeSpan.FromSeconds(20), ct);

        var message = new Message<string, string> { Key = key, Value = JsonSerializer.Serialize(payload, Json) };
        await _producer.ProduceAsync(_options.Topic(topicBase), message, ct);
    }

    public void Dispose()
    {
        _producer.Flush(TimeSpan.FromSeconds(5));
        _producer.Dispose();
    }
}

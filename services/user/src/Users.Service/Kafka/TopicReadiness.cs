namespace Users.Service.Kafka;

// сигнал, что топики созданы или уже существуют: producer ждёт его перед отправкой
public sealed class TopicReadiness
{
    private readonly TaskCompletionSource _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Ready => _tcs.Task;

    public void SetReady() => _tcs.TrySetResult();
}

// создаёт топики в фоне при старте: сервис поднимается и без брокера, а отправка ждёт готовности
public sealed class TopicBootstrapService(
    Microsoft.Extensions.Options.IOptions<Users.Service.Options.KafkaOptions> options,
    TopicReadiness readiness,
    ILogger<TopicBootstrapService> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (await TopicBootstrap.EnsureAsync(options.Value, log, stoppingToken)) readiness.SetReady();
    }
}

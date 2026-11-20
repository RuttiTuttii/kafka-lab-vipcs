using NotificationService.Worker.Application;
using NotificationService.Worker.Infrastructure.Kafka;
using NotificationService.Worker.Infrastructure.Persistence;

var builder = Host.CreateApplicationBuilder(args);

// Configure Kafka options
builder.Services.Configure<KafkaSettings>(builder.Configuration.GetSection(KafkaSettings.SectionName));

// Register Persistence (Thread-safe JSON-backed repository with fast in-memory indexing)
builder.Services.AddSingleton<INotificationRepository, JsonNotificationRepository>();

// Register Application Services
builder.Services.AddSingleton<INotificationProcessor, NotificationProcessor>();
builder.Services.AddSingleton<INotificationHistoryService, NotificationHistoryService>();

// Register Kafka Consumer Worker
builder.Services.AddHostedService<KafkaNotificationConsumerWorker>();

var host = builder.Build();
await host.RunAsync();

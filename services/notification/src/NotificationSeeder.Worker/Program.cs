using NotificationSeeder.Worker;

var builder = Host.CreateApplicationBuilder(args);

// Configure Kafka seeder options
builder.Services.Configure<SeederSettings>(builder.Configuration.GetSection(SeederSettings.SectionName));

// Register Seeder Worker
builder.Services.AddHostedService<SeederWorker>();

var host = builder.Build();
await host.RunAsync();

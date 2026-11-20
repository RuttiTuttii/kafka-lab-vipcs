using PaymentService;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSingleton<PaymentStore>();
builder.Services.AddHostedService<PaymentWorker>();

var host = builder.Build();
host.Run();
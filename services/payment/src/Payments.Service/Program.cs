using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Payments.Service.Domain;
using Payments.Service.Kafka;
using Payments.Service.Options;

var builder = WebApplication.CreateBuilder(args);

// локальные правки (адрес брокера дома) лежат в appsettings.Local.json, он в .gitignore
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);

builder.Services.Configure<KafkaOptions>(builder.Configuration.GetSection("Kafka"));
builder.Services.Configure<PaymentOptions>(builder.Configuration.GetSection("Payment"));
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
// состояние платежей в файле, чтобы сервис можно было запускать и останавливать независимо от остальных
builder.Services.AddSingleton(new PaymentStore(builder.Configuration["Persistence:Path"]));
builder.Services.AddSingleton<TopicReadiness>();
builder.Services.AddHostedService<TopicBootstrapService>();
builder.Services.AddSingleton<EventPublisher>();
builder.Services.AddSingleton<PaymentProcessor>();
builder.Services.AddHostedService<OrderCreatedConsumer>();

var app = builder.Build();

// брокер недоступен: отвечаем 503 вместо 500, чтобы причина была понятна
app.UseExceptionHandler(errApp => errApp.Run(async ctx =>
{
    var err = ctx.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
    ctx.Response.StatusCode = err is TimeoutException or Confluent.Kafka.KafkaException ? 503 : 500;
    await ctx.Response.WriteAsJsonAsync(new { error = err is TimeoutException ? "kafka is not available, topics are not ready" : err?.Message });
}));

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// информация о платежах и заказах, которые пришли из order.created
app.MapGet("/payments", (PaymentStore s) => Results.Ok(s.List()));

app.MapGet("/payments/{orderId:guid}", (Guid orderId, PaymentStore s) =>
    s.Get(orderId) is { } p ? Results.Ok(p) : Results.NotFound(new { error = $"no payment for order {orderId}" }));

// подтверждение оплаты: публикует payment.completed
app.MapPost("/payments/{orderId:guid}/confirm", async (Guid orderId, PaymentProcessor proc, CancellationToken ct) =>
{
    var d = await proc.ConfirmAsync(orderId, ct);
    if (d.Payment is null) return Results.NotFound(new { error = $"no payment for order {orderId}" });
    return d.Changed ? Results.Ok(d.Payment) : Results.Conflict(new { error = "payment is already decided", payment = d.Payment });
});

// отказ: публикует payment.failed
app.MapPost("/payments/{orderId:guid}/decline", async (Guid orderId, DeclineRequest? req, PaymentProcessor proc, CancellationToken ct) =>
{
    var d = await proc.DeclineAsync(orderId, req?.Reason ?? "declined_manually", ct);
    if (d.Payment is null) return Results.NotFound(new { error = $"no payment for order {orderId}" });
    return d.Changed ? Results.Ok(d.Payment) : Results.Conflict(new { error = "payment is already decided", payment = d.Payment });
});

app.Run();

record DeclineRequest(string? Reason);

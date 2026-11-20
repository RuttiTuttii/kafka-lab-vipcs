using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Orders.Service.Domain;
using Orders.Service.Kafka;
using Orders.Service.Options;

var builder = WebApplication.CreateBuilder(args);

// локальные правки (адрес брокера дома) лежат в appsettings.Local.json, он в .gitignore
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);

builder.Services.Configure<KafkaOptions>(builder.Configuration.GetSection("Kafka"));
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
// состояние заказов в файле, чтобы сервис можно было запускать и останавливать независимо от остальных
builder.Services.AddSingleton(new OrderStore(builder.Configuration["Persistence:Path"]));
builder.Services.AddSingleton<TopicReadiness>();
builder.Services.AddHostedService<TopicBootstrapService>();
builder.Services.AddSingleton<EventPublisher>();
builder.Services.AddSingleton<OrderFlow>();
builder.Services.AddHostedService<OrderEventsConsumer>();

var app = builder.Build();

// брокер недоступен: отвечаем 503 вместо 500, чтобы причина была понятна
app.UseExceptionHandler(errApp => errApp.Run(async ctx =>
{
    var err = ctx.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
    ctx.Response.StatusCode = err is TimeoutException or Confluent.Kafka.KafkaException ? 503 : 500;
    await ctx.Response.WriteAsJsonAsync(new { error = err is TimeoutException ? "kafka is not available, topics are not ready" : err?.Message });
}));

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// создание заказа: публикует order.created
app.MapPost("/orders", async (CreateOrderRequest req, OrderFlow flow, CancellationToken ct) =>
{
    try
    {
        var lines = (req.Items ?? []).Select(i => new OrderLine(i.Sku, i.ProductName, i.Quantity, i.UnitPrice)).ToList();
        var order = await flow.CreateAsync(req.UserId, lines, ct);
        return Results.Created($"/orders/{order.Id}", order);
    }
    catch (ArgumentException e) { return Results.BadRequest(new { error = e.Message }); }
});

// просмотр заказов
app.MapGet("/orders", (OrderStore s) => Results.Ok(s.List()));

app.MapGet("/orders/{id:guid}", (Guid id, OrderStore s) =>
    s.Get(id) is { } o ? Results.Ok(o) : Results.NotFound(new { error = $"order {id} not found" }));

// отмена заказа: публикует order.cancelled
app.MapPost("/orders/{id:guid}/cancel", async (Guid id, CancelRequest? req, OrderFlow flow, CancellationToken ct) =>
{
    var t = await flow.CancelAsync(id, req?.Reason ?? "cancelled by user", ct);
    if (t.Order is null) return Results.NotFound(new { error = $"order {id} not found" });
    return t.Changed ? Results.Ok(t.Order) : Results.Conflict(new { error = "order is already cancelled", order = t.Order });
});

// изменение статуса: Confirmed вручную или Cancelled
app.MapPatch("/orders/{id:guid}/status", async (Guid id, StatusRequest req, OrderStore s, OrderFlow flow, CancellationToken ct) =>
{
    if (string.Equals(req.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
    {
        var c = await flow.CancelAsync(id, "status set to Cancelled", ct);
        return c.Order is null ? Results.NotFound() : Results.Ok(c.Order);
    }
    if (string.Equals(req.Status, "Confirmed", StringComparison.OrdinalIgnoreCase))
    {
        var t = s.Confirm(id);
        return t.Order is null ? Results.NotFound() : Results.Ok(t.Order);
    }
    return Results.BadRequest(new { error = "status must be Confirmed or Cancelled" });
});

app.Run();

record OrderItemRequest(string Sku, string? ProductName, int Quantity, decimal UnitPrice);
record CreateOrderRequest(Guid UserId, List<OrderItemRequest>? Items);
record CancelRequest(string? Reason);
record StatusRequest(string Status);

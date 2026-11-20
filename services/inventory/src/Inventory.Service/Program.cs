using Inventory.Service.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Inventory.Service.Domain;
using Inventory.Service.Kafka;
using Inventory.Service.Options;

var builder = WebApplication.CreateBuilder(args);

// локальные правки (адрес брокера дома) лежат в appsettings.Local.json, он в .gitignore
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);

builder.Services.Configure<KafkaOptions>(builder.Configuration.GetSection("Kafka"));

// склад заполняем из конфига, список sku общий с командой order
var store = new InventoryStore();
var seed = builder.Configuration.GetSection("Seed:Stock").Get<Dictionary<string, int>>() ?? new();
foreach (var (sku, qty) in seed) store.Set(sku, qty);

builder.Services.AddSingleton(store);
builder.Services.AddSingleton<EventPublisher>();
builder.Services.AddHostedService<OrderCreatedConsumer>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// просмотр остатков
app.MapGet("/items", (InventoryStore s) => Results.Ok(s.Snapshot().Select(p => new { sku = p.Key, quantity = p.Value })));

app.MapGet("/items/{sku}", (string sku, InventoryStore s) =>
    s.TryGet(sku, out var qty) ? Results.Ok(new { sku, quantity = qty }) : Results.NotFound(new { error = $"unknown sku {sku}" }));

// добавление товара
app.MapPost("/items", (ItemDto dto, InventoryStore s) =>
{
    if (s.TryGet(dto.Sku, out _)) return Results.Conflict(new { error = $"sku {dto.Sku} already exists" });
    try { s.Set(dto.Sku, dto.Quantity); }
    catch (ArgumentException e) { return Results.BadRequest(new { error = e.Message }); }
    return Results.Created($"/items/{dto.Sku}", dto);
});

// изменение количества
app.MapPatch("/items/{sku}", (string sku, QuantityDto dto, InventoryStore s) =>
{
    if (!s.TryGet(sku, out _)) return Results.NotFound(new { error = $"unknown sku {sku}" });
    try { s.Set(sku, dto.Quantity); }
    catch (ArgumentException e) { return Results.BadRequest(new { error = e.Message }); }
    return Results.Ok(new { sku, quantity = dto.Quantity });
});

// проверка достаточности остатков без списания
app.MapPost("/check", (CheckDto dto, InventoryStore s) =>
{
    var missing = dto.Items
        .Where(i => !s.TryGet(i.Sku, out var have) || have < i.Quantity)
        .Select(i => i.Sku).ToList();
    return Results.Ok(new { sufficient = missing.Count == 0, missing });
});

// сидер: шлёт тестовые order.created в брокер (нормальный, нехватка, неизвестный sku, дубликат)
app.MapPost("/seed/orders", async (EventPublisher publisher, CancellationToken ct) =>
{
    OrderCreated Make(Guid orderId, string sku, int qty) => new(
        Guid.NewGuid(), orderId, Guid.NewGuid(), [new OrderItem(sku, qty, 100m)], 100m * qty, "RUB", DateTimeOffset.UtcNow);

    var duplicateId = Guid.NewGuid();
    var orders = new[]
    {
        Make(Guid.NewGuid(), "sku-001", 2),
        Make(Guid.NewGuid(), "sku-004", 99),
        Make(Guid.NewGuid(), "sku-unknown", 1),
        Make(duplicateId, "sku-002", 1),
        Make(duplicateId, "sku-002", 1),
    };
    foreach (var o in orders) await publisher.PublishAsync(TopicNames.OrderCreated, o.OrderId.ToString(), o, ct);
    return Results.Ok(new { sent = orders.Length, orderIds = orders.Select(o => o.OrderId) });
});

app.Run();

record ItemDto(string Sku, int Quantity);
record QuantityDto(int Quantity);
record CheckDto(List<ItemDto> Items);

// нужен для тестов через WebApplicationFactory
public partial class Program;

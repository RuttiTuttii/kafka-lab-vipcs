namespace Inventory.Service.Contracts;

// схемы событий, источник правды: contracts/events.md
public sealed record OrderItem(string Sku, string? ProductName, int Quantity, decimal UnitPrice);

public sealed record OrderCreated(
    Guid EventId, Guid OrderId, Guid UserId, List<OrderItem> Items,
    decimal TotalAmount, string Currency, string Status, DateTimeOffset CreatedAt);

public sealed record ReservedItem(string Sku, int Quantity);

public sealed record InventoryReserved(
    Guid EventId, Guid OrderId, List<ReservedItem> Items, DateTimeOffset ReservedAt);

public sealed record InventoryFailed(
    Guid EventId, Guid OrderId, string Reason, DateTimeOffset FailedAt);

public static class TopicNames
{
    public const string OrderCreated = "order.created";
    public const string InventoryReserved = "inventory.reserved";
    public const string InventoryFailed = "inventory.failed";

    // все топики команды, их создаёт TopicBootstrap
    public static readonly string[] All =
    [
        "user.registered", "order.created", "order.cancelled",
        "payment.completed", "payment.failed", InventoryReserved, InventoryFailed,
    ];
}

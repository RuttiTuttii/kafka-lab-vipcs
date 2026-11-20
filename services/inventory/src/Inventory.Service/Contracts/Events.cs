namespace Inventory.Service.Contracts;

// схемы событий, источник правды: contracts/events.md в kafka-lab-vipcs
public sealed record OrderItem(string Sku, int Quantity, decimal Price = 0);

public sealed record OrderCreated(
    Guid EventId, Guid OrderId, Guid UserId, List<OrderItem> Items,
    decimal Amount, string Currency, DateTimeOffset CreatedAt);

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
}

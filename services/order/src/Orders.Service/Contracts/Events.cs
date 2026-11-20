namespace Orders.Service.Contracts;

// схемы событий, источник правды: contracts/events.md
public sealed record OrderItemEvent(string Sku, string? ProductName, int Quantity, decimal UnitPrice);

public sealed record OrderCreated(
    Guid EventId, Guid OrderId, Guid UserId, List<OrderItemEvent> Items,
    decimal TotalAmount, string Currency, string Status, DateTimeOffset CreatedAt);

public sealed record OrderCancelled(
    Guid EventId, Guid OrderId, Guid UserId, string Reason, string PreviousStatus, DateTimeOffset CancelledAt);

// входящие события payment.* и inventory.* нужны только ради orderId и причины
public sealed record IncomingEvent(Guid OrderId, string? Reason, string? FailureReason);

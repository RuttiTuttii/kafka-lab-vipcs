namespace Orders.Service.Domain;

public enum OrderStatus { Created, Confirmed, Cancelled }

public sealed record OrderLine(string Sku, string? ProductName, int Quantity, decimal UnitPrice);

// неизменяемый снимок заказа для API и событий
public sealed record OrderView(
    Guid Id, Guid UserId, IReadOnlyList<OrderLine> Items, decimal TotalAmount, string Currency,
    string Status, bool Paid, bool Reserved, string? CancelReason,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

// Changed = статус действительно поменялся, PreviousStatus нужен для order.cancelled
public sealed record Transition(OrderView? Order, bool Changed, string? PreviousStatus);

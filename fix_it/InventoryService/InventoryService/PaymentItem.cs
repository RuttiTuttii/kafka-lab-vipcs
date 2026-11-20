using System.Text.Json.Serialization;

namespace PaymentService;

public sealed record PaymentRecord(
    Guid PaymentId,
    Guid OrderId,
    Guid UserId,
    decimal Amount,
    string Status,
    string? Reason,
    DateTimeOffset ProcessedAt
);

public record OrderItemDto(
    [property: JsonPropertyName("sku")] string Sku,
    [property: JsonPropertyName("quantity")] int Quantity,
    [property: JsonPropertyName("price")] decimal Price
);

public record OrderCreatedEvent(
    [property: JsonPropertyName("eventId")] Guid EventId,
    [property: JsonPropertyName("orderId")] Guid OrderId,
    [property: JsonPropertyName("userId")] Guid UserId,
    [property: JsonPropertyName("items")] List<OrderItemDto> Items,
    [property: JsonPropertyName("amount")] decimal Amount,
    [property: JsonPropertyName("currency")] string Currency,
    [property: JsonPropertyName("createdAt")] DateTimeOffset CreatedAt
);

public record PaymentCompleteEvent(
    [property: JsonPropertyName("eventId")] Guid EventId,
    [property: JsonPropertyName("orderId")] Guid OrderId,
    [property: JsonPropertyName("paymentId")] Guid PaymentId,
    [property: JsonPropertyName("amount")] decimal Amount,
    [property: JsonPropertyName("paidAt")] DateTimeOffset PaidAt
);
public record PaymentFailedEvent(
    [property: JsonPropertyName("eventId")] Guid EventId,
    [property: JsonPropertyName("orderId")] Guid OrderId,
    [property: JsonPropertyName("reason")] string Reason,
    [property: JsonPropertyName("failedAt")] DateTimeOffset FailedAt
);
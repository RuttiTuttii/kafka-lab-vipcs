namespace NotificationService.Worker.Contracts;

public record OrderItemDto(
    Guid? ProductId,
    string? ProductName,
    int Quantity,
    decimal UnitPrice,
    string? Sku = null,
    decimal? Price = null
)
{
    public string DisplayName => !string.IsNullOrWhiteSpace(ProductName) 
        ? ProductName 
        : (!string.IsNullOrWhiteSpace(Sku) ? Sku : "Product");

    public decimal EffectiveUnitPrice => UnitPrice > 0 ? UnitPrice : (Price ?? 0m);
}

public record OrderCreatedPayload(
    Guid OrderId,
    Guid UserId,
    List<OrderItemDto>? Items,
    decimal TotalAmount,
    string? Status,
    DateTimeOffset? CreatedAt,
    Guid? EventId = null,
    decimal? Amount = null,
    string? Currency = null
)
{
    public decimal EffectiveTotalAmount => TotalAmount > 0 ? TotalAmount : (Amount ?? 0m);
    public string EffectiveStatus => !string.IsNullOrWhiteSpace(Status) ? Status : "Created";
    public DateTimeOffset EffectiveCreatedAt => CreatedAt ?? DateTimeOffset.UtcNow;
}

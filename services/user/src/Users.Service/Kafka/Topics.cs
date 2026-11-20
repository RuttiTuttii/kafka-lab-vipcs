namespace Users.Service.Kafka;

// базовые имена всех топиков команды, суффикс добавляется из конфига
public static class Topics
{
    public const string UserRegistered = "user.registered";
    public const string OrderCreated = "order.created";
    public const string OrderCancelled = "order.cancelled";
    public const string PaymentCompleted = "payment.completed";
    public const string PaymentFailed = "payment.failed";
    public const string InventoryReserved = "inventory.reserved";
    public const string InventoryFailed = "inventory.failed";

    public static readonly string[] All =
    [
        UserRegistered, OrderCreated, OrderCancelled,
        PaymentCompleted, PaymentFailed, InventoryReserved, InventoryFailed,
    ];
}

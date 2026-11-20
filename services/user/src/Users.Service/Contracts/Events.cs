namespace Users.Service.Contracts;

public sealed record UserRegistered(Guid EventId, Guid UserId, string Email, string Name, DateTimeOffset RegisteredAt);

// входящее order.created: нужен только пользователь и заказ
public sealed record OrderCreatedIn(Guid EventId, Guid OrderId, Guid UserId);

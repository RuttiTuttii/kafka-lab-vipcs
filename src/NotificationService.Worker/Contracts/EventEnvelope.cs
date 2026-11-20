namespace NotificationService.Worker.Contracts;

public record EventEnvelope<T>(
    Guid EventId,
    string EventType,
    DateTimeOffset Timestamp,
    T Payload
);

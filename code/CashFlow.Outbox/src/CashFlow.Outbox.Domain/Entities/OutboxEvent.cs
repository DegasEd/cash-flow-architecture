namespace CashFlow.Outbox.Domain.Entities;

public class OutboxEvent
{
    public Guid Id { get; init; }

    public Guid EntryId { get; init; }

    public string EventType { get; init; } = string.Empty;

    public string Payload { get; init; } = string.Empty;

    public DateTime CreatedAt { get; init; }

    public DateTime? PublishedAt { get; init; }
}
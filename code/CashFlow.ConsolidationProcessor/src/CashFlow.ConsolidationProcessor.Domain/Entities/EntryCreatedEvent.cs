namespace CashFlow.ConsolidationProcessor.Domain.Entities;

public class EntryCreatedEvent
{
    public Guid EventId { get; init; }

    public Guid EntryId { get; init; }

    public int AmountInCents { get; init; }

    public string Type { get; init; } = string.Empty;

    public DateOnly OccurredAt { get; init; }

    public DateTime CreatedAt { get; init; }
}
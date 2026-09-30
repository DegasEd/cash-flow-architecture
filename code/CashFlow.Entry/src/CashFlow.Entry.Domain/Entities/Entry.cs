using CashFlow.Entry.Domain.Enums;

namespace CashFlow.Entry.Domain.Entities;

public class Entry
{
    public Guid Id { get; private set; }

    public int AmountInCents { get; private set; }

    public EntryType Type { get; private set; }

    public DateOnly OccurredAt { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public Entry(
        Guid id,
        int amountInCents,
        EntryType type,
        DateOnly occurredAt)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Entry id cannot be empty.", nameof(id));

        if (amountInCents <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(amountInCents),
                "Entry amount must be greater than zero.");

        if (!Enum.IsDefined(type))
            throw new ArgumentOutOfRangeException(
                nameof(type),
                "Invalid entry type.");

        Id = id;
        AmountInCents = amountInCents;
        Type = type;
        OccurredAt = occurredAt;

        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }
}
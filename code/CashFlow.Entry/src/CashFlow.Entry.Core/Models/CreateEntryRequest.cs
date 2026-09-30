using CashFlow.Entry.Domain.Enums;

namespace CashFlow.Entry.Core.Models;

public record CreateEntryRequest(
    int AmountInCents,
    EntryType Type,
    DateOnly OccurredAt);
namespace CashFlow.ConsolidationQuery.Domain.Entities;

public class DailyConsolidation
{
    public DateOnly Date { get; init; }

    public int BalanceInCents { get; init; }

    public DateTime UpdatedAt { get; init; }
}
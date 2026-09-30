using CashFlow.ConsolidationQuery.Domain.Entities;

namespace CashFlow.ConsolidationQuery.Core.Interfaces;

public interface IConsolidationQueryService
{
    Task<DailyConsolidation?> GetByDateAsync(
        DateOnly date,
        CancellationToken cancellationToken = default);
}
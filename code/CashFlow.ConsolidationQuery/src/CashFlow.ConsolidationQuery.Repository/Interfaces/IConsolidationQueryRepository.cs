using CashFlow.ConsolidationQuery.Domain.Entities;

namespace CashFlow.ConsolidationQuery.Repository.Interfaces;

public interface IConsolidationQueryRepository
{
    Task<DailyConsolidation?> GetByDateAsync(
        DateOnly date,
        CancellationToken cancellationToken = default);
}
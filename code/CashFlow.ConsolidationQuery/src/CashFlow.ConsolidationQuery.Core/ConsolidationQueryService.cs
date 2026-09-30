using CashFlow.ConsolidationQuery.Core.Interfaces;
using CashFlow.ConsolidationQuery.Domain.Entities;
using CashFlow.ConsolidationQuery.Repository.Interfaces;

namespace CashFlow.ConsolidationQuery.Core;

public class ConsolidationQueryService : IConsolidationQueryService
{
    private readonly IConsolidationQueryRepository _repository;

    public ConsolidationQueryService(
        IConsolidationQueryRepository repository)
    {
        _repository = repository;
    }

    public Task<DailyConsolidation?> GetByDateAsync(
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        return _repository.GetByDateAsync(
            date,
            cancellationToken);
    }
}
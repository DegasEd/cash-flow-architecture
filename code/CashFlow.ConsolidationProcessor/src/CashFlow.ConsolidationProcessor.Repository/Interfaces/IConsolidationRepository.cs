using CashFlow.ConsolidationProcessor.Domain.Entities;

namespace CashFlow.ConsolidationProcessor.Repository.Interfaces;

public interface IConsolidationRepository
{
    Task<bool> ProcessAsync(
        EntryCreatedEvent entryEvent,
        CancellationToken cancellationToken = default);
}
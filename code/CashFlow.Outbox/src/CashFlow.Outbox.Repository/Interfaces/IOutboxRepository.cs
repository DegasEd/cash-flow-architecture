using CashFlow.Outbox.Domain.Entities;

namespace CashFlow.Outbox.Repository.Interfaces;

public interface IOutboxRepository
{
    Task<IReadOnlyCollection<OutboxEvent>> GetPendingAsync(
        int batchSize,
        CancellationToken cancellationToken = default);

    Task MarkAsPublishedAsync(
        Guid eventId,
        DateTime publishedAt,
        CancellationToken cancellationToken = default);
}
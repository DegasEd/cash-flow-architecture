namespace CashFlow.Outbox.Core.Interfaces;

public interface IOutboxPublisherService
{
    Task PublishPendingAsync(
        CancellationToken cancellationToken = default);
}
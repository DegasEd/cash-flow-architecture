using Confluent.Kafka;
using CashFlow.Outbox.Core.Interfaces;
using CashFlow.Outbox.Repository.Interfaces;

namespace CashFlow.Outbox.Core;

public class OutboxPublisherService : IOutboxPublisherService
{
    private const string Topic = "entry-events";
    private const int BatchSize = 100;

    private readonly IOutboxRepository _repository;
    private readonly IProducer<string, string> _producer;

    public OutboxPublisherService(
        IOutboxRepository repository,
        IProducer<string, string> producer)
    {
        _repository = repository;
        _producer = producer;
    }

    public async Task PublishPendingAsync(
        CancellationToken cancellationToken = default)
    {
        var events = await _repository.GetPendingAsync(
            BatchSize,
            cancellationToken);

        foreach (var outboxEvent in events)
        {
            var message = new Message<string, string>
            {
                Key = outboxEvent.EntryId.ToString(),
                Value = outboxEvent.Payload
            };

            await _producer.ProduceAsync(
                Topic,
                message,
                cancellationToken);

            await _repository.MarkAsPublishedAsync(
                outboxEvent.Id,
                DateTime.UtcNow,
                cancellationToken);
        }
    }
}
using System.Text.Json;
using CashFlow.ConsolidationProcessor.Core.Interfaces;
using CashFlow.ConsolidationProcessor.Domain.Entities;
using CashFlow.ConsolidationProcessor.Repository.Interfaces;

namespace CashFlow.ConsolidationProcessor.Core;

public class ConsolidationService : IConsolidationService
{
    private readonly IConsolidationRepository _repository;

    public ConsolidationService(
        IConsolidationRepository repository)
    {
        _repository = repository;
    }

    public async Task<bool> ProcessAsync(
        string payload,
        CancellationToken cancellationToken = default)
    {
        var entryEvent =
            JsonSerializer.Deserialize<EntryCreatedEvent>(payload)
            ?? throw new InvalidOperationException(
                "Unable to deserialize EntryCreated event.");

        if (entryEvent.EventId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "EntryCreated event does not contain a valid EventId.");
        }

        if (entryEvent.EntryId == Guid.Empty)
        {
            throw new InvalidOperationException(
                "EntryCreated event does not contain a valid EntryId.");
        }

        if (entryEvent.AmountInCents <= 0)
        {
            throw new InvalidOperationException(
                "EntryCreated event contains an invalid amount.");
        }

        return await _repository.ProcessAsync(
            entryEvent,
            cancellationToken);
    }
}
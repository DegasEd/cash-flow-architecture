using CashFlow.Entry.Core.Interfaces;
using CashFlow.Entry.Core.Models;
using EntryEntity = CashFlow.Entry.Domain.Entities.Entry;
using CashFlow.Entry.Repository.Interfaces;

namespace CashFlow.Entry.Core.Services;

public class EntryService : IEntryService
{
    private readonly IEntryRepository _entryRepository;

    public EntryService(IEntryRepository entryRepository)
    {
        _entryRepository = entryRepository;
    }

    public async Task<EntryEntity> CreateAsync(
        CreateEntryRequest request,
        CancellationToken cancellationToken = default)
    {
        var entry = new EntryEntity(
            Guid.NewGuid(),
            request.AmountInCents,
            request.Type,
            request.OccurredAt);

        await _entryRepository.AddAsync(entry, cancellationToken);

        return entry;
    }
}
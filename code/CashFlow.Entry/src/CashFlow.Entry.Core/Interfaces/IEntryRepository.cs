using EntryEntity = CashFlow.Entry.Domain.Entities.Entry;

namespace CashFlow.Entry.Core.Interfaces;

public interface IEntryRepository
{
    Task AddAsync(
        EntryEntity entry,
        CancellationToken cancellationToken = default);
}
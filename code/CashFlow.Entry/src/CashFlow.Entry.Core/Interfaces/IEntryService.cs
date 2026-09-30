using CashFlow.Entry.Core.Models;
using EntryEntity = CashFlow.Entry.Domain.Entities.Entry;

namespace CashFlow.Entry.Core.Interfaces;

public interface IEntryService
{
    Task<EntryEntity> CreateAsync(
        CreateEntryRequest request,
        CancellationToken cancellationToken = default);
}
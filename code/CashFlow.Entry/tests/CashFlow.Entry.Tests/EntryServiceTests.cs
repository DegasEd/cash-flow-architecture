using CashFlow.Entry.Core.Models;
using CashFlow.Entry.Core.Services;
using CashFlow.Entry.Domain.Enums;
using CashFlow.Entry.Repository.Interfaces;
using Moq;
using EntryEntity = CashFlow.Entry.Domain.Entities.Entry;

namespace CashFlow.Entry.Tests;

public class EntryServiceTests
{
    [Fact]
    public async Task CreateAsync_WithValidRequest_PersistsEntry()
    {
        // Arrange
        var repository = new Mock<IEntryRepository>();
        var service = new EntryService(repository.Object);

        var request = new CreateEntryRequest(
            5000,
            EntryType.Credit,
            new DateOnly(2026, 9, 30));

        // Act
        var entry = await service.CreateAsync(request);

        // Assert
        Assert.Equal(5000, entry.AmountInCents);
        Assert.Equal(EntryType.Credit, entry.Type);
        Assert.Equal(new DateOnly(2026, 9, 30), entry.OccurredAt);
        Assert.NotEqual(Guid.Empty, entry.Id);

        repository.Verify(
            x => x.AddAsync(
                It.Is<EntryEntity>(e =>
                    e.Id == entry.Id &&
                    e.AmountInCents == 5000 &&
                    e.Type == EntryType.Credit &&
                    e.OccurredAt == new DateOnly(2026, 9, 30)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WithInvalidAmount_DoesNotPersistEntry()
    {
        // Arrange
        var repository = new Mock<IEntryRepository>();
        var service = new EntryService(repository.Object);

        var request = new CreateEntryRequest(
            0,
            EntryType.Credit,
            new DateOnly(2026, 9, 30));

        // Act
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.CreateAsync(request));

        // Assert
        repository.Verify(
            x => x.AddAsync(
                It.IsAny<EntryEntity>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
using System.Text.Json;
using CashFlow.ConsolidationProcessor.Core;
using CashFlow.ConsolidationProcessor.Domain.Entities;
using CashFlow.ConsolidationProcessor.Repository.Interfaces;
using Moq;

namespace CashFlow.ConsolidationProcessor.Tests;

public class ConsolidationServiceTests
{
    [Fact]
    public async Task ProcessAsync_WithValidPayload_DelegatesToRepository()
    {
        // Arrange
        var repository = new Mock<IConsolidationRepository>();

        repository
            .Setup(x => x.ProcessAsync(
                It.IsAny<EntryCreatedEvent>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = new ConsolidationService(repository.Object);

        var eventId = Guid.NewGuid();
        var entryId = Guid.NewGuid();

        var payload = JsonSerializer.Serialize(new
        {
            EventId = eventId,
            EntryId = entryId,
            AmountInCents = 5000,
            Type = "Credit",
            OccurredAt = new DateOnly(2026, 9, 30),
            CreatedAt = DateTime.UtcNow
        });

        // Act
        var result = await service.ProcessAsync(payload);

        // Assert
        Assert.True(result);

        repository.Verify(
            x => x.ProcessAsync(
                It.Is<EntryCreatedEvent>(e =>
                    e.EventId == eventId &&
                    e.EntryId == entryId &&
                    e.AmountInCents == 5000 &&
                    e.Type == "Credit" &&
                    e.OccurredAt == new DateOnly(2026, 9, 30)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_WithInvalidAmount_DoesNotCallRepository()
    {
        // Arrange
        var repository = new Mock<IConsolidationRepository>();
        var service = new ConsolidationService(repository.Object);

        var payload = JsonSerializer.Serialize(new
        {
            EventId = Guid.NewGuid(),
            EntryId = Guid.NewGuid(),
            AmountInCents = 0,
            Type = "Credit",
            OccurredAt = new DateOnly(2026, 9, 30),
            CreatedAt = DateTime.UtcNow
        });

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ProcessAsync(payload));

        // Assert
        Assert.Equal(
            "EntryCreated event contains an invalid amount.",
            exception.Message);

        repository.Verify(
            x => x.ProcessAsync(
                It.IsAny<EntryCreatedEvent>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
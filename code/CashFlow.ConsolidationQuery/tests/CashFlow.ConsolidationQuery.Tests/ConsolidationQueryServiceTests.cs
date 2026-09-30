using CashFlow.ConsolidationQuery.Core;
using CashFlow.ConsolidationQuery.Domain.Entities;
using CashFlow.ConsolidationQuery.Repository.Interfaces;
using Moq;

namespace CashFlow.ConsolidationQuery.Tests;

public class ConsolidationQueryServiceTests
{
    [Fact]
    public async Task GetByDateAsync_WhenConsolidationExists_ReturnsConsolidation()
    {
        // Arrange
        var date = new DateOnly(2026, 9, 30);

        var expected = new DailyConsolidation
        {
            Date = date,
            BalanceInCents = 5000,
            UpdatedAt = DateTime.UtcNow
        };

        var repository = new Mock<IConsolidationQueryRepository>();

        repository
            .Setup(x => x.GetByDateAsync(
                date,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var service = new ConsolidationQueryService(repository.Object);

        // Act
        var result = await service.GetByDateAsync(date);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(date, result.Date);
        Assert.Equal(5000, result.BalanceInCents);

        repository.Verify(
            x => x.GetByDateAsync(
                date,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetByDateAsync_WhenConsolidationDoesNotExist_ReturnsNull()
    {
        // Arrange
        var date = new DateOnly(2026, 9, 29);

        var repository = new Mock<IConsolidationQueryRepository>();

        repository
            .Setup(x => x.GetByDateAsync(
                date,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((DailyConsolidation?)null);

        var service = new ConsolidationQueryService(repository.Object);

        // Act
        var result = await service.GetByDateAsync(date);

        // Assert
        Assert.Null(result);

        repository.Verify(
            x => x.GetByDateAsync(
                date,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
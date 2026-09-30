namespace CashFlow.ConsolidationProcessor.Core.Interfaces;

public interface IConsolidationService
{
    Task<bool> ProcessAsync(
        string payload,
        CancellationToken cancellationToken = default);
}
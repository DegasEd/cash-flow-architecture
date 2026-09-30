using Dapper;
using Npgsql;
using CashFlow.ConsolidationProcessor.Domain.Entities;
using CashFlow.ConsolidationProcessor.Repository.Interfaces;

namespace CashFlow.ConsolidationProcessor.Repository;

public class ConsolidationRepository : IConsolidationRepository
{
    private readonly string _connectionString;

    public ConsolidationRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<bool> ProcessAsync(
        EntryCreatedEvent entryEvent,
        CancellationToken cancellationToken = default)
    {
        const string insertProcessedEventSql = """
            INSERT INTO consolidation.processed_event
            (
                event_id,
                processed_at
            )
            VALUES
            (
                @EventId,
                @ProcessedAt
            )
            ON CONFLICT (event_id) DO NOTHING
            RETURNING event_id;
            """;

        const string upsertDailyConsolidationSql = """
            INSERT INTO consolidation.daily_consolidation
            (
                date,
                balance_in_cents,
                updated_at
            )
            VALUES
            (
                @Date,
                @Delta,
                @UpdatedAt
            )
            ON CONFLICT (date)
            DO UPDATE SET
                balance_in_cents =
                    consolidation.daily_consolidation.balance_in_cents
                    + EXCLUDED.balance_in_cents,
                updated_at = EXCLUDED.updated_at;
            """;

        await using var connection =
            new NpgsqlConnection(_connectionString);

        await connection.OpenAsync(cancellationToken);

        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var insertedEventId =
                await connection.QuerySingleOrDefaultAsync<Guid?>(
                    new CommandDefinition(
                        insertProcessedEventSql,
                        new
                        {
                            entryEvent.EventId,
                            ProcessedAt = DateTime.UtcNow
                        },
                        transaction,
                        cancellationToken: cancellationToken));

            if (insertedEventId is null)
            {
                await transaction.CommitAsync(cancellationToken);
                return false;
            }

            var delta = entryEvent.Type switch
            {
                "Credit" => entryEvent.AmountInCents,
                "Debit" => -entryEvent.AmountInCents,
                _ => throw new InvalidOperationException(
                    $"Unsupported entry type '{entryEvent.Type}'.")
            };

            await connection.ExecuteAsync(
                new CommandDefinition(
                    upsertDailyConsolidationSql,
                    new
                    {
                        Date = entryEvent.OccurredAt,
                        Delta = delta,
                        UpdatedAt = DateTime.UtcNow
                    },
                    transaction,
                    cancellationToken: cancellationToken));

            await transaction.CommitAsync(cancellationToken);

            return true;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
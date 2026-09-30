using Dapper;
using Npgsql;
using CashFlow.Outbox.Domain.Entities;
using CashFlow.Outbox.Repository.Interfaces;

namespace CashFlow.Outbox.Repository;

public class OutboxRepository : IOutboxRepository
{
    private readonly string _connectionString;

    public OutboxRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<IReadOnlyCollection<OutboxEvent>> GetPendingAsync(
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                id          AS Id,
                entry_id    AS EntryId,
                event_type  AS EventType,
                payload     AS Payload,
                created_at  AS CreatedAt,
                published_at AS PublishedAt
            FROM launch.outbox_event
            WHERE published_at IS NULL
            ORDER BY created_at
            LIMIT @BatchSize;
            """;

        await using var connection =
            new NpgsqlConnection(_connectionString);

        var events = await connection.QueryAsync<OutboxEvent>(
            new CommandDefinition(
                sql,
                new { BatchSize = batchSize },
                cancellationToken: cancellationToken));

        return events.ToList();
    }

    public async Task MarkAsPublishedAsync(
        Guid eventId,
        DateTime publishedAt,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE launch.outbox_event
            SET published_at = @PublishedAt
            WHERE id = @EventId
              AND published_at IS NULL;
            """;

        await using var connection =
            new NpgsqlConnection(_connectionString);

        await connection.ExecuteAsync(
            new CommandDefinition(
                sql,
                new
                {
                    EventId = eventId,
                    PublishedAt = publishedAt
                },
                cancellationToken: cancellationToken));
    }
}
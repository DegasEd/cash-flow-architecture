using System.Text.Json;
using Dapper;
using Npgsql;
using CashFlow.Entry.Repository.Interfaces;
using EntryEntity = CashFlow.Entry.Domain.Entities.Entry;

namespace CashFlow.Entry.Repository.Repositories;

public class EntryRepository : IEntryRepository
{
    private readonly string _connectionString;

    public EntryRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task AddAsync(
        EntryEntity entry,
        CancellationToken cancellationToken = default)
    {
        const string insertEntrySql = """
            INSERT INTO launch.entry
            (
                id,
                amount_in_cents,
                type,
                occurred_at,
                created_at,
                updated_at
            )
            VALUES
            (
                @Id,
                @AmountInCents,
                @Type,
                @OccurredAt,
                @CreatedAt,
                @UpdatedAt
            );
            """;

        const string insertOutboxSql = """
            INSERT INTO launch.outbox_event
            (
                id,
                entry_id,
                event_type,
                payload,
                created_at,
                published_at
            )
            VALUES
            (
                @Id,
                @EntryId,
                @EventType,
                CAST(@Payload AS jsonb),
                @CreatedAt,
                NULL
            );
            """;

        var outboxEventId = Guid.NewGuid();

        var payload = JsonSerializer.Serialize(new
        {
            EventId = outboxEventId,
            EntryId = entry.Id,
            entry.AmountInCents,
            Type = entry.Type.ToString(),
            entry.OccurredAt,
            entry.CreatedAt
        });

        await using var connection =
            new NpgsqlConnection(_connectionString);

        await connection.OpenAsync(cancellationToken);

        await using var transaction =
            await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    insertEntrySql,
                    new
                    {
                        entry.Id,
                        entry.AmountInCents,
                        Type = entry.Type.ToString().ToUpperInvariant(),
                        entry.OccurredAt,
                        entry.CreatedAt,
                        entry.UpdatedAt
                    },
                    transaction,
                    cancellationToken: cancellationToken));

            await connection.ExecuteAsync(
                new CommandDefinition(
                    insertOutboxSql,
                    new
                    {
                        Id = outboxEventId,
                        EntryId = entry.Id,
                        EventType = "EntryCreated",
                        Payload = payload,
                        CreatedAt = DateTime.UtcNow
                    },
                    transaction,
                    cancellationToken: cancellationToken));

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
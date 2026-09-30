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
        const string sql = """
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

        await using var connection = new NpgsqlConnection(_connectionString);

        await connection.ExecuteAsync(
            new CommandDefinition(
                sql,
                new
                {
                    entry.Id,
                    entry.AmountInCents,
                    Type = entry.Type.ToString().ToUpperInvariant(),
                    entry.OccurredAt,
                    entry.CreatedAt,
                    entry.UpdatedAt
                },
                cancellationToken: cancellationToken));
    }
}
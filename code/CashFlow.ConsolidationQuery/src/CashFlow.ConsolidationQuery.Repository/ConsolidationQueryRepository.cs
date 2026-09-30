using Dapper;
using Npgsql;
using CashFlow.ConsolidationQuery.Domain.Entities;
using CashFlow.ConsolidationQuery.Repository.Interfaces;

namespace CashFlow.ConsolidationQuery.Repository;

public class ConsolidationQueryRepository : IConsolidationQueryRepository
{
    private readonly string _connectionString;

    public ConsolidationQueryRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<DailyConsolidation?> GetByDateAsync(
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                date             AS Date,
                balance_in_cents AS BalanceInCents,
                updated_at       AS UpdatedAt
            FROM consolidation.daily_consolidation
            WHERE date = @Date;
            """;

        await using var connection =
            new NpgsqlConnection(_connectionString);

        return await connection.QuerySingleOrDefaultAsync<DailyConsolidation>(
            new CommandDefinition(
                sql,
                new { Date = date },
                cancellationToken: cancellationToken));
    }
}
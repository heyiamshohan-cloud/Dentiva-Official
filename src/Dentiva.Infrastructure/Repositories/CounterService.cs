using Dapper;
using Microsoft.Data.Sqlite;

namespace Dentiva.Infrastructure.Repositories;

/// <summary>
/// Atomic document numbering. The scope parameter allows yearly (or per
/// doctor) sequence resets without losing uniqueness of stored numbers.
/// </summary>
public sealed class CounterService
{
    private readonly ISqliteConnectionFactory _factory;

    public CounterService(ISqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    /// <summary>Returns the next value for (name, scope) inside the given transaction.</summary>
    public long Next(SqliteConnection connection, SqliteTransaction? tx, string name, string scope = "")
    {
        connection.Execute("""
            INSERT INTO counters(name, scope, value, updated_at) VALUES (@name, @scope, 0, @now)
            ON CONFLICT(name, scope) DO NOTHING
            """,
            new { name, scope, now = DateTimeOffset.UtcNow.ToString("o") }, tx);

        return connection.ExecuteScalar<long>(
            """
            UPDATE counters SET value = value + 1, updated_at = @now
            WHERE name = @name AND scope = @scope
            RETURNING value
            """,
            new { name, scope, now = DateTimeOffset.UtcNow.ToString("o") }, tx);
    }

    public long NextStandalone(string name, string scope = "")
    {
        using var connection = _factory.CreateOpenConnection();
        return Db.InTransaction(connection, c => Next(c, null, name, scope));
    }

    /// <summary>Scope for templates containing {YYYY} — sequences reset per year.</summary>
    public static string ScopeForTemplate(string template, DateOnly date) =>
        template.Contains("{YYYY", StringComparison.Ordinal) || template.Contains("{YY}", StringComparison.Ordinal)
            ? date.Year.ToString("0000")
            : string.Empty;
}

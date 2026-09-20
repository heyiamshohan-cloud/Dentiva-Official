using System.Data;
using System.Text;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Dentiva.Infrastructure.Persistence;

/// <summary>Small data-access helpers shared by all repositories.</summary>
public static class Db
{
    static Db()
    {
        TypeHandlers.Register();
    }

    /// <summary>Per-session pragmas. WAL is enabled once during initialization.</summary>
    public static void ConfigureSession(SqliteConnection connection)
    {
        connection.Execute("PRAGMA foreign_keys = ON;");
        connection.Execute("PRAGMA busy_timeout = 5000;");
        connection.Execute("PRAGMA synchronous = NORMAL;");
        connection.Execute("PRAGMA temp_store = MEMORY;");
        connection.Execute("PRAGMA cache_size = -8000;"); // ~8 MB page cache
    }

    /// <summary>Runs <paramref name="action"/> inside a single ACID transaction.</summary>
    public static async Task<T> InTransaction<T>(SqliteConnection connection, Func<SqliteConnection, T> action)
    {
        await using var tx = await connection.BeginTransactionAsync();
        try
        {
            var result = action(connection);
            await tx.CommitAsync();
            return result;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public static long InsertAndGetId(SqliteConnection connection, string sql, object? param = null) =>
        connection.ExecuteScalar<long>(sql, param);

    /// <summary>
    /// Splits a SQL script into statements. Trigger bodies (lines after a
    /// header ending with BEGIN, closed by an END; line) are kept intact.
    /// Line comments are stripped first. Scripts are authored by Dentiva, so
    /// the format is controlled and deterministic.
    /// </summary>
    public static IReadOnlyList<string> SplitScript(string script)
    {
        var statements = new List<string>();
        var current = new StringBuilder();
        var inTrigger = false;

        foreach (var rawLine in script.Replace("\r\n", "\n").Split('\n'))
        {
            var line = StripLineComment(rawLine).TrimEnd();
            if (line.Length == 0)
            {
                continue;
            }

            current.Append(line).Append('\n');
            var trimmed = line.Trim();

            if (inTrigger)
            {
                if (trimmed.Equals("END;", StringComparison.OrdinalIgnoreCase))
                {
                    Flush(current, statements);
                    inTrigger = false;
                }

                continue;
            }

            if (trimmed.EndsWith(";", StringComparison.Ordinal))
            {
                var statement = Flush(current, statements);
                if (statement is not null &&
                    statement.Contains(" TRIGGER ", StringComparison.OrdinalIgnoreCase) &&
                    statement.EndsWith("BEGIN", StringComparison.OrdinalIgnoreCase))
                {
                    inTrigger = true;
                }
            }
        }

        var tail = Flush(current, statements);
        if (tail is not null && tail.EndsWith(";", StringComparison.OrdinalIgnoreCase))
        {
            // trailing statement without semicolon was still flushed; nothing more
        }

        return statements;
    }

    private static string? Flush(StringBuilder current, List<string> statements)
    {
        var text = current.ToString().Trim();
        current.Clear();
        if (text.Length == 0)
        {
            return null;
        }

        statements.Add(text);
        return text;
    }

    private static string StripLineComment(string line)
    {
        for (var i = 0; i < line.Length - 1; i++)
        {
            if (line[i] == '-' && line[i + 1] == '-')
            {
                return line[..i];
            }
        }

        return line;
    }
}

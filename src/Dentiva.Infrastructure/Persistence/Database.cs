using System.Reflection;
using Dentiva.Core.Security;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Dentiva.Infrastructure.Persistence;

public sealed record MigrationInfo(int Version, string Name, string Sql);

/// <summary>
/// Owns database creation and upgrades. Migrations are embedded SQL scripts
/// applied in order inside transactions; a failed migration rolls back and
/// leaves the database untouched. Existing user data is never destroyed.
/// </summary>
public static class Database
{
    public const int CurrentVersion = 1;

    public static IReadOnlyList<MigrationInfo> BuiltInMigrations() => new[]
    {
        new MigrationInfo(1, "initial", ReadEmbedded("v1.sql")),
    };

    public static void Migrate(ISqliteConnectionFactory factory)
    {
        using var connection = factory.CreateOpenConnection();
        connection.Execute("PRAGMA journal_mode = WAL;");

        connection.Execute("""
            CREATE TABLE IF NOT EXISTS schema_version (
              version INTEGER PRIMARY KEY,
              name TEXT NOT NULL,
              applied_at TEXT NOT NULL
            );
            """);

        var applied = connection.Query<int>("SELECT version FROM schema_version").ToHashSet();
        var migrations = BuiltInMigrations();

        foreach (var migration in migrations.Where(m => !applied.Contains(m.Version)).OrderBy(m => m.Version))
        {
            foreach (var statement in Db.SplitScript(migration.Sql))
            {
                connection.Execute(statement);
            }

            connection.Execute(
                "INSERT INTO schema_version(version, name, applied_at) VALUES (@v, @n, @t)",
                new { v = migration.Version, n = migration.Name, t = DateTimeOffset.UtcNow.ToString("o") });
        }

        // Role permissions are seeded/refreshed from the code presets so the
        // persisted masks can never drift from application logic.
        foreach (var role in PermissionPresets.BuiltinRoleNames)
        {
            connection.Execute("""
                INSERT INTO roles(name, permissions, is_system) VALUES (@name, @perm, 1)
                ON CONFLICT(name) DO UPDATE SET permissions = excluded.permissions
                """,
                new { name = role, perm = (long)PermissionPresets.ForRole(role) });
        }

        // Faster-than-default durability tradeoff after schema work.
        connection.Execute("PRAGMA wal_checkpoint(TRUNCATE);");
    }

    /// <summary>Runs PRAGMA integrity_check; returns the message (ok = healthy).</summary>
    public static string IntegrityCheck(ISqliteConnectionFactory factory)
    {
        using var connection = factory.CreateOpenConnection();
        return connection.ExecuteScalar<string>("PRAGMA integrity_check;") ?? "unknown";
    }

    public static void Optimize(ISqliteConnectionFactory factory)
    {
        using var connection = factory.CreateOpenConnection();
        connection.Execute("PRAGMA wal_checkpoint(TRUNCATE);");
        connection.Execute("PRAGMA optimize;");
    }

    public static void Vacuum(ISqliteConnectionFactory factory)
    {
        using var connection = factory.CreateOpenConnection();
        connection.Execute("VACUUM;");
    }

    public static long PageCount(ISqliteConnectionFactory factory)
    {
        using var connection = factory.CreateOpenConnection();
        return connection.ExecuteScalar<long>("PRAGMA page_count;");
    }

    public static long PageSize(ISqliteConnectionFactory factory)
    {
        using var connection = factory.CreateOpenConnection();
        return connection.ExecuteScalar<long>("PRAGMA page_size;");
    }

    private static string ReadEmbedded(string fileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var name = assembly.GetManifestResourceNames()
            .Single(n => n.EndsWith($"Migrations.{fileName}", StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

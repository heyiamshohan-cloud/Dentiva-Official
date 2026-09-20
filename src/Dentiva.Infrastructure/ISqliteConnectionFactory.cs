namespace Dentiva.Infrastructure;

/// <summary>
/// Creates opened, configured SQLite connections. All sessions run with
/// WAL journaling, enforced foreign keys and a busy timeout so concurrent
/// UI/background access degrades gracefully instead of failing.
/// </summary>
public interface ISqliteConnectionFactory
{
    string DatabasePath { get; }

    Microsoft.Data.Sqlite.SqliteConnection CreateOpenConnection();
}

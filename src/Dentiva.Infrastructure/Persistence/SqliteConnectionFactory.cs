using Dentiva.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace Dentiva.Infrastructure;

/// <inheritdoc />
public sealed class SqliteConnectionFactory : ISqliteConnectionFactory
{
    private readonly string _databasePath;

    public SqliteConnectionFactory(string databasePath)
    {
        _databasePath = Path.GetFullPath(databasePath);
    }

    public string DatabasePath => _databasePath;

    public SqliteConnection CreateOpenConnection()
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
        };

        var connection = new SqliteConnection(builder.ToString());
        connection.Open();
        Db.ConfigureSession(connection);
        return connection;
    }
}

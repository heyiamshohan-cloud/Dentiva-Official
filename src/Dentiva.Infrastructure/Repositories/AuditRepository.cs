using Dentiva.Core.Domain;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Dentiva.Infrastructure.Repositories;

public sealed record AuditFilter(DateOnly? From, DateOnly? To, string? Action, string? Username, int Limit = 500);

/// <summary>Append-only audit trail. Entries never contain clinical content.</summary>
public sealed class AuditRepository
{
    private readonly ISqliteConnectionFactory _factory;

    public AuditRepository(ISqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    public void Record(SqliteConnection connection, SqliteTransaction? tx, SessionContext session, string action, string entity = "", string entityId = "", string details = "")
    {
        connection.Execute("""
            INSERT INTO audit_log(ts, user_id, username, action, entity, entity_id, details)
            VALUES (@ts, @uid, @username, @action, @entity, @entityId, @details)
            """,
            new
            {
                ts = DateTimeOffset.UtcNow.ToString("o"),
                uid = session.UserId == 0 ? (long?)null : session.UserId,
                username = session.Username,
                action,
                entity,
                entityId,
                details,
            }, tx);
    }

    public void RecordStandalone(SessionContext session, string action, string entity = "", string entityId = "", string details = "")
    {
        using var connection = _factory.CreateOpenConnection();
        Record(connection, null, session, action, entity, entityId, details);
    }

    public IReadOnlyList<AuditEntry> Query(AuditFilter filter)
    {
        using var connection = _factory.CreateOpenConnection();

        var sql = """
            SELECT id AS Id, ts AS Timestamp, user_id AS UserId,
                   username AS Username, action AS Action, entity AS Entity,
                   entity_id AS EntityId, details AS Details
            FROM audit_log
            WHERE (@fromIso IS NULL OR ts >= @fromIso)
              AND (@toIso IS NULL OR ts < @toIso)
              AND (@action IS NULL OR action = @action)
              AND (@username IS NULL OR username = @username)
            ORDER BY id DESC
            LIMIT @limit
            """;

        return connection.Query<AuditEntry>(sql, new
        {
            from = (long?)filter.From?.DayNumber,
            fromIso = filter.From is null ? null : new DateOnly?(filter.From.Value).ToString(),
            to = (long?)filter.To?.DayNumber,
            toIso = filter.To is null ? null : new DateOnly?(filter.To.Value.AddDays(1)).ToString(),
            action = string.IsNullOrWhiteSpace(filter.Action) ? null : filter.Action,
            username = string.IsNullOrWhiteSpace(filter.Username) ? null : filter.Username,
            limit = filter.Limit,
        }).AsList();
    }

    public IReadOnlyList<string> DistinctActions()
    {
        using var connection = _factory.CreateOpenConnection();
        return connection.Query<string>("SELECT DISTINCT action FROM audit_log ORDER BY action").AsList();
    }

    public int Count()
    {
        using var connection = _factory.CreateOpenConnection();
        return connection.ExecuteScalar<int>("SELECT COUNT(*) FROM audit_log");
    }

    public void Prune(DateTimeOffset olderThan)
    {
        using var connection = _factory.CreateOpenConnection();
        connection.Execute("DELETE FROM audit_log WHERE ts < @cutoff", new { cutoff = olderThan.ToString("o") });
    }
}

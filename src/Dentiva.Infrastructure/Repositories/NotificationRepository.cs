using Dentiva.Core.Domain;
using Dapper;

namespace Dentiva.Infrastructure.Repositories;

/// <summary>In-app notification center. Everything is local — no external service.</summary>
public sealed class NotificationRepository
{
    private readonly ISqliteConnectionFactory _factory;

    public NotificationRepository(ISqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    public long Insert(NotificationItem item)
    {
        item.CreatedAt = DateTimeOffset.UtcNow;
        using var connection = _factory.CreateOpenConnection();
        return connection.ExecuteScalar<long>("""
            INSERT INTO notifications(kind, priority, title, message, entity, entity_id, created_at)
            VALUES (@Kind, @Priority, @Title, @Message, @Entity, @EntityId, @CreatedAt)
            RETURNING id
            """,
            item);
    }

    public IReadOnlyList<NotificationItem> Active(int limit = 100)
    {
        using var connection = _factory.CreateOpenConnection();
        return connection.Query<NotificationItem>(
            "SELECT id AS Id, kind AS Kind, priority AS Priority, title AS Title, message AS Message, entity AS Entity, entity_id AS EntityId, created_at AS CreatedAt, read_at AS ReadAt, dismissed_at AS DismissedAt, snoozed_until AS SnoozedUntil FROM notifications WHERE dismissed_at IS NULL AND (snoozed_until IS NULL OR snoozed_until <= @now) ORDER BY CASE priority WHEN 2 THEN 0 WHEN 1 THEN 1 ELSE 2 END, created_at DESC LIMIT @limit",
            new { now = DateTimeOffset.UtcNow.ToString("o"), limit }).AsList();
    }

    public int UnreadCount()
    {
        using var connection = _factory.CreateOpenConnection();
        return connection.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM notifications WHERE dismissed_at IS NULL AND read_at IS NULL");
    }

    public void MarkRead(long id)
    {
        using var connection = _factory.CreateOpenConnection();
        connection.Execute("UPDATE notifications SET read_at = @now WHERE id = @id AND read_at IS NULL",
            new { now = DateTimeOffset.UtcNow.ToString("o"), id });
    }

    public void MarkAllRead()
    {
        using var connection = _factory.CreateOpenConnection();
        connection.Execute("UPDATE notifications SET read_at = @now WHERE read_at IS NULL AND dismissed_at IS NULL",
            new { now = DateTimeOffset.UtcNow.ToString("o") });
    }

    public void Dismiss(long id)
    {
        using var connection = _factory.CreateOpenConnection();
        connection.Execute("UPDATE notifications SET dismissed_at = @now WHERE id = @id",
            new { now = DateTimeOffset.UtcNow.ToString("o"), id });
    }

    public void Snooze(long id, DateTimeOffset until)
    {
        using var connection = _factory.CreateOpenConnection();
        connection.Execute("UPDATE notifications SET snoozed_until = @until WHERE id = @id",
            new { until = until.ToString("o"), id });
    }

    public bool AlreadyRaised(string entity, string entityId, NotificationKind kind)
    {
        using var connection = _factory.CreateOpenConnection();
        return connection.ExecuteScalar<long>(
            "SELECT COUNT(*) FROM notifications WHERE entity = @entity AND entity_id = @entityId AND kind = @kind AND dismissed_at IS NULL",
            new { entity, entityId, kind = (int)kind }) > 0;
    }
}

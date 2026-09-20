using Dentiva.Core.Domain;
using Dentiva.Core.Security;
using Dapper;

namespace Dentiva.Infrastructure.Repositories;

/// <summary>
/// Application user accounts. Passwords are stored only as PBKDF2 hashes.
/// The first account created becomes the clinic administrator.
/// </summary>
public sealed class UserRepository
{
    private readonly ISqliteConnectionFactory _factory;

    public UserRepository(ISqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    public bool AnyUserExists()
    {
        using var connection = _factory.CreateOpenConnection();
        return connection.ExecuteScalar<long>("SELECT COUNT(*) FROM users") > 0;
    }

    public UserAccount? Create(string username, string password, string displayName, string roleName)
    {
        Guard.NotNullOrWhiteSpace(username);
        Guard.NotNullOrWhiteSpace(password);
        Guard.NotNullOrWhiteSpace(displayName);

        using var connection = _factory.CreateOpenConnection();
        var id = connection.ExecuteScalar<long>("""
            INSERT INTO users(username, password_hash, display_name, role_name, is_active, created_at)
            VALUES (@username, @hash, @display, @role, 1, @now)
            RETURNING id
            """,
            new
            {
                username = username.Trim(),
                hash = PasswordHasher.Hash(password),
                display = displayName.Trim(),
                role = roleName,
                now = DateTimeOffset.UtcNow.ToString("o"),
            });

        return GetById(id);
    }

    public UserAccount? GetById(long id)
    {
        using var connection = _factory.CreateOpenConnection();
        return connection.QuerySingleOrDefault<UserAccount>(
            "SELECT id AS Id, username AS Username, password_hash AS PasswordHash, display_name AS DisplayName, role_name AS RoleName, is_active AS IsActive, created_at AS CreatedAt, last_login_at AS LastLoginAt FROM users WHERE id = @id",
            new { id });
    }

    public IReadOnlyList<UserAccount> List()
    {
        using var connection = _factory.CreateOpenConnection();
        return connection.Query<UserAccount>(
            "SELECT id AS Id, username AS Username, password_hash AS PasswordHash, display_name AS DisplayName, role_name AS RoleName, is_active AS IsActive, created_at AS CreatedAt, last_login_at AS LastLoginAt FROM users ORDER BY id").AsList();
    }

    public SessionContext? Authenticate(string username, string password)
    {
        Guard.NotNullOrWhiteSpace(username);

        using var connection = _factory.CreateOpenConnection();
        var user = connection.QuerySingleOrDefault<UserAccount>(
            "SELECT id AS Id, username AS Username, password_hash AS PasswordHash, display_name AS DisplayName, role_name AS RoleName, is_active AS IsActive, created_at AS CreatedAt, last_login_at AS LastLoginAt FROM users WHERE username = @username COLLATE NOCASE",
            new { username = username.Trim() });

        if (user is null || !user.IsActive)
        {
            return null;
        }

        if (!PasswordHasher.Verify(password, user.PasswordHash))
        {
            return null;
        }

        connection.Execute("UPDATE users SET last_login_at = @now WHERE id = @id",
            new { now = DateTimeOffset.UtcNow.ToString("o"), id = user.Id });

        return new SessionContext(user.Id, user.Username, user.DisplayName, user.RoleName,
            PermissionPresets.ForRole(user.RoleName));
    }

    public void ChangePassword(long userId, string newPassword)
    {
        Guard.NotNullOrWhiteSpace(newPassword);
        using var connection = _factory.CreateOpenConnection();
        connection.Execute("UPDATE users SET password_hash = @hash WHERE id = @id",
            new { hash = PasswordHasher.Hash(newPassword), id = userId });
    }

    public void Update(UserAccount user)
    {
        using var connection = _factory.CreateOpenConnection();
        connection.Execute("""
            UPDATE users SET display_name = @display, role_name = @role, is_active = @active WHERE id = @id
            """,
            new { display = user.DisplayName, role = user.RoleName, active = user.IsActive, id = user.Id });
    }

    public void SetActive(long userId, bool active)
    {
        using var connection = _factory.CreateOpenConnection();
        connection.Execute("UPDATE users SET is_active = @active WHERE id = @id", new { active, id = userId });
    }
}

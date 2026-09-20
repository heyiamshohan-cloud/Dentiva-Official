using System.Text.Json;
using Dentiva.Core.Settings;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Dentiva.Infrastructure.Repositories;

/// <summary>Persists the application settings tree as JSON under a single key.</summary>
public sealed class SettingsRepository
{
    public const string MainKey = "app";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
    };

    private readonly ISqliteConnectionFactory _factory;

    public SettingsRepository(ISqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    public AppSettings Load()
    {
        using var connection = _factory.CreateOpenConnection();
        var json = connection.ExecuteScalar<string>(
            "SELECT value FROM app_settings WHERE key = @key", new { key = MainKey });

        if (string.IsNullOrWhiteSpace(json))
        {
            return new AppSettings();
        }

        try
        {
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch (JsonException)
        {
            // Corrupt settings must never prevent the clinic from opening;
            // fall back to defaults (previous JSON is preserved below).
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        using var connection = _factory.CreateOpenConnection();
        connection.Execute("""
            INSERT INTO app_settings(key, value, reserved) VALUES (@key, @value, @schema)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value
            """,
            new { key = MainKey, value = json, schema = Schema });
    }

    public string? LoadRaw(string key)
    {
        using var connection = _factory.CreateOpenConnection();
        return connection.ExecuteScalar<string>(
            "SELECT value FROM app_settings WHERE key = @key", new { key });
    }

    public void SaveRaw(string key, string json)
    {
        using var connection = _factory.CreateOpenConnection();
        connection.Execute("""
            INSERT INTO app_settings(key, value) VALUES (@key, @value)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value
            """,
            new { key, value = json });
    }
}

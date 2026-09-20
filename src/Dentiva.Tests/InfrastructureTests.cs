using Dentiva.Infrastructure.Persistence;
using Dentiva.Infrastructure.Repositories;
using Xunit;

namespace Dentiva.Tests;

public class InfrastructureTests : IDisposable
{
    private readonly string _tempDir;
    private readonly SqliteConnectionFactory _factory;

    public InfrastructureTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "dentiva-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _factory = new SqliteConnectionFactory(Path.Combine(_tempDir, "test.db"));
        Database.Migrate(_factory);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tempDir, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void Migration_creates_a_healthy_database()
    {
        Assert.Equal("ok", Database.IntegrityCheck(_factory));
        using var connection = _factory.CreateOpenConnection();
        var version = connection.ExecuteScalar<long>("SELECT MAX(version) FROM schema_version");
        Assert.Equal(Database.CurrentVersion, version);
    }

    [Fact]
    public void Migration_is_idempotent()
    {
        Database.Migrate(_factory);
        Database.Migrate(_factory);
        Assert.Equal("ok", Database.IntegrityCheck(_factory));
    }

    [Fact]
    public void Counters_increment_atomically()
    {
        var counters = new CounterService(_factory);
        Assert.Equal(1, counters.NextStandalone("invoice", "2026"));
        Assert.Equal(2, counters.NextStandalone("invoice", "2026"));
        Assert.Equal(1, counters.NextStandalone("invoice", "2027")); // separate scope
    }

    [Fact]
    public void Settings_round_trip()
    {
        var repo = new SettingsRepository(_factory);
        var settings = repo.Load();
        settings.Clinic.ClinicName = "Test Dental Care";
        settings.Billing.InvoiceNumberTemplate = "INV-{YYYY}-{SEQ:5}";
        repo.Save(settings);

        var reloaded = repo.Load();
        Assert.Equal("Test Dental Care", reloaded.Clinic.ClinicName);
        Assert.Equal("INV-{YYYY}-{SEQ:5}", reloaded.Billing.InvoiceNumberTemplate);
    }

    [Fact]
    public void Fts_query_builds_prefix_queries()
    {
        Assert.Equal(string.Empty, FtsQuery.Build("   "));
        Assert.Equal("\"rahim\"*", FtsQuery.Build("Rahim"));
        Assert.Equal("\"rah\"* \"mi\"*", FtsQuery.Build("rah, mi!"));
    }

    [Fact]
    public void Sql_script_splitter_handles_triggers()
    {
        var script = """
            CREATE TABLE t1(id INTEGER);
            CREATE TRIGGER trg AFTER INSERT ON t1 BEGIN
              INSERT INTO t1 VALUES (1);
              UPDATE t1 SET id = 2;
            END;
            CREATE INDEX ix_t1 ON t1(id);
            """;

        var statements = Db.SplitScript(script);
        Assert.Equal(3, statements.Count);
        Assert.Contains(statements, s => s.StartsWith("CREATE TRIGGER", StringComparison.OrdinalIgnoreCase) && s.Contains("UPDATE t1"));
    }
}

using Dapper;

namespace Dentiva.Infrastructure.Repositories;

public sealed record GlobalSearchHit(string Kind, long RefId, long PatientId, string Title, string Snippet);

public sealed record GlobalSearchResult(IReadOnlyList<GlobalSearchHit> Hits, int TotalMatches, long ElapsedMilliseconds);

/// <summary>
/// Global search across the practice using SQLite FTS5. Fast prefix search
/// over patients, visits, invoices, payments, appointments, medications,
/// referrals, attachments and staff — even with hundreds of thousands of rows.
/// </summary>
public sealed class SearchRepository
{
    private readonly ISqliteConnectionFactory _factory;

    public SearchRepository(ISqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    public GlobalSearchResult Search(string query, int limit = 60, string? kind = null, long? patientId = null)
    {
        var ftsQuery = FtsQuery.Build(query);
        if (ftsQuery.Length == 0)
        {
            return new GlobalSearchResult(Array.Empty<GlobalSearchHit>(), 0, 0);
        }

        using var connection = _factory.CreateOpenConnection();

        var total = connection.ExecuteScalar<int>(
            """
            SELECT COUNT(*) FROM search_fts
            WHERE search_fts MATCH @q
              AND (@kind IS NULL OR kind = @kind)
              AND (@pid IS NULL OR patient_id = @pid)
            """,
            new { q = ftsQuery, kind, pid = patientId });

        var sw = System.Diagnostics.Stopwatch.StartNew();

        var hits = connection.Query<GlobalSearchHit>(
            """
            SELECT kind AS Kind, ref_id AS RefId, patient_id AS PatientId,
                   title AS Title,
                   substr(body, 1, 160) AS Snippet
            FROM search_fts
            WHERE search_fts MATCH @q
              AND (@kind IS NULL OR kind = @kind)
              AND (@pid IS NULL OR patient_id = @pid)
            ORDER BY rank
            LIMIT @limit
            """,
            new { q = ftsQuery, kind, pid = patientId, limit }).AsList();

        sw.Stop();
        return new GlobalSearchResult(hits, total, sw.ElapsedMilliseconds);
    }

    public IReadOnlyList<(string Kind, long Count)> Counts()
    {
        using var connection = _factory.CreateOpenConnection();
        return connection.Query<(string Kind, long Count)>(
            "SELECT kind AS Kind, COUNT(*) AS Count FROM search_fts GROUP BY kind").AsList();
    }

    /// <summary>Rebuilds the FTS index from source tables (maintenance tool).</summary>
    public void RebuildIndex()
    {
        using var connection = _factory.CreateOpenConnection();
        connection.Execute("INSERT INTO search_fts(search_fts) VALUES('rebuild')");
    }
}

public static class FtsQuery
{
    /// <summary>
    /// Converts user text into a safe FTS5 prefix query. Special characters
    /// are stripped; each word becomes a prefix match (e.g. "rah mi" →
    /// 'rah*' 'mi*'). Returns an empty string when there is nothing to search.
    /// </summary>
    public static string Build(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var words = input.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(w => new string(w.Where(char.IsLetterOrDigit).ToArray()))
            .Where(w => w.Length > 0)
            .Take(8)
            .Select(w => $"\"{w}\"*")
            .ToArray();

        return string.Join(' ', words);
    }
}

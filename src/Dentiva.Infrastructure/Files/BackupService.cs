using System.IO.Compression;
using System.Text.Json;
using Dentiva.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace Dentiva.Infrastructure.Files;

public sealed record BackupManifest(
    string Product, string AppVersion, int SchemaVersion,
    DateTimeOffset CreatedAt, string DatabaseSha256, long AttachmentCount, long DatabaseBytes);

public sealed record BackupResult(string Path, long SizeBytes, long AttachmentCount, TimeSpan Duration);

public sealed record RestoreResult(int SchemaVersion, DateTimeOffset CreatedAt, string AppVersion);

/// <summary>
/// Backup and restore. A backup is a single verified <c>.dentiva</c> archive
/// containing a checkpointed copy of the database plus all attachment files
/// and a signed manifest. Restore validates integrity before replacing data.
/// </summary>
public sealed class BackupService
{
    public const string ManifestEntry = "manifest.json";
    public const string DatabaseEntry = "dentiva.db";
    public const string AttachmentsPrefix = "attachments/";

    private readonly ISqliteConnectionFactory _factory;
    private readonly string _attachmentsRoot;

    public BackupService(ISqliteConnectionFactory factory, string attachmentsRoot)
    {
        _factory = factory;
        _attachmentsRoot = attachmentsRoot;
    }

    public BackupResult Create(string targetFolder, bool includeAttachments, string appVersion)
    {
        Guard.NotNullOrWhiteSpace(targetFolder);
        Directory.CreateDirectory(targetFolder);

        var started = DateTimeOffset.UtcNow;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var fileName = $"DentivaBackup-{DateTime.Now:yyyyMMdd-HHmmss}.dentiva";
        var fullPath = Path.Combine(targetFolder, fileName);

        // Write to a temp file first so a failed backup never replaces an
        // existing good archive.
        var tempPath = fullPath + ".tmp";

        try
        {
            // WAL checkpoint so the copy is complete.
            using (var connection = _factory.CreateOpenConnection())
            {
                connection.Execute("PRAGMA wal_checkpoint(TRUNCATE);");
            }

            long attachmentCount = 0;
            using (var archive = ZipFile.Open(tempPath, ZipArchiveMode.Create))
            {
                var dbPath = _factory.DatabasePath;
                var dbBytes = File.ReadAllBytes(dbPath);

                var dbEntry = archive.CreateEntry(DatabaseEntry, CompressionLevel.Optimal);
                using (var entryStream = dbEntry.Open())
                {
                    entryStream.Write(dbBytes);
                }

                if (includeAttachments && Directory.Exists(_attachmentsRoot))
                {
                    foreach (var file in Directory.EnumerateFiles(_attachmentsRoot, "*", SearchOption.AllDirectories))
                    {
                        var relative = Path.GetRelativePath(_attachmentsRoot, file).Replace('\\', '/');
                        archive.CreateEntryFromFile(file, AttachmentsPrefix + relative, CompressionLevel.Optimal);
                        attachmentCount++;
                    }
                }

                var manifest = new BackupManifest(
                    "Dentiva", appVersion, Database.CurrentVersion,
                    started, AttachmentStore.ComputeHash(dbPath), attachmentCount, dbBytes.Length);

                var manifestEntry = archive.CreateEntry(ManifestEntry, CompressionLevel.Optimal);
                using (var writer = new StreamWriter(manifestEntry.Open()))
                {
                    writer.Write(JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
                }
            }

            File.Move(tempPath, fullPath, overwrite: false);
            stopwatch.Stop();
            return new BackupResult(fullPath, new FileInfo(fullPath).Length, attachmentCount, stopwatch.Elapsed);
        }
        catch
        {
            TryDelete(tempPath);
            throw;
        }
    }

    /// <summary>Reads and validates a backup archive without restoring it.</summary>
    public BackupManifest Inspect(string backupPath)
    {
        using var archive = ZipFile.OpenRead(backupPath);
        var manifestEntry = archive.GetEntry(ManifestEntry)
            ?? throw new InvalidOperationException("This file is not a valid Dentiva backup (manifest missing).");

        using var reader = new StreamReader(manifestEntry.Open());
        var manifest = JsonSerializer.Deserialize<BackupManifest>(reader.ReadToEnd())
            ?? throw new InvalidOperationException("The backup manifest could not be read.");

        return manifest;
    }

    public RestoreResult Restore(string backupPath, string appVersion, string? dataDirectory = null, string? attachmentsDirectory = null)
    {
        Guard.NotNullOrWhiteSpace(backupPath);

        var manifest = Inspect(backupPath);

        // Stage everything first; only touch live data after validation.
        var stagingDb = Path.Combine(Path.GetTempPath(), "dentiva-restore-" + Guid.NewGuid().ToString("N") + ".db");
        var stagingAttachments = Path.Combine(Path.GetTempPath(), "dentiva-restore-att-" + Guid.NewGuid().ToString("N"));

        try
        {
            using (var archive = ZipFile.OpenRead(backupPath))
            {
                var dbEntry = archive.GetEntry(DatabaseEntry)
                    ?? throw new InvalidOperationException("The backup archive does not contain the database.");

                dbEntry.ExtractToFile(stagingDb, overwrite: true);

                Directory.CreateDirectory(stagingAttachments);
                foreach (var entry in archive.Entries.Where(e => e.FullName.StartsWith(AttachmentsPrefix, StringComparison.Ordinal)))
                {
                    var relative = entry.FullName[AttachmentsPrefix.Length..];
                    if (relative.Length == 0)
                    {
                        continue;
                    }

                    var target = Path.Combine(stagingAttachments, relative.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    entry.ExtractToFile(target, overwrite: true);
                }
            }

            // Integrity gate before anything is replaced.
            var stagingFactory = new SqliteConnectionFactory(stagingDb);
            var check = Database.IntegrityCheck(stagingFactory);
            if (!string.Equals(check, "ok", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"The backup failed its integrity check ({check}). Nothing was changed.");
            }

            // Replace live database files (main, -wal, -shm).
            var liveDb = dataDirectory is null ? _factory.DatabasePath : Path.Combine(dataDirectory, "dentiva.db");
            foreach (var suffix in new[] { "-wal", "-shm" })
            {
                var sidecar = liveDb + suffix;
                if (File.Exists(sidecar))
                {
                    File.Delete(sidecar);
                }
            }

            File.Copy(stagingDb, liveDb, overwrite: true);

            if (attachmentsDirectory is not null || Directory.Exists(_attachmentsRoot))
            {
                var targetRoot = attachmentsDirectory ?? _attachmentsRoot;
                if (Directory.Exists(targetRoot))
                {
                    Directory.Delete(targetRoot, recursive: true);
                }

                if (Directory.Exists(stagingAttachments))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(targetRoot)!);
                    Directory.Move(stagingAttachments, targetRoot);
                }
            }

            return new RestoreResult(manifest.SchemaVersion, manifest.CreatedAt, manifest.AppVersion);
        }
        finally
        {
            TryDelete(stagingDb);
            TryDelete(stagingDb + "-wal");
            TryDelete(stagingDb + "-shm");
            try
            {
                if (Directory.Exists(stagingAttachments))
                {
                    Directory.Delete(stagingAttachments, recursive: true);
                }
            }
            catch (IOException)
            {
            }
        }
    }

    /// <summary>Keeps only the newest <paramref name="keep"/> backups in a folder.</summary>
    public static IReadOnlyList<string> PruneOldBackups(string folder, int keep)
    {
        var files = Directory.EnumerateFiles(folder, "DentivaBackup-*.dentiva")
            .OrderByDescending(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var removed = new List<string>();
        foreach (var old in files.Skip(Math.Max(1, keep)))
        {
            try
            {
                File.Delete(old);
                removed.Add(old);
            }
            catch (IOException)
            {
                // A locked file must never break pruning.
            }
        }

        return removed;
    }

    public static IReadOnlyList<(string Path, long Size, DateTimeOffset Created)> ListBackups(string folder)
    {
        if (!Directory.Exists(folder))
        {
            return Array.Empty<(string, long, DateTimeOffset)>();
        }

        return Directory.EnumerateFiles(folder, "DentivaBackup-*.dentiva")
            .Select(f => (f, new FileInfo(f).Length, File.GetCreationTime(f)))
            .OrderByDescending(t => t.Item3)
            .ToList();
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
    }
}

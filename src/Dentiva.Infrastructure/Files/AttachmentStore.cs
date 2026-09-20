using System.Security.Cryptography;
using Dentiva.Core.Domain;
using Dapper;

namespace Dentiva.Infrastructure.Files;

/// <summary>
/// Safe managed storage for clinical attachments. Files live outside the
/// database under a structured folder tree; the database keeps metadata.
/// There is no limit on the number of attachments per patient — capacity is
/// bounded only by available disk space.
/// </summary>
public sealed class AttachmentStore
{
    private readonly ISqliteConnectionFactory _factory;
    private readonly string _root;

    public AttachmentStore(ISqliteConnectionFactory factory, string attachmentsRoot)
    {
        _factory = factory;
        _root = attachmentsRoot;
        Directory.CreateDirectory(_root);
    }

    public string Root => _root;

    public sealed record StoredFile(AttachmentInfo Info, string FullPath);

    public StoredFile Save(
        Stream content,
        string originalFileName,
        AttachmentCategory category,
        long? patientId,
        long? visitId = null,
        long? referralId = null,
        long? staffId = null,
        long? invoiceId = null,
        string description = "",
        string addedBy = "",
        int? pixelWidth = null,
        int? pixelHeight = null)
    {
        Guard.NotNullOrWhiteSpace(originalFileName);
        Guard.Against(content.Length < 0, "Invalid content stream.");

        var ext = Path.GetExtension(originalFileName);
        var storedName = $"{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..12]}";
        var relativeFolder = Path.Combine(DateTimeOffset.UtcNow.Year.ToString("0000"), DateTimeOffset.UtcNow.Month.ToString("00"));
        var folder = Path.Combine(_root, relativeFolder);
        Directory.CreateDirectory(folder);
        var fullPath = Path.Combine(folder, storedName + ext);

        using (var file = File.Create(fullPath))
        {
            content.CopyTo(file);
        }

        var info = new AttachmentInfo
        {
            PatientId = patientId,
            VisitId = visitId,
            ReferralId = referralId,
            StaffId = staffId,
            InvoiceId = invoiceId,
            Category = category,
            FileName = SanitizeFileName(originalFileName),
            StoredName = storedName + ext,
            ContentType = GuessContentType(ext),
            Extension = ext.TrimStart('.').ToLowerInvariant(),
            SizeBytes = new FileInfo(fullPath).Length,
            PixelWidth = pixelWidth,
            PixelHeight = pixelHeight,
            Description = description,
            AddedBy = addedBy,
            Sha256 = ComputeHash(fullPath),
        };

        info.Id = Insert(info);
        return new StoredFile(info, fullPath);
    }

    public string PathOf(AttachmentInfo info) => Path.Combine(_root, info.StoredName[..4], info.StoredName[5..7], info.StoredName);

    public bool FileExists(AttachmentInfo info) => File.Exists(PathOf(info));

    public Stream OpenRead(AttachmentInfo info) => File.OpenRead(PathOf(info));

    public AttachmentRepository.RepositoryAttachment? GetMetadata(long id)
    {
        using var connection = _factory.CreateOpenConnection();
        return Repository.Get(connection, id);
    }

    private long Insert(AttachmentInfo info)
    {
        using var connection = _factory.CreateOpenConnection();
        return connection.ExecuteScalar<long>("""
            INSERT INTO attachments(patient_id, visit_id, referral_id, staff_id, invoice_id, category, file_name,
                stored_name, content_type, ext, size_bytes, pixel_w, pixel_h, description, added_by, created_at, sha256)
            VALUES (@PatientId, @VisitId, @ReferralId, @StaffId, @InvoiceId, @Category, @FileName,
                @StoredName, @ContentType, @Extension, @SizeBytes, @PixelWidth, @PixelHeight, @Description, @AddedBy, @CreatedAt, @Sha256)
            RETURNING id
            """,
            new
            {
                info.PatientId,
                info.VisitId,
                info.ReferralId,
                info.StaffId,
                info.InvoiceId,
                Category = (int)info.Category,
                info.FileName,
                info.StoredName,
                info.ContentType,
                info.Extension,
                info.SizeBytes,
                info.PixelWidth,
                info.PixelHeight,
                info.Description,
                info.AddedBy,
                info.CreatedAt,
                info.Sha256,
            });
    }

    public void SetMetadata(long id, string fileName, string description, AttachmentCategory category)
    {
        using var connection = _factory.CreateOpenConnection();
        connection.Execute(
            "UPDATE attachments SET file_name = @name, description = @desc, category = @cat WHERE id = @id",
            new { name = fileName, desc = description, cat = (int)category, id });
    }

    public void SoftDelete(long id)
    {
        using var connection = _factory.CreateOpenConnection();
        connection.Execute("UPDATE attachments SET deleted_at = @now WHERE id = @id",
            new { now = DateTimeOffset.UtcNow.ToString("o"), id });
    }

    public void HardDeletePurged(DateTimeOffset olderThan)
    {
        using var connection = _factory.CreateOpenConnection();
        var doomed = connection.Query<(long Id, string StoredName)>(
            "SELECT id, stored_name FROM attachments WHERE deleted_at IS NOT NULL AND deleted_at < @cutoff",
            new { cutoff = olderThan.ToString("o") }).AsList();

        foreach (var (id, storedName) in doomed)
        {
            var path = Path.Combine(_root, storedName[..4], storedName[5..7], storedName);
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            connection.Execute("DELETE FROM attachments WHERE id = @id", new { id });
        }
    }

    public static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var clean = new string(name.Where(c => !invalid.Contains(c) && c != '"').ToArray()).Trim();
        return clean.Length == 0 ? "file" : clean;
    }

    public static string GuessContentType(string extension) => extension.ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".webp" => "image/webp",
        ".tif" or ".tiff" => "image/tiff",
        ".gif" => "image/gif",
        ".bmp" => "image/bmp",
        ".doc" => "application/msword",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".xls" => "application/vnd.ms-excel",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ".txt" => "text/plain",
        _ => "application/octet-stream",
    };

    public static string ComputeHash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}

/// <summary>Query layer over attachment metadata.</summary>
public sealed class AttachmentRepository
{
    private readonly ISqliteConnectionFactory _factory;

    public AttachmentRepository(ISqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    public const string Columns = """
        id AS Id, patient_id AS PatientId, visit_id AS VisitId, referral_id AS ReferralId,
        staff_id AS StaffId, invoice_id AS InvoiceId, category AS Category, file_name AS FileName,
        stored_name AS StoredName, content_type AS ContentType, ext AS Extension, size_bytes AS SizeBytes,
        pixel_w AS PixelWidth, pixel_h AS PixelHeight, description AS Description, added_by AS AddedBy,
        created_at AS CreatedAt, deleted_at AS DeletedAt, sha256 AS Sha256
        """;

    public static AttachmentInfo? Get(Microsoft.Data.Sqlite.SqliteConnection connection, long id) =>
        connection.QuerySingleOrDefault<AttachmentInfo>(
            $"SELECT {Columns} FROM attachments WHERE id = @id", new { id });

    public IReadOnlyList<AttachmentInfo> Query(long? patientId = null, long? visitId = null, long? staffId = null,
        long? invoiceId = null, long? referralId = null, bool includeDeleted = false, string? search = null, int limit = 500)
    {
        using var connection = _factory.CreateOpenConnection();
        var conditions = new List<string>();
        if (!includeDeleted)
        {
            conditions.Add("deleted_at IS NULL");
        }

        if (patientId is { } pid) { conditions.Add("patient_id = @pid"); }
        if (visitId is { } vid) { conditions.Add("visit_id = @vid"); }
        if (staffId is { } sid) { conditions.Add("staff_id = @sid"); }
        if (invoiceId is { } iid) { conditions.Add("invoice_id = @iid"); }
        if (referralId is { } rid) { conditions.Add("referral_id = @rid"); }
        if (!string.IsNullOrWhiteSpace(search))
        {
            conditions.Add("(file_name LIKE @s OR description LIKE @s)");
        }

        var where = conditions.Count == 0 ? string.Empty : "WHERE " + string.Join(" AND ", conditions);
        return connection.Query<RepositoryAttachment>(
            $"SELECT {Columns} FROM attachments {where} ORDER BY created_at DESC LIMIT @limit",
            new { pid, vid, sid, iid, rid, s = (search ?? "").Trim() + "%", limit }).AsList();
    }

    public long TotalBytes(bool includeDeleted = false)
    {
        using var connection = _factory.CreateOpenConnection();
        return connection.ExecuteScalar<long>(
            includeDeleted
                ? "SELECT COALESCE(SUM(size_bytes), 0) FROM attachments"
                : "SELECT COALESCE(SUM(size_bytes), 0) FROM attachments WHERE deleted_at IS NULL");
    }

    public long Count(bool includeDeleted = false)
    {
        using var connection = _factory.CreateOpenConnection();
        return connection.ExecuteScalar<long>(
            includeDeleted
                ? "SELECT COUNT(*) FROM attachments"
                : "SELECT COUNT(*) FROM attachments WHERE deleted_at IS NULL");
    }

    /// <summary>Metadata rows whose physical file is missing or renamed.</summary>
    public IReadOnlyList<AttachmentInfo> FindOrphans()
    {
        using var connection = _factory.CreateOpenConnection();
        return connection.Query<RepositoryAttachment>(
            $"SELECT {Columns} FROM attachments WHERE deleted_at IS NULL ORDER BY id").AsList();
    }
}

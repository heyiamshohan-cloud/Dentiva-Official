using Dentiva.Core.Domain;
using Dentiva.Core.Numbering;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Dentiva.Infrastructure.Repositories;

public sealed record PatientQuery
{
    public string SearchText { get; init; } = string.Empty;
    public PatientStatus? Status { get; init; }
    public Gender? Gender { get; init; }
    public long? TagId { get; init; }
    public bool OnlyWithOutstanding { get; init; }
    public string SortBy { get; init; } = "name";
    public bool SortDescending { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
    public DateOnly Today { get; init; }
}

public sealed record PagedPatients(IReadOnlyList<Patient> Items, int TotalCount, int Page, int PageSize)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
}

public sealed class PatientRepository
{
    private readonly ISqliteConnectionFactory _factory;
    private readonly CounterService _counters;

    public PatientRepository(ISqliteConnectionFactory factory, CounterService counters)
    {
        _factory = factory;
        _counters = counters;
    }

    private const string BaseColumns = """
        p.id AS Id, p.code AS Code, p.full_name AS FullName, p.preferred_name AS PreferredName,
        p.dob AS DateOfBirth, p.gender AS Gender, p.phone AS Phone, p.alt_phone AS AlternatePhone,
        p.email AS Email, p.address AS Address, p.city AS City, p.occupation AS Occupation,
        p.emergency_name AS EmergencyContactName, p.emergency_relation AS EmergencyContactRelation,
        p.emergency_phone AS EmergencyContactPhone, p.chief_complaint AS ChiefComplaint,
        p.medical_history AS MedicalHistory, p.dental_history AS DentalHistory,
        p.allergies AS Allergies, p.current_medications AS CurrentMedications,
        p.risk_factors AS RiskFactors, p.notes AS Notes, p.status AS Status,
        p.registration_date AS RegistrationDate, p.created_at AS CreatedAt, p.updated_at AS UpdatedAt,
        p.deleted_at AS DeletedAt
        """;

    private const string ListColumns = BaseColumns + """
        ,
        (SELECT MAX(v.visit_date) FROM visits v WHERE v.patient_id = p.id AND v.deleted_at IS NULL) AS LastVisitDate,
        (SELECT MIN(a.date) FROM appointments a
          WHERE a.patient_id = p.id AND a.date >= @today AND a.status IN (0, 1)) AS NextAppointmentDate,
        (SELECT COALESCE(SUM(i.due_minor), 0) FROM invoices i
          WHERE i.patient_id = p.id AND i.voided_at IS NULL) AS OutstandingMinor
        """;

    public PagedPatients Search(PatientQuery query)
    {
        using var connection = _factory.CreateOpenConnection();

        var where = new List<string> { "p.deleted_at IS NULL" };
        var p = new DynamicParameters();
        p.Add("today", query.Today.ToString("yyyy-MM-dd"));

        if (!string.IsNullOrWhiteSpace(query.SearchText))
        {
            var term = query.SearchText.Trim();
            p.Add("term", term);
            p.Add("termPrefix", term + "%");
            where.Add("(p.full_name LIKE @termPrefix OR p.code LIKE @termPrefix OR p.phone LIKE @termPrefix OR p.alt_phone LIKE @termPrefix OR p.email LIKE @termPrefix)");
        }

        if (query.Status is { } status)
        {
            p.Add("status", (int)status);
            where.Add("p.status = @status");
        }

        if (query.Gender is { } gender)
        {
            p.Add("gender", (int)gender);
            where.Add("p.gender = @gender");
        }

        if (query.TagId is { } tagId)
        {
            p.Add("tagId", tagId);
            where.Add("EXISTS (SELECT 1 FROM patient_tag_map m WHERE m.patient_id = p.id AND m.tag_id = @tagId)");
        }

        if (query.OnlyWithOutstanding)
        {
            where.Add("(SELECT COALESCE(SUM(i.due_minor), 0) FROM invoices i WHERE i.patient_id = p.id AND i.voided_at IS NULL) > 0");
        }

        var whereSql = string.Join(" AND ", where);
        var orderBy = query.SortBy switch
        {
            "code" => "p.code",
            "registration" => "p.registration_date",
            "lastVisit" => "lastVisitSort",
            "outstanding" => "outstandingSort",
            _ => "p.full_name COLLATE NOCASE",
        };
        orderBy += query.SortDescending ? " DESC" : " ASC";

        // Subquery results need stable aliases for sorting.
        var sortSql = query.SortBy switch
        {
            "lastVisit" => orderBy.Replace("lastVisitSort", "(SELECT MAX(v.visit_date) FROM visits v WHERE v.patient_id = p.id AND v.deleted_at IS NULL)"),
            "outstanding" => orderBy.Replace("outstandingSort", "(SELECT COALESCE(SUM(i.due_minor), 0) FROM invoices i WHERE i.patient_id = p.id AND i.voided_at IS NULL)"),
            _ => orderBy,
        };

        var total = connection.ExecuteScalar<int>(
            $"SELECT COUNT(*) FROM patients p WHERE {whereSql}", p);

        var pageSize = Math.Clamp(query.PageSize, 10, 500);
        var page = Math.Max(1, query.Page);
        p.Add("limit", pageSize);
        p.Add("offset", (page - 1) * pageSize);

        var items = connection.Query<Patient>(
            $"SELECT {ListColumns} FROM patients p WHERE {whereSql} ORDER BY {sortSql} LIMIT @limit OFFSET @offset",
            p).AsList();

        return new PagedPatients(items, total, page, pageSize);
    }

    public Patient? GetById(long id)
    {
        using var connection = _factory.CreateOpenConnection();
        return connection.QuerySingleOrDefault<Patient>(
            $"SELECT {ListColumns} FROM patients p WHERE p.id = @id",
            new { id, today = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd") });
    }

    public Patient? GetByCode(string code)
    {
        using var connection = _factory.CreateOpenConnection();
        return connection.QuerySingleOrDefault<Patient>(
            $"SELECT {ListColumns} FROM patients p WHERE p.code = @code COLLATE NOCASE AND p.deleted_at IS NULL",
            new { code, today = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd") });
    }

    public bool CodeExists(string code, long? excludeId = null)
    {
        using var connection = _factory.CreateOpenConnection();
        return connection.ExecuteScalar<long>(
            "SELECT COUNT(*) FROM patients WHERE code = @code COLLATE NOCASE AND (@exclude IS NULL OR id != @exclude)",
            new { code, exclude = excludeId }) > 0;
    }

    public string GenerateCode(AppSettings settings, DateOnly today, SqliteConnection? external = null, SqliteTransaction? tx = null)
    {
        var template = settings.Billing.PatientCodeTemplate;
        var scope = CounterService.ScopeForTemplate(template, today);
        var seq = external is null
            ? _counters.NextStandalone("patient", scope)
            : _counters.Next(external, tx, "patient", scope);
        return NumberFormatter.Format(template, seq, today);
    }

    public Patient Insert(Patient patient, string code, SessionContext session)
    {
        patient.Code = code;
        var now = DateTimeOffset.UtcNow;
        patient.CreatedAt = now;
        patient.UpdatedAt = now;

        using var connection = _factory.CreateOpenConnection();
        Db.InTransaction(connection, c =>
        {
            patient.Id = c.ExecuteScalar<long>("""
                INSERT INTO patients(code, full_name, preferred_name, dob, gender, phone, alt_phone, email,
                    address, city, occupation, emergency_name, emergency_relation, emergency_phone,
                    chief_complaint, medical_history, dental_history, allergies, current_medications,
                    risk_factors, notes, status, registration_date, created_at, updated_at)
                VALUES (@Code, @FullName, @PreferredName, @DateOfBirth, @Gender, @Phone, @AlternatePhone, @Email,
                    @Address, @City, @Occupation, @EmergencyContactName, @EmergencyContactRelation, @EmergencyContactPhone,
                    @ChiefComplaint, @MedicalHistory, @DentalHistory, @Allergies, @CurrentMedications,
                    @RiskFactors, @Notes, @Status, @RegistrationDate, @CreatedAt, @UpdatedAt)
                RETURNING id
                """,
                patient);
            return true;
        });

        return patient;
    }

    public void Update(Patient patient, SessionContext session)
    {
        patient.UpdatedAt = DateTimeOffset.UtcNow;
        using var connection = _factory.CreateOpenConnection();
        Db.InTransaction(connection, c =>
        {
            c.Execute("""
                UPDATE patients SET
                    full_name = @FullName, preferred_name = @PreferredName, dob = @DateOfBirth,
                    gender = @Gender, phone = @Phone, alt_phone = @AlternatePhone, email = @Email,
                    address = @Address, city = @City, occupation = @Occupation,
                    emergency_name = @EmergencyContactName, emergency_relation = @EmergencyContactRelation,
                    emergency_phone = @EmergencyContactPhone, chief_complaint = @ChiefComplaint,
                    medical_history = @MedicalHistory, dental_history = @DentalHistory,
                    allergies = @Allergies, current_medications = @CurrentMedications,
                    risk_factors = @RiskFactors, notes = @Notes, status = @Status, updated_at = @UpdatedAt
                WHERE id = @Id
                """,
                patient);
            return true;
        });
    }

    public void SoftDelete(long id, SessionContext session)
    {
        using var connection = _factory.CreateOpenConnection();
        connection.Execute("UPDATE patients SET deleted_at = @now, updated_at = @now WHERE id = @id",
            new { now = DateTimeOffset.UtcNow.ToString("o"), id });
    }

    public void SetTags(long patientId, IEnumerable<long> tagIds)
    {
        using var connection = _factory.CreateOpenConnection();
        Db.InTransaction(connection, c =>
        {
            c.Execute("DELETE FROM patient_tag_map WHERE patient_id = @id", new { id = patientId });
            foreach (var tagId in tagIds.Distinct())
            {
                c.Execute("INSERT OR IGNORE INTO patient_tag_map(patient_id, tag_id) VALUES (@id, @tagId)",
                    new { id = patientId, tagId });
            }

            return true;
        });
    }

    public IReadOnlyList<long> GetTagIds(long patientId)
    {
        using var connection = _factory.CreateOpenConnection();
        return connection.Query<long>("SELECT tag_id FROM patient_tag_map WHERE patient_id = @id",
            new { id = patientId }).AsList();
    }

    public IReadOnlyList<PatientTag> GetAllTags()
    {
        using var connection = _factory.CreateOpenConnection();
        return connection.Query<PatientTag>(
            "SELECT id AS Id, name AS Name, color AS ColorHex, is_system AS IsSystem FROM patient_tags ORDER BY name").AsList();
    }

    public PatientTag AddTag(string name, string colorHex)
    {
        using var connection = _factory.CreateOpenConnection();
        var id = connection.ExecuteScalar<long>(
            "INSERT INTO patient_tags(name, color, is_system) VALUES (@name, @color, 0) RETURNING id",
            new { name = name.Trim(), color = colorHex });
        return new PatientTag { Id = id, Name = name.Trim(), ColorHex = colorHex, IsSystem = false };
    }

    public void DeleteTag(long tagId)
    {
        using var connection = _factory.CreateOpenConnection();
        Db.InTransaction(connection, c =>
        {
            c.Execute("DELETE FROM patient_tag_map WHERE tag_id = @id", new { id = tagId });
            c.Execute("DELETE FROM patient_tags WHERE id = @id AND is_system = 0", new { id = tagId });
            return true;
        });
    }

    public long CountActive()
    {
        using var connection = _factory.CreateOpenConnection();
        return connection.ExecuteScalar<long>("SELECT COUNT(*) FROM patients WHERE deleted_at IS NULL AND status = 0");
    }

    public long CountNewInRange(DateOnly from, DateOnly to)
    {
        using var connection = _factory.CreateOpenConnection();
        return connection.ExecuteScalar<long>(
            "SELECT COUNT(*) FROM patients WHERE deleted_at IS NULL AND registration_date >= @from AND registration_date <= @to",
            new { from = from.ToString("yyyy-MM-dd"), to = to.ToString("yyyy-MM-dd") });
    }

    public IReadOnlyList<Patient> RecentPatients(int limit, DateOnly today)
    {
        using var connection = _factory.CreateOpenConnection();
        return connection.Query<Patient>(
            $"SELECT {ListColumns} FROM patients p WHERE p.deleted_at IS NULL ORDER BY p.created_at DESC LIMIT @limit",
            new { limit, today = today.ToString("yyyy-MM-dd") }).AsList();
    }

    public long TotalOutstanding()
    {
        using var connection = _factory.CreateOpenConnection();
        return connection.ExecuteScalar<long>(
            "SELECT COALESCE(SUM(due_minor), 0) FROM invoices WHERE voided_at IS NULL");
    }
}

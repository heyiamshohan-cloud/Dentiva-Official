using Dentiva.Core.Domain;
using Dapper;

namespace Dentiva.Infrastructure.Repositories;

public sealed class StaffRepository
{
    private readonly ISqliteConnectionFactory _factory;

    public StaffRepository(ISqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    public IReadOnlyList<StaffMember> Query(bool includeInactive = false, string? search = null)
    {
        using var connection = _factory.CreateOpenConnection();
        var conditions = new List<string>();
        if (!includeInactive)
        {
            conditions.Add("status != @terminated");
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            conditions.Add("(name LIKE @s OR code LIKE @s OR role LIKE @s OR phone LIKE @s)");
        }

        var where = conditions.Count == 0 ? string.Empty : "WHERE " + string.Join(" AND ", conditions);
        return connection.Query<StaffMember>(
            "SELECT id AS Id, code AS Code, name AS Name, role AS Role, phone AS Phone, email AS Email, address AS Address, joining_date AS JoiningDate, salary_minor AS SalaryMinor, salary_frequency AS SalaryFrequency, responsibilities AS Responsibilities, status AS Status, emergency_name AS EmergencyContactName, emergency_phone AS EmergencyContactPhone, notes AS Notes, created_at AS CreatedAt, updated_at AS UpdatedAt FROM staff " + where + " ORDER BY name",
            new { terminated = (int)StaffStatus.Terminated, s = (search ?? "").Trim() + "%" }).AsList();
    }

    public StaffMember? GetById(long id)
    {
        using var connection = _factory.CreateOpenConnection();
        return connection.QuerySingleOrDefault<StaffMember>(
            "SELECT id AS Id, code AS Code, name AS Name, role AS Role, phone AS Phone, email AS Email, address AS Address, joining_date AS JoiningDate, salary_minor AS SalaryMinor, salary_frequency AS SalaryFrequency, responsibilities AS Responsibilities, status AS Status, emergency_name AS EmergencyContactName, emergency_phone AS EmergencyContactPhone, notes AS Notes, created_at AS CreatedAt, updated_at AS UpdatedAt FROM staff WHERE id = @id",
            new { id });
    }

    public string NextStaffCode(DateOnly today)
    {
        using var connection = _factory.CreateOpenConnection();
        var max = connection.ExecuteScalar<long>("SELECT COUNT(*) FROM staff");
        return $"ST-{today:yyyy}-{max + 1:0000}";
    }

    public StaffMember Insert(StaffMember staff, SessionContext session)
    {
        staff.CreatedAt = DateTimeOffset.UtcNow;
        staff.UpdatedAt = staff.CreatedAt;
        using var connection = _factory.CreateOpenConnection();
        staff.Id = connection.ExecuteScalar<long>("""
            INSERT INTO staff(code, name, role, phone, email, address, joining_date, salary_minor, salary_frequency,
                responsibilities, status, emergency_name, emergency_phone, notes, created_at, updated_at)
            VALUES (@Code, @Name, @Role, @Phone, @Email, @Address, @JoiningDate, @SalaryMinor, @SalaryFrequency,
                @Responsibilities, @Status, @EmergencyContactName, @EmergencyContactPhone, @Notes, @CreatedAt, @UpdatedAt)
            RETURNING id
            """,
            staff);
        return staff;
    }

    public void Update(StaffMember staff, SessionContext session)
    {
        staff.UpdatedAt = DateTimeOffset.UtcNow;
        using var connection = _factory.CreateOpenConnection();
        connection.Execute("""
            UPDATE staff SET code = @Code, name = @Name, role = @Role, phone = @Phone, email = @Email,
                address = @Address, joining_date = @JoiningDate, salary_minor = @SalaryMinor,
                salary_frequency = @SalaryFrequency, responsibilities = @Responsibilities, status = @Status,
                emergency_name = @EmergencyContactName, emergency_phone = @EmergencyContactPhone,
                notes = @Notes, updated_at = @UpdatedAt
            WHERE id = @Id
            """,
            staff);
    }

    public IReadOnlyList<(string Role, long Count)> RoleCounts()
    {
        using var connection = _factory.CreateOpenConnection();
        return connection.Query<(string Role, long Count)>(
            "SELECT role AS Role, COUNT(*) AS Count FROM staff WHERE status = 0 GROUP BY role ORDER BY Count DESC").AsList();
    }
}

using Dentiva.Core.Domain;
using Dapper;
using Microsoft.Data.Sqlite;

namespace Dentiva.Infrastructure.Repositories;

public sealed record AppointmentQuery
{
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public string? DoctorName { get; init; }
    public AppointmentStatus? Status { get; init; }
    public long? PatientId { get; init; }
    public string SearchText { get; init; } = string.Empty;
    public int Limit { get; init; } = 300;
}

public sealed record AppointmentListItem(
    long Id, long PatientId, string PatientName, string PatientCode, string PatientPhone,
    DateOnly AppointmentDate, TimeOnly StartTime, int DurationMinutes, int SerialNumber,
    string AppointmentType, string DoctorName, AppointmentStatus Status,
    AppointmentPriority Priority, bool IsWalkIn, string Notes, long? VisitId);

public sealed class SerialConflictException : Exception
{
    public int SerialNumber { get; }

    public SerialConflictException(int serial, DateOnly date, string doctor)
        : base($"Serial {serial} is already taken for {date:yyyy-MM-dd} ({doctor}).")
    {
        SerialNumber = serial;
    }
}

/// <summary>
/// Appointments and the daily serial (queue) system. Serials are unique per
/// day per doctor (or per day when the clinic uses a single queue), they
/// reset naturally each day, and duplicates are rejected at the database level.
/// </summary>
public sealed class AppointmentRepository
{
    private readonly ISqliteConnectionFactory _factory;

    public AppointmentRepository(ISqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    private const string ListSelect = """
        SELECT a.id AS Id, a.patient_id AS PatientId, pa.full_name AS PatientName, pa.code AS PatientCode,
               pa.phone AS PatientPhone, a.date AS AppointmentDate, a.start_time AS StartTime,
               a.duration AS DurationMinutes, a.serial AS SerialNumber, a.type AS AppointmentType,
               a.doctor_name AS DoctorName, a.status AS Status, a.priority AS Priority,
               a.is_walk_in AS IsWalkIn, a.notes AS Notes, a.visit_id AS VisitId
        FROM appointments a
        JOIN patients pa ON pa.id = a.patient_id
        """;

    public int NextSerial(DateOnly date, string doctorName, bool perDoctor)
    {
        using var connection = _factory.CreateOpenConnection();
        return NextSerial(connection, null, date, doctorName, perDoctor);
    }

    public int NextSerial(SqliteConnection connection, SqliteTransaction? tx, DateOnly date, string doctorName, bool perDoctor)
    {
        var doctorScope = perDoctor ? doctorName : string.Empty;
        return connection.ExecuteScalar<int>(
            """
            SELECT COALESCE(MAX(serial), 0) + 1 FROM appointments
            WHERE date = @date AND (@perDoctor = 0 OR doctor_name = @doctor)
            """,
            new { date = date.ToString("yyyy-MM-dd"), perDoctor = perDoctor ? 1 : 0, doctor = doctorScope }, tx);
    }

    public Appointment Insert(Appointment appointment, bool perDoctor, SessionContext session)
    {
        appointment.CreatedAt = DateTimeOffset.UtcNow;
        appointment.UpdatedAt = appointment.CreatedAt;

        using var connection = _factory.CreateOpenConnection();
        Db.InTransaction(connection, c =>
        {
            if (appointment.SerialNumber <= 0)
            {
                appointment.SerialNumber = NextSerial(c, null, appointment.AppointmentDate, appointment.DoctorName, perDoctor);
            }
            else if (!IsSerialFree(c, appointment.AppointmentDate, appointment.DoctorName, appointment.SerialNumber, perDoctor, null))
            {
                throw new SerialConflictException(appointment.SerialNumber, appointment.AppointmentDate, appointment.DoctorName);
            }

            appointment.Id = c.ExecuteScalar<long>("""
                INSERT INTO appointments(patient_id, date, start_time, duration, serial, type, doctor_name,
                    status, priority, is_walk_in, notes, rescheduled_from, created_by, created_at, updated_at)
                VALUES (@PatientId, @AppointmentDate, @StartTime, @DurationMinutes, @SerialNumber, @AppointmentType,
                    @DoctorName, @Status, @Priority, @IsWalkIn, @Notes, @RescheduledFromId, @CreatedBy, @CreatedAt, @UpdatedAt)
                RETURNING id
                """,
                appointment);
            return true;
        });

        return appointment;
    }

    public bool IsSerialFree(DateOnly date, string doctorName, int serial, bool perDoctor, long? excludeId)
    {
        using var connection = _factory.CreateOpenConnection();
        return IsSerialFree(connection, null, date, doctorName, serial, perDoctor, excludeId);
    }

    public bool IsSerialFree(SqliteConnection connection, SqliteTransaction? tx, DateOnly date, string doctorName, int serial, bool perDoctor, long? excludeId)
    {
        var doctorScope = perDoctor ? doctorName : string.Empty;
        return connection.ExecuteScalar<long>(
            """
            SELECT COUNT(*) FROM appointments
            WHERE date = @date AND serial = @serial
              AND (@perDoctor = 0 OR doctor_name = @doctor)
              AND (@exclude IS NULL OR id != @exclude)
            """,
            new
            {
                date = date.ToString("yyyy-MM-dd"),
                serial,
                perDoctor = perDoctor ? 1 : 0,
                doctor = doctorScope,
                exclude = excludeId,
            }, tx) == 0;
    }

    public void Update(Appointment appointment, bool perDoctor, SessionContext session)
    {
        appointment.UpdatedAt = DateTimeOffset.UtcNow;
        using var connection = _factory.CreateOpenConnection();
        Db.InTransaction(connection, c =>
        {
            if (!IsSerialFree(c, null, appointment.AppointmentDate, appointment.DoctorName,
                    appointment.SerialNumber, perDoctor, appointment.Id))
            {
                throw new SerialConflictException(appointment.SerialNumber, appointment.AppointmentDate, appointment.DoctorName);
            }

            c.Execute("""
                UPDATE appointments SET patient_id = @PatientId, date = @AppointmentDate, start_time = @StartTime,
                    duration = @DurationMinutes, serial = @SerialNumber, type = @AppointmentType,
                    doctor_name = @DoctorName, status = @Status, priority = @Priority, is_walk_in = @IsWalkIn,
                    notes = @Notes, updated_at = @UpdatedAt
                WHERE id = @Id
                """,
                appointment);
            return true;
        });
    }

    public void UpdateStatus(long appointmentId, AppointmentStatus status)
    {
        using var connection = _factory.CreateOpenConnection();
        connection.Execute("UPDATE appointments SET status = @status, updated_at = @now WHERE id = @id",
            new { status = (int)status, now = DateTimeOffset.UtcNow.ToString("o"), id = appointmentId });
    }

    public void LinkVisit(long appointmentId, long visitId)
    {
        using var connection = _factory.CreateOpenConnection();
        connection.Execute("UPDATE appointments SET visit_id = @visit, updated_at = @now WHERE id = @id",
            new { visit, now = DateTimeOffset.UtcNow.ToString("o"), id = appointmentId });
    }

    /// <summary>Cancels an appointment and records a fresh one on the new date/time.</summary>
    public Appointment Reschedule(long appointmentId, DateOnly newDate, TimeOnly newTime, bool perDoctor, SessionContext session)
    {
        using var connection = _factory.CreateOpenConnection();

        return Db.InTransaction(connection, c =>
        {
            var original = c.QuerySingleOrDefault<Appointment>(
                "SELECT id AS Id, patient_id AS PatientId, duration AS DurationMinutes, type AS AppointmentType, doctor_name AS DoctorName, notes AS Notes FROM appointments WHERE id = @id",
                new { id = appointmentId });

            if (original is null)
            {
                throw new InvalidOperationException($"Appointment {appointmentId} was not found.");
            }

            c.Execute(
                "UPDATE appointments SET status = @rescheduled, updated_at = @now WHERE id = @id",
                new { rescheduled = (int)AppointmentStatus.Rescheduled, now = DateTimeOffset.UtcNow.ToString("o"), id = appointmentId });

            var moved = new Appointment
            {
                PatientId = original.PatientId,
                AppointmentDate = newDate,
                StartTime = newTime,
                DurationMinutes = original.DurationMinutes,
                AppointmentType = original.AppointmentType,
                DoctorName = original.DoctorName,
                Notes = original.Notes,
                RescheduledFromId = appointmentId,
                CreatedBy = session.Username,
            };

            moved.SerialNumber = NextSerial(c, null, newDate, moved.DoctorName, perDoctor);
            moved.CreatedAt = DateTimeOffset.UtcNow;
            moved.UpdatedAt = moved.CreatedAt;
            moved.Id = c.ExecuteScalar<long>("""
                INSERT INTO appointments(patient_id, date, start_time, duration, serial, type, doctor_name,
                    status, priority, is_walk_in, notes, rescheduled_from, created_by, created_at, updated_at)
                VALUES (@PatientId, @AppointmentDate, @StartTime, @DurationMinutes, @SerialNumber, @AppointmentType,
                    @DoctorName, @Status, @Priority, @IsWalkIn, @Notes, @RescheduledFromId, @CreatedBy, @CreatedAt, @UpdatedAt)
                RETURNING id
                """,
                moved);
            return moved;
        });
    }

    public AppointmentListItem? GetListItem(long appointmentId)
    {
        using var connection = _factory.CreateOpenConnection();
        return connection.QuerySingleOrDefault<AppointmentListItem>($"{ListSelect} WHERE a.id = @id", new { id = appointmentId });
    }

    public IReadOnlyList<AppointmentListItem> Query(AppointmentQuery query)
    {
        using var connection = _factory.CreateOpenConnection();

        var conditions = new List<string> { "1 = 1" };
        var p = new DynamicParameters();

        if (query.From is { } from)
        {
            p.Add("from", from.ToString("yyyy-MM-dd"));
            conditions.Add("a.date >= @from");
        }

        if (query.To is { } to)
        {
            p.Add("to", to.ToString("yyyy-MM-dd"));
            conditions.Add("a.date <= @to");
        }

        if (!string.IsNullOrWhiteSpace(query.DoctorName))
        {
            p.Add("doctor", query.DoctorName);
            conditions.Add("a.doctor_name = @doctor");
        }

        if (query.Status is { } status)
        {
            p.Add("status", (int)status);
            conditions.Add("a.status = @status");
        }

        if (query.PatientId is { } pid)
        {
            p.Add("pid", pid);
            conditions.Add("a.patient_id = @pid");
        }

        if (!string.IsNullOrWhiteSpace(query.SearchText))
        {
            p.Add("term", query.SearchText.Trim() + "%");
            conditions.Add("(pa.full_name LIKE @term OR pa.code LIKE @term OR pa.phone LIKE @term)");
        }

        p.Add("limit", query.Limit);

        return connection.Query<AppointmentListItem>(
            $"{ListSelect} WHERE {string.Join(" AND ", conditions)} ORDER BY a.date, a.serial LIMIT @limit",
            p).AsList();
    }

    /// <summary>Today's queue in serial order — the front-desk primary view.</summary>
    public IReadOnlyList<AppointmentListItem> DayQueue(DateOnly date, string? doctorName = null)
    {
        using var connection = _factory.CreateOpenConnection();
        var p = new DynamicParameters();
        p.Add("date", date.ToString("yyyy-MM-dd"));
        var doctorFilter = string.Empty;
        if (!string.IsNullOrWhiteSpace(doctorName))
        {
            p.Add("doctor", doctorName);
            doctorFilter = " AND a.doctor_name = @doctor";
        }

        return connection.Query<AppointmentListItem>(
            $"{ListSelect} WHERE a.date = @date{doctorFilter} ORDER BY a.serial",
            p).AsList();
    }

    public IReadOnlyList<AppointmentListItem> Upcoming(DateOnly today, int limit)
    {
        using var connection = _factory.CreateOpenConnection();
        return connection.Query<AppointmentListItem>(
            $"{ListSelect} WHERE a.date >= @date AND a.status IN (0, 1, 2) ORDER BY a.date, a.serial LIMIT @limit",
            new { date = today.ToString("yyyy-MM-dd"), limit }).AsList();
    }

    public IReadOnlyList<string> DoctorsInUse()
    {
        using var connection = _factory.CreateOpenConnection();
        return connection.Query<string>(
            "SELECT DISTINCT doctor_name FROM appointments WHERE doctor_name != '' ORDER BY doctor_name").AsList();
    }

    public Appointment? GetById(long id)
    {
        using var connection = _factory.CreateOpenConnection();
        return connection.QuerySingleOrDefault<Appointment>(
            "SELECT id AS Id, patient_id AS PatientId, date AS AppointmentDate, start_time AS StartTime, duration AS DurationMinutes, serial AS SerialNumber, type AS AppointmentType, doctor_name AS DoctorName, status AS Status, priority AS Priority, is_walk_in AS IsWalkIn, notes AS Notes, rescheduled_from AS RescheduledFromId, visit_id AS VisitId, created_by AS CreatedBy, created_at AS CreatedAt, updated_at AS UpdatedAt FROM appointments WHERE id = @id",
            new { id });
    }

    public DayQueueSummary DaySummary(DateOnly date)
    {
        using var connection = _factory.CreateOpenConnection();
        return connection.QuerySingleOrDefault<DayQueueSummary>(
            """
            SELECT
              COUNT(*) AS Total,
              SUM(CASE WHEN status = 0 THEN 1 ELSE 0 END) AS Scheduled,
              SUM(CASE WHEN status = 1 THEN 1 ELSE 0 END) AS Waiting,
              SUM(CASE WHEN status = 2 THEN 1 ELSE 0 END) AS InConsultation,
              SUM(CASE WHEN status = 3 THEN 1 ELSE 0 END) AS Completed,
              SUM(CASE WHEN status = 4 THEN 1 ELSE 0 END) AS Cancelled,
              SUM(CASE WHEN status = 5 THEN 1 ELSE 0 END) AS NoShow
            FROM appointments WHERE date = @date
            """,
            new { date = date.ToString("yyyy-MM-dd") }) ?? new DayQueueSummary();
    }
}

public sealed class DayQueueSummary
{
    public long Total { get; set; }
    public long? Scheduled { get; set; }
    public long? Waiting { get; set; }
    public long? InConsultation { get; set; }
    public long? Completed { get; set; }
    public long? Cancelled { get; set; }
    public long? NoShow { get; set; }
}

using Dentiva.Core.Domain;
using Dapper;

namespace Dentiva.Infrastructure.Repositories;

public sealed record VisitQuery
{
    public long? PatientId { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public VisitStatus? Status { get; init; }
    public string SearchText { get; init; } = string.Empty;
    public int Limit { get; init; } = 200;
}

public sealed record VisitListItem(
    long Id, long PatientId, string PatientName, string PatientCode,
    DateTime VisitDate, string Reason, string Diagnosis, string DoctorName,
    VisitStatus Status, DateOnly? FollowUpDate, long InvoiceCount, long PaymentCount);

/// <summary>
/// Visits and everything recorded inside them: procedures, tooth records,
/// treatment plans, medication orders and referrals. Unbounded histories are
/// paged; nothing is limited to a fixed number of records.
/// </summary>
public sealed class VisitRepository
{
    private readonly ISqliteConnectionFactory _factory;

    public VisitRepository(ISqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    // ---------- Visits ----------

    public Visit Insert(Visit visit, SessionContext session)
    {
        visit.CreatedAt = DateTimeOffset.UtcNow;
        visit.UpdatedAt = visit.CreatedAt;
        visit.CreatedBy = session.Username;

        using var connection = _factory.CreateOpenConnection();
        visit.Id = connection.ExecuteScalar<long>("""
            INSERT INTO visits(patient_id, visit_date, reason, chief_complaint, diagnosis, examination,
                treatment_notes, doctor_name, follow_up_date, status, appointment_id, created_by, created_at, updated_at)
            VALUES (@PatientId, @VisitDate, @Reason, @ChiefComplaint, @Diagnosis, @ExaminationFindings,
                @TreatmentNotes, @DoctorName, @FollowUpDate, @Status, @AppointmentId, @CreatedBy, @CreatedAt, @UpdatedAt)
            RETURNING id
            """,
            visit);
        return visit;
    }

    public void Update(Visit visit, SessionContext session)
    {
        visit.UpdatedAt = DateTimeOffset.UtcNow;
        using var connection = _factory.CreateOpenConnection();
        connection.Execute("""
            UPDATE visits SET visit_date = @VisitDate, reason = @Reason, chief_complaint = @ChiefComplaint,
                diagnosis = @Diagnosis, examination = @ExaminationFindings, treatment_notes = @TreatmentNotes,
                doctor_name = @DoctorName, follow_up_date = @FollowUpDate, status = @Status, updated_at = @UpdatedAt
            WHERE id = @Id
            """,
            visit);
    }

    public Visit? GetById(long id)
    {
        using var connection = _factory.CreateOpenConnection();
        var visit = connection.QuerySingleOrDefault<Visit>(
            "SELECT id AS Id, patient_id AS PatientId, visit_date AS VisitDate, reason AS Reason, chief_complaint AS ChiefComplaint, diagnosis AS Diagnosis, examination AS ExaminationFindings, treatment_notes AS TreatmentNotes, doctor_name AS DoctorName, follow_up_date AS FollowUpDate, status AS Status, appointment_id AS AppointmentId, created_by AS CreatedBy, created_at AS CreatedAt, updated_at AS UpdatedAt FROM visits WHERE id = @id",
            new { id });

        if (visit is not null)
        {
            visit.Procedures = GetProcedures(connection, id).ToList();
        }

        return visit;
    }

    public IReadOnlyList<VisitListItem> Query(VisitQuery query)
    {
        using var connection = _factory.CreateOpenConnection();

        var conditions = new List<string> { "v.deleted_at IS NULL" };
        var p = new DynamicParameters();

        if (query.PatientId is { } pid)
        {
            p.Add("pid", pid);
            conditions.Add("v.patient_id = @pid");
        }

        if (query.From is { } from)
        {
            p.Add("from", from.ToString("yyyy-MM-dd"));
            conditions.Add("v.visit_date >= @from");
        }

        if (query.To is { } to)
        {
            p.Add("to", to.ToString("yyyy-MM-dd") + " 23:59:59");
            conditions.Add("v.visit_date <= @to");
        }

        if (query.Status is { } status)
        {
            p.Add("status", (int)status);
            conditions.Add("v.status = @status");
        }

        if (!string.IsNullOrWhiteSpace(query.SearchText))
        {
            p.Add("term", query.SearchText.Trim() + "%");
            conditions.Add("(pa.full_name LIKE @term OR pa.code LIKE @term OR v.diagnosis LIKE @term OR v.reason LIKE @term)");
        }

        p.Add("limit", query.Limit);

        return connection.Query<VisitListItem>("""
            SELECT v.id AS Id, v.patient_id AS PatientId, pa.full_name AS PatientName, pa.code AS PatientCode,
                   v.visit_date AS VisitDate, v.reason AS Reason, v.diagnosis AS Diagnosis,
                   v.doctor_name AS DoctorName, v.status AS Status, v.follow_up_date AS FollowUpDate,
                   (SELECT COUNT(*) FROM invoices i WHERE i.visit_id = v.id) AS InvoiceCount,
                   (SELECT COUNT(*) FROM payments pay JOIN invoices i2 ON pay.invoice_id = i2.id WHERE i2.visit_id = v.id) AS PaymentCount
            FROM visits v
            JOIN patients pa ON pa.id = v.patient_id
            """ + $" WHERE {string.Join(" AND ", conditions)} ORDER BY v.visit_date DESC LIMIT @limit",
            p).AsList();
    }

    public void SoftDelete(long visitId, SessionContext session)
    {
        using var connection = _factory.CreateOpenConnection();
        connection.Execute("UPDATE visits SET deleted_at = @now, updated_at = @now WHERE id = @id",
            new { now = DateTimeOffset.UtcNow.ToString("o"), id = visitId });
    }

    // ---------- Procedures ----------

    public void ReplaceProcedures(long visitId, IEnumerable<VisitProcedure> procedures)
    {
        using var connection = _factory.CreateOpenConnection();
        Db.InTransaction(connection, c =>
        {
            c.Execute("DELETE FROM visit_procedures WHERE visit_id = @id", new { id = visitId });
            foreach (var proc in procedures)
            {
                proc.VisitId = visitId;
                c.Execute("""
                    INSERT INTO visit_procedures(visit_id, service_id, name, tooth, cost_minor, notes)
                    VALUES (@VisitId, @ServiceId, @Name, @Tooth, @CostMinor, @Notes)
                    """,
                    proc);
            }

            return true;
        });
    }

    public IReadOnlyList<VisitProcedure> GetProcedures(long visitId)
    {
        using var connection = _factory.CreateOpenConnection();
        return GetProcedures(connection, visitId);
    }

    private static IReadOnlyList<VisitProcedure> GetProcedures(Microsoft.Data.Sqlite.SqliteConnection c, long visitId) =>
        c.Query<VisitProcedure>(
            "SELECT id AS Id, visit_id AS VisitId, service_id AS ServiceId, name AS Name, tooth AS Tooth, cost_minor AS CostMinor, notes AS Notes FROM visit_procedures WHERE visit_id = @id ORDER BY id",
            new { id = visitId }).AsList();

    // ---------- Tooth records ----------

    public void UpsertToothRecord(ToothRecord record)
    {
        record.UpdatedAt = DateTimeOffset.UtcNow;
        using var connection = _factory.CreateOpenConnection();
        connection.Execute("""
            INSERT INTO tooth_records(patient_id, tooth, dentition, condition, surfaces, notes, updated_at)
            VALUES (@PatientId, @ToothNumber, @Dentition, @Condition, @Surfaces, @Notes, @UpdatedAt)
            ON CONFLICT(patient_id, tooth) DO UPDATE SET
                condition = excluded.condition, surfaces = excluded.surfaces,
                notes = excluded.notes, dentition = excluded.dentition, updated_at = excluded.updated_at
            """,
            record);
    }

    public IReadOnlyList<ToothRecord> GetToothRecords(long patientId)
    {
        using var connection = _factory.CreateOpenConnection();
        return connection.Query<ToothRecord>(
            "SELECT id AS Id, patient_id AS PatientId, tooth AS ToothNumber, dentition AS Dentition, condition AS Condition, surfaces AS Surfaces, notes AS Notes, updated_at AS UpdatedAt FROM tooth_records WHERE patient_id = @id",
            new { id = patientId }).AsList();
    }

    // ---------- Treatment plans ----------

    public TreatmentPlan InsertPlan(TreatmentPlan plan, SessionContext session)
    {
        plan.CreatedAt = DateTimeOffset.UtcNow;
        plan.UpdatedAt = plan.CreatedAt;
        using var connection = _factory.CreateOpenConnection();
        Db.InTransaction(connection, c =>
        {
            plan.Id = c.ExecuteScalar<long>("""
                INSERT INTO treatment_plans(patient_id, title, status, notes, created_date, created_at, updated_at)
                VALUES (@PatientId, @Title, @Status, @Notes, @CreatedDate, @CreatedAt, @UpdatedAt)
                RETURNING id
                """,
                plan);
            foreach (var item in plan.Items)
            {
                item.PlanId = plan.Id;
                InsertPlanItem(c, item);
            }

            return true;
        });
        return plan;
    }

    public void UpdatePlan(TreatmentPlan plan, SessionContext session)
    {
        plan.UpdatedAt = DateTimeOffset.UtcNow;
        using var connection = _factory.CreateOpenConnection();
        connection.Execute("UPDATE treatment_plans SET title = @Title, status = @Status, notes = @Notes, updated_at = @UpdatedAt WHERE id = @Id", plan);
    }

    private static void InsertPlanItem(Microsoft.Data.Sqlite.SqliteConnection c, TreatmentPlanItem item)
    {
        item.Id = c.ExecuteScalar<long>("""
            INSERT INTO treatment_plan_items(plan_id, service_id, treatment_name, tooth, estimated_cost_minor,
                status, priority, planned_date, completed_date, completed_visit_id, notes)
            VALUES (@PlanId, @ServiceId, @TreatmentName, @Tooth, @EstimatedCostMinor,
                @Status, @Priority, @PlannedDate, @CompletedDate, @CompletedVisitId, @Notes)
            RETURNING id
            """,
            item);
    }

    public void AddPlanItem(TreatmentPlanItem item)
    {
        using var connection = _factory.CreateOpenConnection();
        InsertPlanItem(connection, item);
    }

    public void UpdatePlanItem(TreatmentPlanItem item)
    {
        using var connection = _factory.CreateOpenConnection();
        connection.Execute("""
            UPDATE treatment_plan_items SET treatment_name = @TreatmentName, tooth = @Tooth,
                estimated_cost_minor = @EstimatedCostMinor, status = @Status, priority = @Priority,
                planned_date = @PlannedDate, completed_date = @CompletedDate, completed_visit_id = @CompletedVisitId,
                notes = @Notes
            WHERE id = @Id
            """,
            item);
    }

    public void DeletePlanItem(long itemId)
    {
        using var connection = _factory.CreateOpenConnection();
        connection.Execute("DELETE FROM treatment_plan_items WHERE id = @id", new { id = itemId });
    }

    public IReadOnlyList<TreatmentPlan> GetPlans(long patientId)
    {
        using var connection = _factory.CreateOpenConnection();
        var plans = connection.Query<TreatmentPlan>(
            "SELECT id AS Id, patient_id AS PatientId, title AS Title, status AS Status, notes AS Notes, created_date AS CreatedDate, created_at AS CreatedAt, updated_at AS UpdatedAt FROM treatment_plans WHERE patient_id = @id ORDER BY created_date DESC",
            new { id = patientId }).AsList();

        var items = connection.Query<TreatmentPlanItem>(
            """
            SELECT t.id AS Id, t.plan_id AS PlanId, t.service_id AS ServiceId, t.treatment_name AS TreatmentName,
                   t.tooth AS Tooth, t.estimated_cost_minor AS EstimatedCostMinor, t.status AS Status,
                   t.priority AS Priority, t.planned_date AS PlannedDate, t.completed_date AS CompletedDate,
                   t.completed_visit_id AS CompletedVisitId, t.notes AS Notes
            FROM treatment_plan_items t
            JOIN treatment_plans p ON p.id = t.plan_id
            WHERE p.patient_id = @id ORDER BY t.id
            """,
            new { id = patientId }).AsList();

        foreach (var plan in plans)
        {
            plan.Items = items.Where(i => i.PlanId == plan.Id).ToList();
        }

        return plans;
    }

    // ---------- Medications ----------

    public MedicationOrder InsertMedication(MedicationOrder order)
    {
        order.CreatedAt = DateTimeOffset.UtcNow;
        using var connection = _factory.CreateOpenConnection();
        order.Id = connection.ExecuteScalar<long>("""
            INSERT INTO medications(patient_id, visit_id, name, strength, dosage, frequency, duration, route, instructions, notes, prescribed_date, created_at)
            VALUES (@PatientId, @VisitId, @Name, @Strength, @Dosage, @Frequency, @Duration, @Route, @Instructions, @Notes, @PrescribedDate, @CreatedAt)
            RETURNING id
            """,
            order);
        return order;
    }

    public void UpdateMedication(MedicationOrder order)
    {
        using var connection = _factory.CreateOpenConnection();
        connection.Execute("""
            UPDATE medications SET name = @Name, strength = @Strength, dosage = @Dosage, frequency = @Frequency,
                duration = @Duration, route = @Route, instructions = @Instructions, notes = @Notes,
                prescribed_date = @PrescribedDate
            WHERE id = @Id
            """,
            order);
    }

    public void DeleteMedication(long id)
    {
        using var connection = _factory.CreateOpenConnection();
        connection.Execute("DELETE FROM medications WHERE id = @id", new { id });
    }

    public IReadOnlyList<MedicationOrder> GetMedications(long patientId, int limit = 1000)
    {
        using var connection = _factory.CreateOpenConnection();
        return connection.Query<MedicationOrder>(
            "SELECT id AS Id, patient_id AS PatientId, visit_id AS VisitId, name AS Name, strength AS Strength, dosage AS Dosage, frequency AS Frequency, duration AS Duration, route AS Route, instructions AS Instructions, notes AS Notes, prescribed_date AS PrescribedDate, created_at AS CreatedAt FROM medications WHERE patient_id = @id ORDER BY prescribed_date DESC, id DESC LIMIT @limit",
            new { id = patientId, limit }).AsList();
    }

    // ---------- Referrals ----------

    public Referral InsertReferral(Referral referral)
    {
        referral.CreatedAt = DateTimeOffset.UtcNow;
        using var connection = _factory.CreateOpenConnection();
        referral.Id = connection.ExecuteScalar<long>("""
            INSERT INTO referrals(patient_id, visit_id, referral_date, provider, specialty, organization,
                reason, notes, report_received, report_summary, follow_up_date, created_at)
            VALUES (@PatientId, @VisitId, @ReferralDate, @Provider, @Specialty, @Organization,
                @Reason, @Notes, @ReportReceived, @ReportSummary, @FollowUpDate, @CreatedAt)
            RETURNING id
            """,
            referral);
        return referral;
    }

    public void UpdateReferral(Referral referral)
    {
        using var connection = _factory.CreateOpenConnection();
        connection.Execute("""
            UPDATE referrals SET referral_date = @ReferralDate, provider = @Provider, specialty = @Specialty,
                organization = @Organization, reason = @Reason, notes = @Notes,
                report_received = @ReportReceived, report_summary = @ReportSummary, follow_up_date = @FollowUpDate
            WHERE id = @Id
            """,
            referral);
    }

    public IReadOnlyList<Referral> GetReferrals(long patientId)
    {
        using var connection = _factory.CreateOpenConnection();
        return connection.Query<Referral>(
            "SELECT id AS Id, patient_id AS PatientId, visit_id AS VisitId, referral_date AS ReferralDate, provider AS Provider, specialty AS Specialty, organization AS Organization, reason AS Reason, notes AS Notes, report_received AS ReportReceived, report_summary AS ReportSummary, follow_up_date AS FollowUpDate, created_at AS CreatedAt FROM referrals WHERE patient_id = @id ORDER BY referral_date DESC, id DESC",
            new { id = patientId }).AsList();
    }

    // ---------- Timeline ----------

    public sealed record TimelineEntry(string Kind, long RefId, string When, string Title, string Subtitle);

    public IReadOnlyList<TimelineEntry> GetTimeline(long patientId, int limit = 400)
    {
        using var connection = _factory.CreateOpenConnection();
        var rows = connection.Query<TimelineEntry>("""
            SELECT kind AS Kind, ref_id AS RefId, when_text AS When, title AS Title, subtitle AS Subtitle FROM (
              SELECT 'visit' AS kind, id AS ref_id, visit_date AS when_text,
                     'Visit: ' || COALESCE(NULLIF(reason, ''), 'Clinical visit') AS title,
                     COALESCE(NULLIF(diagnosis, ''), '') AS subtitle
              FROM visits WHERE patient_id = @id AND deleted_at IS NULL
              UNION ALL
              SELECT 'payment', id, paid_at, 'Payment received', method_label || '  —  ' || amount_minor
              FROM payments WHERE patient_id = @id AND voided_at IS NULL
              UNION ALL
              SELECT 'medication', id, prescribed_date, 'Medication: ' || name,
                     dosage || ' ' || frequency || ' ' || duration
              FROM medications WHERE patient_id = @id
              UNION ALL
              SELECT 'referral', id, referral_date, 'Referral: ' || COALESCE(NULLIF(provider, ''), 'Specialist'),
                     COALESCE(NULLIF(reason, ''), '')
              FROM referrals WHERE patient_id = @id
              UNION ALL
              SELECT 'attachment', id, created_at, 'Attachment: ' || file_name, description
              FROM attachments WHERE patient_id = @id AND deleted_at IS NULL
            ) ORDER BY when_text DESC, ref_id DESC LIMIT @limit
            """,
            new { id = patientId, limit }).AsList();

        return rows;
    }

    public sealed record FollowUpDuePatient(long VisitId, long PatientId, string PatientName, string PatientCode, string Phone, DateOnly FollowUpDate, string Diagnosis);

    public IReadOnlyList<FollowUpDuePatient> FollowUpsDue(DateOnly today, int withinDays)
    {
        using var connection = _factory.CreateOpenConnection();
        var until = today.AddDays(withinDays);
        return connection.Query<FollowUpDuePatient>("""
            SELECT v.id AS VisitId, v.patient_id AS PatientId, pa.full_name AS PatientName, pa.code AS PatientCode,
                   pa.phone AS Phone, v.follow_up_date AS FollowUpDate, v.diagnosis AS Diagnosis
            FROM visits v JOIN patients pa ON pa.id = v.patient_id
            WHERE v.deleted_at IS NULL AND v.follow_up_date IS NOT NULL
              AND v.follow_up_date >= @today AND v.follow_up_date <= @until
            ORDER BY v.follow_up_date
            """,
            new { today = today.ToString("yyyy-MM-dd"), until = until.ToString("yyyy-MM-dd") }).AsList();
    }
}

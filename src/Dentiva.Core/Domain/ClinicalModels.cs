namespace Dentiva.Core.Domain;

public sealed class Visit
{
    public long Id { get; set; }
    public long PatientId { get; set; }
    public DateTime VisitDate { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string ChiefComplaint { get; set; } = string.Empty;
    public string Diagnosis { get; set; } = string.Empty;
    public string ExaminationFindings { get; set; } = string.Empty;
    public string TreatmentNotes { get; set; } = string.Empty;
    public string DoctorName { get; set; } = string.Empty;
    public DateOnly? FollowUpDate { get; set; }
    public VisitStatus Status { get; set; } = VisitStatus.Open;
    public long? AppointmentId { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}

public sealed class VisitProcedure
{
    public long Id { get; set; }
    public long VisitId { get; set; }
    public long? ServiceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Tooth { get; set; } = string.Empty;
    public long CostMinor { get; set; }
    public string Notes { get; set; } = string.Empty;
}

public sealed class ToothRecord
{
    public long Id { get; set; }
    public long PatientId { get; set; }
    public string ToothNumber { get; set; } = string.Empty;
    public Dentition Dentition { get; set; } = Dentition.Permanent;
    public ToothCondition Condition { get; set; }
    public string Surfaces { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class TreatmentPlan
{
    public long Id { get; set; }
    public long PatientId { get; set; }
    public string Title { get; set; } = string.Empty;
    public TreatmentPlanStatus Status { get; set; } = TreatmentPlanStatus.Active;
    public string Notes { get; set; } = string.Empty;
    public DateOnly CreatedDate { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public List<TreatmentPlanItem> Items { get; set; } = new();
}

public sealed class TreatmentPlanItem
{
    public long Id { get; set; }
    public long PlanId { get; set; }
    public long? ServiceId { get; set; }
    public string TreatmentName { get; set; } = string.Empty;
    public string Tooth { get; set; } = string.Empty;
    public long EstimatedCostMinor { get; set; }
    public TreatmentPlanItemStatus Status { get; set; } = TreatmentPlanItemStatus.Planned;
    public AppointmentPriority Priority { get; set; }
    public DateOnly? PlannedDate { get; set; }
    public DateOnly? CompletedDate { get; set; }
    public long? CompletedVisitId { get; set; }
    public string Notes { get; set; } = string.Empty;
}

public sealed class MedicationOrder
{
    public long Id { get; set; }
    public long PatientId { get; set; }
    public long? VisitId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Strength { get; set; } = string.Empty;
    public string Dosage { get; set; } = string.Empty;
    public string Frequency { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public string Route { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public DateOnly PrescribedDate { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class Referral
{
    public long Id { get; set; }
    public long PatientId { get; set; }
    public long? VisitId { get; set; }
    public DateOnly ReferralDate { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string Specialty { get; set; } = string.Empty;
    public string Organization { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public bool ReportReceived { get; set; }
    public string ReportSummary { get; set; } = string.Empty;
    public DateOnly? FollowUpDate { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class AttachmentInfo
{
    public long Id { get; set; }
    public long? PatientId { get; set; }
    public long? VisitId { get; set; }
    public long? ReferralId { get; set; }
    public long? StaffId { get; set; }
    public long? InvoiceId { get; set; }
    public AttachmentCategory Category { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string StoredName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public int? PixelWidth { get; set; }
    public int? PixelHeight { get; set; }
    public string Description { get; set; } = string.Empty;
    public string AddedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string Sha256 { get; set; } = string.Empty;
}

public sealed class Appointment
{
    public long Id { get; set; }
    public long PatientId { get; set; }
    public DateOnly AppointmentDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public int DurationMinutes { get; set; } = 30;
    public int SerialNumber { get; set; }
    public string AppointmentType { get; set; } = string.Empty;
    public string DoctorName { get; set; } = string.Empty;
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Scheduled;
    public AppointmentPriority Priority { get; set; }
    public bool IsWalkIn { get; set; }
    public string Notes { get; set; } = string.Empty;
    public long? RescheduledFromId { get; set; }
    public long? VisitId { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

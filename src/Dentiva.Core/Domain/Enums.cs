namespace Dentiva.Core.Domain;

public enum Gender
{
    Unspecified = 0,
    Female = 1,
    Male = 2,
    Other = 3,
}

public enum PatientStatus
{
    Active = 0,
    Inactive = 1,
    Archived = 2,
}

public enum VisitStatus
{
    Open = 0,
    Completed = 1,
    Cancelled = 2,
}

public enum AppointmentStatus
{
    Scheduled = 0,
    Waiting = 1,
    InConsultation = 2,
    Completed = 3,
    Cancelled = 4,
    NoShow = 5,
    Rescheduled = 6,
}

public enum AppointmentPriority
{
    Normal = 0,
    Urgent = 1,
    Emergency = 2,
}

public enum Dentition
{
    Permanent = 0,
    Deciduous = 1,
}

public enum ToothCondition
{
    Healthy = 0,
    Decayed = 1,
    Filled = 2,
    RootCanalTreated = 3,
    Crowned = 4,
    Extracted = 5,
    Missing = 6,
    Implanted = 7,
    Other = 8,
}

public enum TreatmentPlanItemStatus
{
    Planned = 0,
    InProgress = 1,
    Completed = 2,
    Deferred = 3,
    Cancelled = 4,
}

public enum TreatmentPlanStatus
{
    Active = 0,
    Completed = 1,
    Cancelled = 2,
}

public enum InvoiceStatus
{
    Unpaid = 0,
    PartiallyPaid = 1,
    Paid = 2,
    Voided = 3,
}

public enum PaymentMethodKind
{
    Cash = 0,
    Bank = 1,
    Card = 2,
    MobileFinancialService = 3,
    Other = 4,
}

public enum DiscountKind
{
    None = 0,
    FixedAmount = 1,
    Percentage = 2,
}

public enum ExpenseFrequency
{
    OneOff = 0,
    Recurring = 1,
}

public enum StaffStatus
{
    Active = 0,
    OnLeave = 1,
    Terminated = 2,
}

public enum SalaryFrequency
{
    Monthly = 0,
    BiWeekly = 1,
    Weekly = 2,
    Daily = 3,
}

public enum NotificationKind
{
    AppointmentReminder = 0,
    FollowUpDue = 1,
    UnpaidBalance = 2,
    OverdueInvoice = 3,
    MissedAppointment = 4,
    IncompleteTreatmentPlan = 5,
    PatientAlert = 6,
    BackupReminder = 7,
    SystemWarning = 8,
    StorageWarning = 9,
    DataIntegrity = 10,
}

public enum NotificationPriority
{
    Normal = 0,
    Important = 1,
    Critical = 2,
}

public enum AttachmentCategory
{
    XRay = 0,
    Photograph = 1,
    Report = 2,
    Document = 3,
    ReferralDocument = 4,
    Prescription = 5,
    Consent = 6,
    InvoiceDocument = 7,
    Other = 8,
}

public enum DatePeriodKind
{
    Today = 0,
    Last7Days = 1,
    ThisMonth = 2,
    Last3Months = 3,
    Last6Months = 4,
    Last12Months = 5,
    Custom = 6,
}

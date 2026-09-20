namespace Dentiva.Core.Security;

/// <summary>
/// Granular application permissions. Roles map to a permission mask stored as
/// a ulong; the mask is persisted with each role and checked at the service
/// layer so UI affordances and actions always agree.
/// </summary>
[Flags]
public enum AppPermission : ulong
{
    None = 0,

    PatientsView = 1UL << 0,
    PatientsEdit = 1UL << 1,
    PatientsDelete = 1UL << 2,

    AppointmentsManage = 1UL << 3,
    ClinicalRecords = 1UL << 4,
    DocumentsManage = 1UL << 5,

    BillingManage = 1UL << 6,
    PaymentsManage = 1UL << 7,

    FinanceView = 1UL << 8,
    FinanceManage = 1UL << 9,

    StaffManage = 1UL << 10,
    ReportsView = 1UL << 11,

    SettingsManage = 1UL << 12,
    BackupRestore = 1UL << 13,
    AuditView = 1UL << 14,
    UserManagement = 1UL << 15,
    Maintenance = 1UL << 16,
}

public static class PermissionPresets
{
    public const string Administrator = "Administrator";
    public const string Dentist = "Dentist";
    public const string Receptionist = "Receptionist";
    public const string Accountant = "Accountant";
    public const string Assistant = "Assistant";

    public static AppPermission ForRole(string roleName) => roleName switch
    {
        Administrator => AppPermission.None | (AppPermission)0xFFFFFUL, // bits 0..19
        Dentist => AppPermission.PatientsView | AppPermission.PatientsEdit
                 | AppPermission.AppointmentsManage | AppPermission.ClinicalRecords
                 | AppPermission.DocumentsManage | AppPermission.BillingManage
                 | AppPermission.ReportsView,
        Receptionist => AppPermission.PatientsView | AppPermission.PatientsEdit
                     | AppPermission.AppointmentsManage | AppPermission.BillingManage
                     | AppPermission.PaymentsManage | AppPermission.DocumentsManage,
        Accountant => AppPermission.PatientsView | AppPermission.BillingManage
                   | AppPermission.PaymentsManage | AppPermission.FinanceView
                   | AppPermission.FinanceManage | AppPermission.ReportsView,
        Assistant => AppPermission.PatientsView | AppPermission.AppointmentsManage
                  | AppPermission.ClinicalRecords | AppPermission.DocumentsManage,
        _ => AppPermission.None,
    };

    public static readonly string[] BuiltinRoleNames =
    {
        Administrator, Dentist, Receptionist, Accountant, Assistant,
    };
}

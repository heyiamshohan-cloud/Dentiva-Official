using Dentiva.Core.Security;

namespace Dentiva.Core.Domain;

public sealed class ClinicInfo
{
    public string ClinicName { get; set; } = string.Empty;
    public string DoctorName { get; set; } = string.Empty;
    public string DoctorDegrees { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string AlternatePhone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string Website { get; set; } = string.Empty;
    public string RegistrationNumber { get; set; } = string.Empty;
    public string LogoStoredName { get; set; } = string.Empty;
    public string CurrencySymbol { get; set; } = "৳";
    public string CurrencyCode { get; set; } = "BDT";
    public string DocumentFooter { get; set; } = string.Empty;
}

public sealed class UserAccount
{
    public long Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
}

public sealed class AuditEntry
{
    public long Id { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public long? UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Entity { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
}

public sealed class NotificationItem
{
    public long Id { get; set; }
    public NotificationKind Kind { get; set; }
    public NotificationPriority Priority { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Entity { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
    public DateTimeOffset? DismissedAt { get; set; }
    public DateTimeOffset? SnoozedUntil { get; set; }
}

/// <summary>Effective session identity used by services and the audit trail.</summary>
public sealed record SessionContext(long UserId, string Username, string DisplayName, string RoleName, AppPermission Permissions)
{
    public static SessionContext System { get; } = new(0, "system", "System", PermissionPresets.Administrator, PermissionPresets.ForRole(PermissionPresets.Administrator));

    public bool Has(AppPermission required) => Permissions.HasFlag(required);
}

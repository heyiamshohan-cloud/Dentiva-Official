using Dentiva.Core.Domain;

namespace Dentiva.Core.Settings;

public sealed class AppSettings
{
    public int SettingsVersion { get; set; } = 1;

    public ClinicInfo Clinic { get; set; } = new();

    public LocalizationSettings Localization { get; set; } = new();

    public AppearanceSettings Appearance { get; set; } = new();

    public ClinicalSettings Clinical { get; set; } = new();

    public AppointmentSettings Appointments { get; set; } = new();

    public BillingSettings Billing { get; set; } = new();

    public PrintingSettings Printing { get; set; } = new();

    public NotificationSettings Notifications { get; set; } = new();

    public BackupSettings Backup { get; set; } = new();

    public SecuritySettings Security { get; set; } = new();

    public DashboardSettings Dashboard { get; set; } = new();

    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class LocalizationSettings
{
    /// <summary>"en" or "bn".</summary>
    public string Language { get; set; } = "en";
    public string DateFormat { get; set; } = "dd MMM yyyy";
    public string TimeFormat { get; set; } = "HH:mm";
    public bool UseBengaliDigits { get; set; }
}

public sealed class AppearanceSettings
{
    /// <summary>Hex accent, e.g. #0E7490. The theme stays light regardless of OS theme.</summary>
    public string AccentHex { get; set; } = "#0E7490";
    public double UiScalePercent { get; set; } = 100;
    public bool CompactLists { get; set; }
}

public sealed class ClinicalSettings
{
    public string DefaultVisitReason { get; set; } = string.Empty;
    public int FollowUpReminderDays { get; set; } = 1;
    public bool ShowDeciduousTeethByDefault { get; set; }
    public List<string> CustomMedicationSuggestions { get; set; } = new();
}

public sealed class AppointmentSettings
{
    public int DefaultDurationMinutes { get; set; } = 30;
    public TimeOnly WorkdayStart { get; set; } = new(9, 0);
    public TimeOnly WorkdayEnd { get; set; } = new(18, 0);
    /// <summary>0 = Sunday … 6 = Saturday (DayOfWeek order).</summary>
    public List<int> WorkingDays { get; set; } = new() { 0, 1, 2, 3, 4, 6 };
    /// <summary>true: serials are allocated per doctor per day; false: one daily sequence.</summary>
    public bool SerialPerDoctor { get; set; } = true;
    public bool ResetSerialDaily { get; set; } = true;
    public bool AllowManualSerialOverride { get; set; } = true;
    public string DefaultAppointmentType { get; set; } = "Consultation";
    public List<string> AppointmentTypes { get; set; } = new()
    {
        "Consultation", "Follow-up", "Scaling", "Filling", "Extraction",
        "Root Canal", "Crown Fitting", "Emergency", "Other",
    };
}

public sealed class PaymentMethodConfig
{
    public PaymentMethodKind Kind { get; set; }
    public string Label { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string DetailsHint { get; set; } = string.Empty;
}

public sealed class BillingSettings
{
    public string InvoiceNumberTemplate { get; set; } = "INV-{YYYY}-{SEQ:4}";
    public string ReceiptNumberTemplate { get; set; } = "RCP-{YYYY}-{SEQ:4}";
    public bool TaxEnabled { get; set; }
    public decimal DefaultTaxPercent { get; set; }
    public DiscountKind DefaultDiscountKind { get; set; } = DiscountKind.None;
    public List<PaymentMethodConfig> PaymentMethods { get; set; } = new()
    {
        new PaymentMethodConfig { Kind = PaymentMethodKind.Cash, Label = "Cash" },
        new PaymentMethodConfig { Kind = PaymentMethodKind.Bank, Label = "Bank Transfer", DetailsHint = "Bank / account" },
        new PaymentMethodConfig { Kind = PaymentMethodKind.Card, Label = "Card" },
        new PaymentMethodConfig { Kind = PaymentMethodKind.MobileFinancialService, Label = "Mobile Banking", DetailsHint = "Provider / number" },
        new PaymentMethodConfig { Kind = PaymentMethodKind.Other, Label = "Other" },
    };
}

public sealed record PaperSizeOption(string Key, string Name, double WidthMm, double HeightMm)
{
    public static readonly PaperSizeOption A4 = new("A4", "A4 (210 × 297 mm)", 210, 297);
    public static readonly PaperSizeOption Letter = new("Letter", "Letter (216 × 279 mm)", 215.9, 279.4);
    public static readonly PaperSizeOption Thermal80 = new("Thermal80", "Thermal 80 mm", 80, 297);
    public static readonly PaperSizeOption Thermal58 = new("Thermal58", "Thermal 58 mm", 58, 297);
    public static readonly PaperSizeOption Custom = new("Custom", "Custom…", 0, 0);

    public static readonly PaperSizeOption[] All = { A4, Letter, Thermal80, Thermal58, Custom };
}

public sealed class PrintingSettings
{
    public string DefaultPrinterName { get; set; } = string.Empty;
    public string InvoicePaperKey { get; set; } = PaperSizeOption.A4.Key;
    public double CustomPaperWidthMm { get; set; } = 210;
    public double CustomPaperHeightMm { get; set; } = 297;
    public string ReceiptPaperKey { get; set; } = PaperSizeOption.Thermal80.Key;
    public double MarginMm { get; set; } = 12;
    public double ReceiptMarginMm { get; set; } = 4;
    public bool ShowLogoOnDocuments { get; set; } = true;
    public bool ShowSignatureLine { get; set; } = true;
    public string SignatureLabel { get; set; } = "Authorized Signature";
    public int PrintQualityDpi { get; set; } = 300;
}

public sealed class NotificationSettings
{
    public bool EnableAppointmentReminders { get; set; } = true;
    public bool EnableFollowUpReminders { get; set; } = true;
    public bool EnableOverdueInvoiceAlerts { get; set; } = true;
    public int OverdueInvoiceDays { get; set; } = 7;
    public bool EnableSystemWarnings { get; set; } = true;
    public bool QuietModeOutsideWorkingHours { get; set; }
}

public sealed class BackupSettings
{
    public bool AutoBackupEnabled { get; set; } = true;
    /// <summary>Hours between automatic backups.</summary>
    public int IntervalHours { get; set; } = 24;
    public string TargetFolder { get; set; } = string.Empty;
    public int KeepCount { get; set; } = 14;
    public bool IncludeAttachments { get; set; } = true;
    public DateTimeOffset LastBackupAt { get; set; }
    public DateTimeOffset LastSuccessfulRestoreAt { get; set; }
}

public sealed class SecuritySettings
{
    public bool AutoLockEnabled { get; set; } = true;
    public int AutoLockMinutes { get; set; } = 10;
    public bool RequirePasswordOnStart { get; set; } = true;
    public int MinPasswordLength { get; set; } = 8;
    public int AuditRetentionDays { get; set; } = 3650;
}

public sealed class DashboardWidgetState
{
    public string WidgetKey { get; set; } = string.Empty;
    public bool Visible { get; set; } = true;
    public int Order { get; set; }
}

public sealed class DashboardSettings
{
    public List<DashboardWidgetState> Widgets { get; set; } = new();
}

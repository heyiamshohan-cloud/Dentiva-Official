using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Dentiva.Core.Domain;

namespace Dentiva.App.Infrastructure;

public sealed class DateConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var format = AppServices.GetService<SettingsStore>()?.Settings.Localization.DateFormat ?? "dd MMM yyyy";
        return value switch
        {
            DateOnly d => d.ToString(format),
            DateTime dt => dt.ToString(format),
            _ => string.Empty,
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class TimeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var format = AppServices.GetService<SettingsStore>()?.Settings.Localization.TimeFormat ?? "HH:mm";
        return value switch
        {
            TimeOnly t => t.ToString(format),
            DateTime dt => dt.ToString(format),
            _ => string.Empty,
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class MoneyConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not long minor)
        {
            return string.Empty;
        }

        var symbol = AppServices.GetService<SettingsStore>()?.Settings.Clinic.CurrencySymbol ?? "৳";
        return symbol + MoneyMath.ToMajor(minor).ToString("N2");
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class InitialsConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var name = value as string;
        if (string.IsNullOrWhiteSpace(name))
        {
            return "?";
        }

        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var initials = string.Concat(parts.Take(2).Select(p => char.ToUpperInvariant(p[0])));
        return initials;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Status → label through localization tables.</summary>
public sealed class StatusLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        AppointmentStatus.Scheduled => LocalizationManager.T("status.scheduled"),
        AppointmentStatus.Waiting => LocalizationManager.T("status.waiting"),
        AppointmentStatus.InConsultation => LocalizationManager.T("status.inConsultation"),
        AppointmentStatus.Completed => LocalizationManager.T("status.completed"),
        AppointmentStatus.Cancelled => LocalizationManager.T("status.cancelled"),
        AppointmentStatus.NoShow => LocalizationManager.T("status.noShow"),
        AppointmentStatus.Rescheduled => LocalizationManager.T("status.rescheduled"),
        InvoiceStatus.Unpaid => LocalizationManager.T("status.unpaid"),
        InvoiceStatus.PartiallyPaid => LocalizationManager.T("status.partiallyPaid"),
        InvoiceStatus.Paid => LocalizationManager.T("status.paidStatus"),
        InvoiceStatus.Voided => LocalizationManager.T("status.voided"),
        VisitStatus.Open => LocalizationManager.T("status.open"),
        VisitStatus.Completed => LocalizationManager.T("status.completed"),
        VisitStatus.Cancelled => LocalizationManager.T("status.cancelled"),
        PatientStatus.Active => LocalizationManager.T("status.active"),
        PatientStatus.Inactive => LocalizationManager.T("status.inactive"),
        PatientStatus.Archived => LocalizationManager.T("status.archived"),
        _ => value?.ToString() ?? string.Empty,
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class StatusBackgroundConverter : IValueConverter
{
    private static Brush Lookup(string key) =>
        (Application.Current.TryFindResource(key) as Brush) ?? Brushes.Transparent;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        AppointmentStatus.Scheduled or InvoiceStatus.Unpaid or VisitStatus.Open or PatientStatus.Active => Lookup("Brush.InfoSubtle"),
        AppointmentStatus.Waiting or TreatmentPlanItemStatus.Planned => Lookup("Brush.WarningSubtle"),
        AppointmentStatus.InConsultation or InvoiceStatus.PartiallyPaid => Lookup("Brush.AccentSubtle"),
        AppointmentStatus.Completed or InvoiceStatus.Paid or VisitStatus.Completed => Lookup("Brush.SuccessSubtle"),
        AppointmentStatus.Cancelled or InvoiceStatus.Voided or VisitStatus.Cancelled
            or AppointmentStatus.NoShow or PatientStatus.Archived => Lookup("Brush.DangerSubtle"),
        _ => Lookup("Brush.SurfaceSunken"),
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class StatusForegroundConverter : IValueConverter
{
    private static Brush Lookup(string key) =>
        (Application.Current.TryFindResource(key) as Brush) ?? Brushes.Gray;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        AppointmentStatus.Scheduled or InvoiceStatus.Unpaid or VisitStatus.Open or PatientStatus.Active => Lookup("Brush.Info"),
        AppointmentStatus.Waiting or TreatmentPlanItemStatus.Planned => Lookup("Brush.Warning"),
        AppointmentStatus.InConsultation or InvoiceStatus.PartiallyPaid => Lookup("Brush.Accent"),
        AppointmentStatus.Completed or InvoiceStatus.Paid or VisitStatus.Completed => Lookup("Brush.Success"),
        AppointmentStatus.Cancelled or InvoiceStatus.Voided or VisitStatus.Cancelled
            or AppointmentStatus.NoShow or PatientStatus.Archived => Lookup("Brush.Danger"),
        _ => Lookup("Brush.InkTertiary"),
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class GenderLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        Gender.Female => LocalizationManager.T("gender.female"),
        Gender.Male => LocalizationManager.T("gender.male"),
        Gender.Other => LocalizationManager.T("gender.other"),
        _ => LocalizationManager.T("gender.unspecified"),
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class NullableDateConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not DateOnly d)
        {
            return "—";
        }

        var format = AppServices.GetService<SettingsStore>()?.Settings.Localization.DateFormat ?? "dd MMM yyyy";
        return d.ToString(format);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class DateTimeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not DateTime dt)
        {
            return string.Empty;
        }

        var settings = AppServices.GetService<SettingsStore>()?.Settings;
        var format = (settings?.Localization.DateFormat ?? "dd MMM yyyy") + " " + (settings?.Localization.TimeFormat ?? "HH:mm");
        return dt.ToString(format);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        string.IsNullOrWhiteSpace(value as string) || (value as string) == "\u2014"
            ? Visibility.Collapsed
            : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

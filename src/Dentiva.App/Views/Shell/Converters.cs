using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Dentiva.App.Views.Shell;

public sealed class SearchKindLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value switch
    {
        "patient" => "Patient",
        "visit" => "Visit",
        "invoice" => "Invoice",
        "payment" => "Receipt",
        "appointment" => "Appointment",
        "medication" => "Medication",
        "referral" => "Referral",
        "attachment" => "Document",
        "staff" => "Staff",
        _ => "Result",
    };

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class NullToCollapsedConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

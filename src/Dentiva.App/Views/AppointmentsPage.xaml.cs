using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Dentiva.App.Services;
using Dentiva.Core.Domain;
using Dentiva.Infrastructure.Repositories;

namespace Dentiva.App.Views;

public partial class AppointmentsPage : UserControl
{
    private readonly AppointmentRepository _appointments;
    private readonly ToastService _toasts;

    public AppointmentsPage()
    {
        InitializeComponent();
        _appointments = AppServices.Get<AppointmentRepository>();
        _toasts = AppServices.Get<ToastService>();
        DayPicker.SelectedDate = DateTime.Today;
        Loaded += async (_, _) => await LoadAsync();
    }

    private DateOnly Day => DayPicker.SelectedDate is { } date
        ? DateOnly.FromDateTime(date)
        : DateOnly.FromDateTime(DateTime.Today);

    private async Task LoadAsync()
    {
        var day = Day;
        SubtitleLine.Text = day == DateOnly.FromDateTime(DateTime.Today)
            ? "Today's queue in serial order"
            : $"Queue for {day.ToString("dddd, dd MMM yyyy")}";

        try
        {
            var items = await Task.Run(() => _appointments.DayQueue(day));
            QueueGrid.ItemsSource = items;

            var summary = _appointments.DaySummary(day);
            BuildSummaryChips(summary);
        }
        catch (Exception ex)
        {
            Log.Error("Appointment queue failed", ex);
            _toasts.Error("Appointments", "The queue could not be loaded. Please try again.");
        }
    }

    private void BuildSummaryChips(DayQueueSummary summary)
    {
        SummaryChips.Children.Clear();
        AddChip($"{summary.Total} scheduled", "Brush.InfoSubtle", "Brush.Info");
        AddChip($"{summary.Waiting ?? 0} waiting", "Brush.WarningSubtle", "Brush.Warning");
        AddChip($"{summary.InConsultation ?? 0} in consultation", "Brush.AccentSubtle", "Brush.Accent");
        AddChip($"{summary.Completed ?? 0} completed", "Brush.SuccessSubtle", "Brush.Success");
        if ((summary.Cancelled ?? 0) + (summary.NoShow ?? 0) > 0)
        {
            AddChip($"{(summary.Cancelled ?? 0) + (summary.NoShow ?? 0)} cancelled / no-show", "Brush.DangerSubtle", "Brush.Danger");
        }
    }

    private void AddChip(string text, string backgroundKey, string foregroundKey)
    {
        var border = new Border
        {
            Background = (Brush?)FindResource(backgroundKey) ?? Brushes.Transparent,
            CornerRadius = new CornerRadius(999),
            Padding = new Thickness(12, 5, 12, 5),
            Margin = new Thickness(8, 0, 0, 0),
        };
        border.SetResourceReference(Border.BackgroundProperty, backgroundKey);
        var label = new TextBlock
        {
            Text = text,
            FontSize = 11.5,
        };
        label.SetResourceReference(FontFamilyProperty, "Font.Medium");
        label.SetResourceReference(TextBlock.ForegroundProperty, foregroundKey);
        border.Child = label;
        SummaryChips.Children.Add(border);
    }

    private async void OnDayChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded)
        {
            await LoadAsync();
        }
    }

    private async void OnPrevDay(object sender, RoutedEventArgs e)
    {
        DayPicker.SelectedDate = Day.ToDateTime(TimeOnly.MinValue).AddDays(-1);
    }

    private async void OnNextDay(object sender, RoutedEventArgs e)
    {
        DayPicker.SelectedDate = Day.ToDateTime(TimeOnly.MinValue).AddDays(1);
    }

    private async void OnToday(object sender, RoutedEventArgs e)
    {
        DayPicker.SelectedDate = DateTime.Today;
    }

    private void OnNewAppointment(object sender, RoutedEventArgs e)
    {
        var dialog = new Dialogs.NewAppointmentDialog(Day) { Owner = Window.GetWindow(this) };
        dialog.ShowDialog();
        _ = LoadAsync();
    }

    private void OnRowDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (QueueGrid.SelectedItem is AppointmentListItem item)
        {
            AppServices.Get<NavigationService>().NavigateTo("patient-profile", () => new PatientProfilePage(item.PatientId));
        }
    }

    private async void OnMarkWaiting(object sender, RoutedEventArgs e) => await SetStatus(sender, AppointmentStatus.Waiting);

    private async void OnStartConsultation(object sender, RoutedEventArgs e) => await SetStatus(sender, AppointmentStatus.InConsultation);

    private async void OnComplete(object sender, RoutedEventArgs e) => await SetStatus(sender, AppointmentStatus.Completed);

    private async Task SetStatus(object sender, AppointmentStatus status)
    {
        if ((sender as FrameworkElement)?.Tag is not long id)
        {
            return;
        }

        try
        {
            await Task.Run(() => _appointments.UpdateStatus(id, status));
            await LoadAsync();
        }
        catch (Exception ex)
        {
            Log.Error("Status update failed", ex);
            _toasts.Error("Appointments", "The appointment status could not be updated. Please try again.");
        }
    }

    private async void OnCancelAppointment(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not long id)
        {
            return;
        }

        var confirm = ConfirmDialog.Show(
            "Cancel appointment",
            "The appointment will be marked as cancelled. The serial number stays recorded for the day's history.",
            "Cancel appointment", danger: true, owner: Window.GetWindow(this));

        if (!confirm)
        {
            return;
        }

        try
        {
            await Task.Run(() => _appointments.UpdateStatus(id, AppointmentStatus.Cancelled));
            await LoadAsync();
            _toasts.Info("Appointment cancelled");
        }
        catch (Exception ex)
        {
            Log.Error("Cancellation failed", ex);
            _toasts.Error("Appointments", "The appointment could not be cancelled. Please try again.");
        }
    }
}

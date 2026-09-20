using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Dentiva.App.Infrastructure.Common;
using Dentiva.App.Services;
using Dentiva.Core.Time;
using Dentiva.Infrastructure.Repositories;

namespace Dentiva.App.Views;

public partial class DashboardPage : UserControl
{
    private readonly AppointmentRepository _appointments;
    private readonly PatientRepository _patients;
    private readonly VisitRepository _visits;
    private readonly NotificationRepository _notifications;
    private readonly DispatcherTimer _clock;
    private readonly ToastService _toast;
    private List<long> _recentPatientIds = new();

    public DashboardPage()
    {
        InitializeComponent();

        _appointments = AppServices.Get<AppointmentRepository>();
        _patients = AppServices.Get<PatientRepository>();
        _visits = AppServices.Get<VisitRepository>();
        _notifications = AppServices.Get<NotificationRepository>();
        _toast = AppServices.Get<ToastService>();

        _clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _clock.Tick += (_, _) => RefreshHeader();
        _clock.Start();

        Loaded += async (_, _) => await LoadAsync();
        Unloaded += (_, _) => _clock.Stop();
    }

    private void RefreshHeader()
    {
        var now = DateTime.Now;
        var settings = AppServices.Get<SettingsStore>().Settings;
        var session = AppServices.Get<SessionManager>().Current;

        var hour = now.Hour;
        var greeting = hour < 12 ? "Good morning" : hour < 17 ? "Good afternoon" : "Good evening";
        var name = string.IsNullOrWhiteSpace(settings.Clinic.DoctorName) ? string.Empty : ", " + settings.Clinic.DoctorName;
        GreetingText.Text = $"{greeting}{name}";
        DateLine.Text = now.ToString("dddd, " + settings.Localization.DateFormat) + "  ·  " + now.ToString(settings.Localization.TimeFormat);
    }

    private async Task LoadAsync()
    {
        RefreshHeader();

        var today = DateOnly.FromDateTime(DateTime.Today);

        try
        {
            var queueTask = Task.Run(() => _appointments.DayQueue(today));
            var summaryTask = Task.Run(() => _appointments.DaySummary(today));
            var outstandingTask = Task.Run(() => _patients.TotalOutstanding());
            var followUpsTask = Task.Run(() => _visits.FollowUpsDue(today, 7));
            var recentTask = Task.Run(() => _patients.RecentPatients(6, today));
            var remindersTask = Task.Run(() => _notifications.Active(5));

            await Task.WhenAll(queueTask, summaryTask, outstandingTask, followUpsTask, recentTask, remindersTask);

            var settings = AppServices.Get<SettingsStore>().Settings;
            var symbol = settings.Clinic.CurrencySymbol;

            var summary = summaryTask.Result;
            QueueGrid.ItemsSource = queueTask.Result;
            QueueSummary.Text = summary.Total == 0
                ? "No appointments scheduled for today."
                : $"{summary.Total} scheduled  ·  {summary.Waiting ?? 0} waiting  ·  {summary.Completed ?? 0} completed";

            StatAppointments.Text = summary.Total.ToString();
            StatWaiting.Text = (summary.Waiting ?? 0).ToString();
            StatOutstanding.Text = symbol + Dentiva.Core.MoneyMath.ToMajor(outstandingTask.Result).ToString("N0");

            var followUps = followUpsTask.Result;
            FollowUps.ItemsSource = followUps;
            FollowUpCount.Text = followUps.Count.ToString();

            var recent = recentTask.Result;
            _recentPatientIds = recent.Select(p => p.Id).ToList();
            RecentPatients.ItemsSource = recent;

            Reminders.ItemsSource = remindersTask.Result;
        }
        catch (Exception ex)
        {
            Log.Error("Dashboard load failed", ex);
            _toast.Error("Dashboard", "Some information could not be loaded. Please reopen the dashboard.");
        }
    }

    private void OnNewPatient(object sender, RoutedEventArgs e)
    {
        var dialog = new Dialogs.NewPatientDialog { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true && dialog.CreatedPatientId is long id)
        {
            AppServices.Get<NavigationService>().NavigateTo("patient-profile", () => new PatientProfilePage(id));
        }
    }

    private void OnNewAppointment(object sender, RoutedEventArgs e)
    {
        var dialog = new Dialogs.NewAppointmentDialog { Owner = Window.GetWindow(this) };
        dialog.ShowDialog();
        _ = LoadAsync();
    }

    private void OnOpenSchedule(object sender, RoutedEventArgs e)
    {
        AppServices.Get<NavigationService>().NavigateTo("appointments");
    }

    private void OnOpenPatient(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is long id)
        {
            AppServices.Get<NavigationService>().NavigateTo("patient-profile", () => new PatientProfilePage(id));
        }
    }
}

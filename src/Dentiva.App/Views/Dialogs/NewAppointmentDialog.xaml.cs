using System.Windows;
using System.Windows.Controls;
using Dentiva.App.Services;
using Dentiva.Core.Domain;
using Dentiva.Infrastructure.Repositories;

namespace Dentiva.App.Views.Dialogs;

public partial class NewAppointmentDialog : Window
{
    private readonly DateOnly _defaultDay;
    private long? _patientId;
    private DispatcherTimer? _debounce;

    public NewAppointmentDialog(DateOnly defaultDay)
    {
        InitializeComponent();

        _defaultDay = defaultDay;
        DateBox.SelectedDate = defaultDay.ToDateTime(TimeOnly.MinValue);

        var settings = AppServices.Get<SettingsStore>().Settings;
        var start = settings.Appointments.WorkdayStart;
        var end = settings.Appointments.WorkdayEnd;
        for (var t = start; t <= end; t = t.AddMinutes(15))
        {
            TimeBox.Items.Add(t.ToString("HH:mm"));
        }

        var now = TimeOnly.FromDateTime(DateTime.Now);
        var nearest = new TimeOnly(now.Hour, now.Minute / 15 * 15 + 15);
        TimeBox.SelectedItem = nearest.ToString("HH:mm");

        foreach (var type in settings.Appointments.AppointmentTypes)
        {
            TypeBox.Items.Add(type);
        }

        TypeBox.SelectedItem = settings.Appointments.DefaultAppointmentType;
        DoctorBox.Text = settings.Clinic.DoctorName;

        Loaded += (_, _) => PatientBox.Focus();
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        };
    }

    public void PreselectPatient(Patient patient)
    {
        _patientId = patient.Id;
        PatientBox.Text = $"{patient.FullName}  ({patient.Code})";
    }

    private void OnPatientSearch(object sender, TextChangedEventArgs e)
    {
        _patientId = null;
        _debounce ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _debounce.Stop();
        _debounce.Tick += async (_, _) =>
        {
            _debounce!.Stop();
            await SearchPatients();
        };
        _debounce.Start();
    }

    private async Task SearchPatients()
    {
        var term = PatientBox.Text.Trim();
        if (term.Length < 1)
        {
            PatientResults.Visibility = Visibility.Collapsed;
            return;
        }

        var patients = AppServices.Get<PatientRepository>();
        var results = await Task.Run(() => patients.Search(new PatientQuery
        {
            SearchText = term,
            PageSize = 12,
            Today = DateOnly.FromDateTime(DateTime.Today),
        }));

        PatientResults.ItemsSource = results.Items;
        PatientResults.Visibility = results.Items.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnPatientSelected(object sender, SelectionChangedEventArgs e)
    {
        if (PatientResults.SelectedItem is Patient patient)
        {
            _patientId = patient.Id;
            PatientBox.Text = $"{patient.FullName}  ({patient.Code})";
            PatientResults.Visibility = Visibility.Collapsed;
            TimeBox.Focus();
        }
    }

    private void OnCancel(object sender, RoutedEventArgs e) => Close();

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        ErrorText.Visibility = Visibility.Collapsed;

        if (_patientId is null)
        {
            ShowError("Select a patient from the search results.");
            return;
        }

        if (DateBox.SelectedDate is null)
        {
            ShowError("Choose the appointment date.");
            return;
        }

        if (TimeBox.SelectedItem is not string timeText || !TimeOnly.TryParse(timeText, out var time))
        {
            ShowError("Choose the appointment time.");
            return;
        }

        SaveButton.IsEnabled = false;
        try
        {
            var appointments = AppServices.Get<AppointmentRepository>();
            var session = AppServices.Get<SessionManager>();
            var settings = AppServices.Get<SettingsStore>().Settings;

            var appointment = new Appointment
            {
                PatientId = _patientId.Value,
                AppointmentDate = DateOnly.FromDateTime(DateBox.SelectedDate!.Value),
                StartTime = time,
                DurationMinutes = settings.Appointments.DefaultDurationMinutes,
                AppointmentType = TypeBox.SelectedItem?.ToString() ?? string.Empty,
                DoctorName = DoctorBox.Text.Trim(),
                IsWalkIn = WalkInCheck.IsChecked == true,
                Notes = NotesBox.Text.Trim(),
                CreatedBy = session.Current?.Username ?? "system",
            };

            var created = await Task.Run(() => appointments.Insert(appointment, settings.Appointments.SerialPerDoctor, session.Current ?? SessionContext.System));
            AppServices.Get<AuditRepository>().RecordStandalone(
                session.Current ?? SessionContext.System, "appointment.created", "appointment", created.Id.ToString());

            AppServices.Get<ToastService>().Success("Appointment booked",
                $"Serial {created.SerialNumber} at {time} on {created.AppointmentDate:dd MMM yyyy}.");
            DialogResult = true;
            Close();
        }
        catch (SerialConflictException)
        {
            ShowError("That serial number is already taken for the selected day. Leave the serial blank to assign the next free number.");
        }
        catch (Exception ex)
        {
            Log.Error("Appointment booking failed", ex);
            ShowError("The appointment could not be saved. Nothing has been changed. Please try again.");
        }
        finally
        {
            SaveButton.IsEnabled = true;
        }
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}

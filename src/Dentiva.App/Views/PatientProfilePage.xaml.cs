using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Dentiva.App.Infrastructure;
using Dentiva.App.Services;
using Dentiva.Core.Domain;
using Dentiva.Infrastructure.Repositories;

namespace Dentiva.App.Views;

public partial class PatientProfilePage : UserControl
{
    private readonly long _patientId;
    private Patient? _patient;

    private readonly PatientRepository _patients;
    private readonly VisitRepository _visits;
    private readonly AppointmentRepository _appointments;
    private readonly BillingRepository _billing;
    private readonly ToastService _toasts;

    public PatientProfilePage(long patientId)
    {
        InitializeComponent();
        _patientId = patientId;

        _patients = AppServices.Get<PatientRepository>();
        _visits = AppServices.Get<VisitRepository>();
        _appointments = AppServices.Get<AppointmentRepository>();
        _billing = AppServices.Get<BillingRepository>();
        _toasts = AppServices.Get<ToastService>();

        Loaded += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            _patient = await Task.Run(() => _patients.GetById(_patientId));
            if (_patient is null)
            {
                _toasts.Error("Patients", "This patient record could not be found.");
                AppServices.Get<NavigationService>().NavigateTo("patients");
                return;
            }

            Initials.Text = new InitialsConverter().Convert(_patient.FullName, null!, null!, null!).ToString();
            NameText.Text = _patient.FullName;
            CodeText.Text = _patient.Code;
            PhoneText.Text = _patient.Phone;

            var meta = new List<string>();
            if (_patient.AgeLabel != "—")
            {
                meta.Add(_patient.AgeLabel);
            }

            meta.Add(new GenderLabelConverter().Convert(_patient.Gender, null!, null!, null!).ToString()!);
            meta.Add("Registered " + _patient.RegistrationDate.ToString("dd MMM yyyy"));
            MetaText.Text = string.Join("   ·   ", meta);

            BalanceText.Text = new MoneyConverter().Convert(_patient.OutstandingMinor, null!, null!, null!).ToString();

            ClinicalSummary.Text = BuildClinicalSummary(_patient);
            AlertsText.Text = BuildAlerts(_patient);

            var visitsTask = Task.Run(() => _visits.Query(new VisitQuery { PatientId = _patientId, Limit = 100 }));
            var apptsTask = Task.Run(() => _appointments.Query(new AppointmentQuery { PatientId = _patientId, Limit = 100 }));
            var invoicesTask = Task.Run(() => _billing.QueryInvoices(new InvoiceQuery { PatientId = _patientId, Limit = 100 }));
            var medsTask = Task.Run(() => _visits.GetMedications(_patientId));
            var referralsTask = Task.Run(() => _visits.GetReferrals(_patientId));
            var teethTask = Task.Run(() => _visits.GetToothRecords(_patientId));

            await Task.WhenAll(visitsTask, apptsTask, invoicesTask, medsTask, referralsTask, teethTask);

            VisitsList.ItemsSource = visitsTask.Result;
            AppointmentsList.ItemsSource = apptsTask.Result;
            InvoicesList.ItemsSource = invoicesTask.Result;
            MedicationsList.ItemsSource = medsTask.Result;
            ReferralsList.ItemsSource = referralsTask.Result;

            ToothChartHost.Content = new ToothChartControl(teethTask.Result, _patientId);
        }
        catch (Exception ex)
        {
            Log.Error("Patient profile failed", ex);
            _toasts.Error("Patients", "The patient profile could not be loaded. Please try again.");
        }
    }

    private static string BuildClinicalSummary(Patient patient)
    {
        var lines = new List<string>();
        void Add(string label, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                lines.Add($"{label}: {value}");
            }
        }

        Add("Chief complaint", patient.ChiefComplaint);
        Add("Medical history", patient.MedicalHistory);
        Add("Dental history", patient.DentalHistory);
        Add("Current medications", patient.CurrentMedications);
        Add("Notes", patient.Notes);

        return lines.Count == 0
            ? "No clinical summary has been recorded for this patient yet."
            : string.Join("\n", lines);
    }

    private static string BuildAlerts(Patient patient)
    {
        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(patient.Allergies))
        {
            lines.Add("Allergies: " + patient.Allergies);
        }

        if (!string.IsNullOrWhiteSpace(patient.RiskFactors))
        {
            lines.Add("Risk factors: " + patient.RiskFactors);
        }

        return lines.Count == 0
            ? "No allergies or risk factors on record."
            : string.Join("\n", lines);
    }

    private async void OnNewVisit(object sender, RoutedEventArgs e)
    {
        if (_patient is null)
        {
            return;
        }

        var dialog = new Dialogs.NewVisitDialog(_patient) { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true)
        {
            await LoadAsync();
        }
    }

    private void OnNewAppointment(object sender, RoutedEventArgs e)
    {
        var dialog = new Dialogs.NewAppointmentDialog(DateOnly.FromDateTime(DateTime.Today)) { Owner = Window.GetWindow(this) };
        if (_patient is not null)
        {
            dialog.PreselectPatient(_patient);
        }

        dialog.ShowDialog();
    }

    private void OnOpenInvoice(object sender, MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is long invoiceId)
        {
            var dialog = new Dialogs.InvoiceDialog(invoiceId) { Owner = Window.GetWindow(this) };
            dialog.ShowDialog();
            _ = LoadAsync();
        }
    }
}

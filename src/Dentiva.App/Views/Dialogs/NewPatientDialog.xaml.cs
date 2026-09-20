using System.Windows;
using Dentiva.App.Services;
using Dentiva.Core.Domain;
using Dentiva.Infrastructure.Repositories;

namespace Dentiva.App.Views.Dialogs;

public partial class NewPatientDialog : Window
{
    public long? CreatedPatientId { get; private set; }

    public NewPatientDialog()
    {
        InitializeComponent();
        Loaded += (_, _) => NameBox.Focus();
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        };
    }

    private void OnCancel(object sender, RoutedEventArgs e) => Close();

    private void OnSave(object sender, RoutedEventArgs e)
    {
        ErrorText.Visibility = Visibility.Collapsed;

        var name = NameBox.Text.Trim();
        var phone = PhoneBox.Text.Trim();

        if (name.Length < 2)
        {
            ShowError("Please enter the patient's full name.");
            return;
        }

        if (phone.Length == 0)
        {
            ShowError("A phone number is required so the clinic can reach the patient.");
            return;
        }

        if (DobPicker.SelectedDate is { } dob && dob > DateTime.Today)
        {
            ShowError("The date of birth cannot be in the future.");
            return;
        }

        SaveButton.IsEnabled = false;
        try
        {
            var patients = AppServices.Get<PatientRepository>();
            var session = AppServices.Get<SessionManager>();
            var settings = AppServices.Get<SettingsStore>().Settings;
            var today = DateOnly.FromDateTime(DateTime.Today);

            var code = patients.GenerateCode(settings, today);
            var patient = new Patient
            {
                FullName = name,
                Phone = phone,
                Gender = (Gender)GenderBox.SelectedIndex,
                DateOfBirth = DobPicker.SelectedDate is { } date ? DateOnly.FromDateTime(date) : null,
                City = CityBox.Text.Trim(),
                ChiefComplaint = ComplaintBox.Text.Trim(),
                RegistrationDate = today,
            };

            var created = patients.Insert(patient, code, session.Current ?? SessionContext.System);
            AppServices.Get<AuditRepository>().RecordStandalone(
                session.Current ?? SessionContext.System, "patient.created", "patient", created.Id.ToString(), name);

            CreatedPatientId = created.Id;
            AppServices.Get<ToastService>().Success("Patient added", $"{name} was registered as {code}.");
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            Log.Error("Patient creation failed", ex);
            ShowError("The patient could not be saved. Your information has not been changed. Please try again.");
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

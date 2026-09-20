using System.Globalization;
using System.Windows;
using Dentiva.App.Services;
using Dentiva.Core.Domain;
using Dentiva.Core.Teeth;
using Dentiva.Infrastructure.Repositories;

namespace Dentiva.App.Views.Dialogs;

public partial class NewVisitDialog : Window
{
    private readonly Patient _patient;

    public NewVisitDialog(Patient patient)
    {
        InitializeComponent();

        _patient = patient;
        PatientLine.Text = $"{patient.FullName}  ·  {patient.Code}";
        DoctorBox.Text = AppServices.Get<SettingsStore>().Settings.Clinic.DoctorName;

        Loaded += (_, _) => ReasonBox.Focus();
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        };
    }

    private void OnCancel(object sender, RoutedEventArgs e) => Close();

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        ErrorText.Visibility = Visibility.Collapsed;

        var reason = ReasonBox.Text.Trim();
        if (reason.Length == 0)
        {
            ShowError("Please describe the reason for this visit.");
            return;
        }

        var tooth = ToothBox.Text.Trim();
        if (tooth.Length > 0 && !ToothCatalog.IsValid(tooth))
        {
            ShowError($"'{tooth}' is not a valid FDI tooth number (for example 16 or 36).");
            return;
        }

        long feeMinor = 0;
        if (!string.IsNullOrWhiteSpace(FeeBox.Text))
        {
            if (!decimal.TryParse(FeeBox.Text.Trim(), NumberStyles.Number, CultureInfo.CurrentCulture, out var fee) || fee < 0)
            {
                ShowError("The procedure fee must be a positive number.");
                return;
            }

            feeMinor = Dentiva.Core.MoneyMath.FromMajor(fee);
        }

        SaveButton.IsEnabled = false;
        try
        {
            var visits = AppServices.Get<VisitRepository>();
            var session = AppServices.Get<SessionManager>();

            var visit = new Visit
            {
                PatientId = _patient.Id,
                VisitDate = DateTime.Now,
                Reason = reason,
                ChiefComplaint = ComplaintBox.Text.Trim(),
                Diagnosis = DiagnosisBox.Text.Trim(),
                ExaminationFindings = ExamBox.Text.Trim(),
                TreatmentNotes = TreatmentBox.Text.Trim(),
                DoctorName = DoctorBox.Text.Trim(),
                FollowUpDate = FollowUpPicker.SelectedDate is { } d ? DateOnly.FromDateTime(d) : null,
            };

            var created = await Task.Run(() => visits.Insert(visit, session.Current ?? SessionContext.System));

            if (!string.IsNullOrWhiteSpace(ProcedureBox.Text))
            {
                visits.ReplaceProcedures(created.Id, new[]
                {
                    new VisitProcedure
                    {
                        VisitId = created.Id,
                        Name = ProcedureBox.Text.Trim(),
                        Tooth = tooth,
                        CostMinor = feeMinor,
                    },
                });
            }

            if (!string.IsNullOrWhiteSpace(tooth))
            {
                visits.UpsertToothRecord(new ToothRecord
                {
                    PatientId = _patient.Id,
                    ToothNumber = tooth,
                    Condition = ToothCondition.Other,
                    Notes = reason,
                });
            }

            var medication = MedicationBox.Text.Trim();
            if (medication.Length > 0)
            {
                visits.InsertMedication(new MedicationOrder
                {
                    PatientId = _patient.Id,
                    VisitId = created.Id,
                    Name = medication,
                    PrescribedDate = DateOnly.FromDateTime(DateTime.Today),
                });
            }

            AppServices.Get<AuditRepository>().RecordStandalone(
                session.Current ?? SessionContext.System, "visit.created", "visit", created.Id.ToString(), _patient.Code);

            AppServices.Get<ToastService>().Success("Visit saved", "The clinical record was added to the patient history.");
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            Log.Error("Visit save failed", ex);
            ShowError("The visit could not be saved. Nothing has been changed. Please try again.");
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

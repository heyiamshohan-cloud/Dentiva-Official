using System.IO;
using System.Windows;
using System.Windows.Controls;
using Dentiva.App.Services;
using Dentiva.Core.Localization;
using Dentiva.Core.Security;
using Dentiva.Core.Settings;
using Microsoft.Win32;

namespace Dentiva.App.Views;

/// <summary>
/// First-run setup: language, clinic profile, formats, schedule, security and
/// backup preference. Every field can be changed later in Settings; only the
/// administrator account is required to proceed safely.
/// </summary>
public partial class SetupWizardWindow : Window
{
    private int _step = 1;

    private static readonly (string Key, string Symbol, string Code)[] Currencies =
    {
        ("currency.bdt", "৳", "BDT"),
        ("currency.usd", "$", "USD"),
        ("currency.eur", "€", "EUR"),
        ("currency.gbp", "£", "GBP"),
        ("currency.inr", "₹", "INR"),
        ("currency.custom", "…", ""),
    };

    private static readonly string[] DateFormats =
    {
        "dd MMM yyyy", "dd/MM/yyyy", "MM/dd/yyyy", "yyyy-MM-dd",
    };

    private static readonly string[] TimeFormats =
    {
        "HH:mm", "hh:mm tt",
    };

    private readonly DayOfWeek[] _week = { DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday };

    public SetupWizardWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        foreach (var (key, _, _) in Currencies)
        {
            CurrencyBox.Items.Add(LocalizationManager.T(key));
        }

        CurrencyBox.SelectedIndex = 0;

        foreach (var format in DateFormats)
        {
            DateFormatBox.Items.Add(DateTime.Today.ToString(format));
        }

        DateFormatBox.SelectedIndex = 0;

        foreach (var format in TimeFormats)
        {
            TimeFormatBox.Items.Add(DateTime.Today.AddHours(14).AddMinutes(30).ToString(format));
        }

        TimeFormatBox.SelectedIndex = 0;

        for (var hour = 6; hour <= 23; hour++)
        {
            OpenHourBox.Items.Add(new TimeOnly(hour, 0).ToString("HH:mm"));
            CloseHourBox.Items.Add(new TimeOnly(hour, 0).ToString("HH:mm"));
        }

        OpenHourBox.SelectedItem = "09:00";
        CloseHourBox.SelectedItem = "18:00";

        foreach (var minutes in new[] { 15, 20, 30, 45, 60 })
        {
            DurationBox.Items.Add(minutes);
        }

        DurationBox.SelectedItem = 30;

        foreach (var day in _week)
        {
            var check = new CheckBox
            {
                Content = day.ToString()[..3],
                Tag = day,
                Margin = new Thickness(0, 0, 10, 0),
                IsChecked = day is DayOfWeek.Saturday or DayOfWeek.Sunday or DayOfWeek.Monday
                    or DayOfWeek.Tuesday or DayOfWeek.Wednesday or DayOfWeek.Thursday,
            };
            WorkingDaysPanel.Children.Add(check);
        }

        BackupFolderBox.Text = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Dentiva Backups");
    }

    private void OnCurrencyChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CustomCurrencyBox is null)
        {
            return;
        }

        var isCustom = CurrencyBox.SelectedIndex == Currencies.Length - 1;
        CustomCurrencyBox.Visibility = isCustom ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnAdminPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (StrengthBar is null || StrengthText is null)
        {
            return;
        }

        var score = PasswordHasher.StrengthScore(AdminPasswordBox.Password);
        StrengthBar.Value = score;
        StrengthText.Text = score switch
        {
            0 or 1 => "Weak — use at least 8 characters with numbers and symbols",
            2 => "Fair — add symbols to make it stronger",
            3 => "Good password",
            _ => "Strong password",
        };
    }

    private void OnChooseBackupFolder(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Choose backup folder",
        };

        if (dialog.ShowDialog(this) == true)
        {
            BackupFolderBox.Text = dialog.FolderName;
        }
    }

    private void OnBack(object sender, RoutedEventArgs e)
    {
        if (_step > 1)
        {
            _step--;
            ShowStep();
        }
    }

    private void OnNext(object sender, RoutedEventArgs e)
    {
        if (!ValidateStep(_step))
        {
            return;
        }

        if (_step == 1)
        {
            var language = LangBengali.IsChecked == true ? "bn" : "en";
            LocalizationManager.Instance.Language = language;
        }

        if (_step < 6)
        {
            _step++;
            ShowStep();
            return;
        }

        Finish();
    }

    private bool ValidateStep(int step)
    {
        SecurityError.Visibility = Visibility.Collapsed;

        switch (step)
        {
            case 2 when string.IsNullOrWhiteSpace(ClinicNameBox.Text)
                        || string.IsNullOrWhiteSpace(DoctorNameBox.Text)
                        || string.IsNullOrWhiteSpace(ClinicPhoneBox.Text):
                MessageBox.Show(this,
                    "Please fill in the clinic name, doctor name and phone number. These appear on your documents.",
                    "Dentiva Setup", MessageBoxButton.OK, MessageBoxImage.Information);
                return false;

            case 5:
                var username = AdminUsernameBox.Text.Trim();
                if (username.Length < 3)
                {
                    ShowSecurityError("Choose a username with at least 3 characters.");
                    return false;
                }

                if (AdminPasswordBox.Password.Length < Math.Max(8, AppServices.Get<SettingsStore>().Settings.Security.MinPasswordLength))
                {
                    ShowSecurityError("The password must be at least 8 characters long.");
                    return false;
                }

                if (!string.Equals(AdminPasswordBox.Password, AdminPassword2Box.Password, StringComparison.Ordinal))
                {
                    ShowSecurityError("The two passwords do not match.");
                    return false;
                }

                break;
        }

        return true;
    }

    private void ShowSecurityError(string message)
    {
        SecurityError.Text = message;
        SecurityError.Visibility = Visibility.Visible;
    }

    private void ShowStep()
    {
        Step1.Visibility = _step == 1 ? Visibility.Visible : Visibility.Collapsed;
        Step2.Visibility = _step == 2 ? Visibility.Visible : Visibility.Collapsed;
        Step3.Visibility = _step == 3 ? Visibility.Visible : Visibility.Collapsed;
        Step4.Visibility = _step == 4 ? Visibility.Visible : Visibility.Collapsed;
        Step5.Visibility = _step == 5 ? Visibility.Visible : Visibility.Collapsed;
        Step6.Visibility = _step == 6 ? Visibility.Visible : Visibility.Collapsed;

        Progress.Value = _step;
        StepLabel.Text = $"Step {_step} of 6";
        BackButton.IsEnabled = _step > 1;
        NextButton.Content = _step == 6 ? LocalizationManager.T("common.finish") : LocalizationManager.T("common.next");
    }

    private void Finish()
    {
        try
        {
            var settingsStore = AppServices.Get<SettingsStore>();
            var users = AppServices.Get<UserRepository>();

            if (users.AnyUserExists())
            {
                // The database already has an administrator; never overwrite it.
                OpenShell();
                return;
            }

            var role = PermissionPresets.Administrator;
            var admin = users.Create(AdminUsernameBox.Text.Trim(), AdminPasswordBox.Password, "Administrator", role)
                ?? throw new InvalidOperationException("Administrator account could not be created.");

            settingsStore.Save(s =>
            {
                s.Localization.Language = LangBengali.IsChecked == true ? "bn" : "en";
                s.Clinic.ClinicName = ClinicNameBox.Text.Trim();
                s.Clinic.DoctorName = DoctorNameBox.Text.Trim();
                s.Clinic.DoctorDegrees = DegreesBox.Text.Trim();
                s.Clinic.Phone = ClinicPhoneBox.Text.Trim();
                s.Clinic.Email = ClinicEmailBox.Text.Trim();
                s.Clinic.Address = ClinicAddressBox.Text.Trim();

                var currencyIndex = CurrencyBox.SelectedIndex;
                if (currencyIndex >= 0 && currencyIndex < Currencies.Length - 1)
                {
                    s.Clinic.CurrencySymbol = Currencies[currencyIndex].Symbol;
                    s.Clinic.CurrencyCode = Currencies[currencyIndex].Code;
                }
                else
                {
                    s.Clinic.CurrencySymbol = string.IsNullOrWhiteSpace(CustomCurrencyBox.Text) ? "৳" : CustomCurrencyBox.Text.Trim();
                    s.Clinic.CurrencyCode = "CUSTOM";
                }

                s.Localization.DateFormat = DateFormats[Math.Max(0, DateFormatBox.SelectedIndex)];
                s.Localization.TimeFormat = TimeFormats[Math.Max(0, TimeFormatBox.SelectedIndex)];

                s.Appointments.WorkdayStart = TimeOnly.Parse(OpenHourBox.SelectedItem?.ToString() ?? "09:00");
                s.Appointments.WorkdayEnd = TimeOnly.Parse(CloseHourBox.SelectedItem?.ToString() ?? "18:00");
                s.Appointments.DefaultDurationMinutes = DurationBox.SelectedItem as int? ?? 30;
                s.Appointments.WorkingDays = WorkingDaysPanel.Children.OfType<CheckBox>()
                    .Where(c => c.IsChecked == true)
                    .Select(c => (int)(DayOfWeek)c.Tag!)
                    .OrderBy(d => d)
                    .ToList();

                s.Backup.AutoBackupEnabled = AutoBackupCheck.IsChecked == true;
                s.Backup.TargetFolder = BackupFolderBox.Text.Trim();
            });

            var sessionManager = AppServices.Get<SessionManager>();
            sessionManager.SignIn(new SessionContext(
                admin.Id, admin.Username, admin.DisplayName, admin.RoleName, PermissionPresets.ForRole(admin.RoleName)));

            var audit = AppServices.Get<Infrastructure.Repositories.AuditRepository>();
            audit.RecordStandalone(sessionManager.Current!, "setup.completed", "settings", "app");

            OpenShell();
        }
        catch (Exception ex)
        {
            Log.Error("Setup wizard failed", ex);
            MessageBox.Show(this,
                "Setup could not be completed. Your information has not been changed — please try again.\n\n" + ex.Message,
                "Dentiva Setup", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OpenShell()
    {
        var shell = new ShellWindow();
        Application.Current.MainWindow = shell;
        shell.Show();
        Close();
    }
}

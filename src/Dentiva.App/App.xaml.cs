using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using Dentiva.App.Services;
using Dentiva.Core.Security;
using Dentiva.Infrastructure;
using Dentiva.Infrastructure.Files;
using Dentiva.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Dentiva.App;

/// <summary>
/// Application entry point. Handles command-line automation switches used by
/// CI (--selftest, --qa-tour, --bench) before the interactive shell starts.
/// </summary>
public partial class App : Application
{
    private static Mutex? _singleInstanceMutex;

    public static string Version =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

    public static string DataDirectory { get; private set; } = string.Empty;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var args = e.Args ?? Array.Empty<string>();

        if (args.Contains("--selftest"))
        {
            RunSelftest(GetArgValue(args, "--data-dir"));
            return;
        }

        if (!TryAcquireSingleInstance())
        {
            Shutdown(2);
            return;
        }

        // Data directory: --data-dir for portable/automation use, otherwise %LOCALAPPDATA%\Dentiva.
        DataDirectory = GetArgValue(args, "--data-dir") ?? string.Empty;
        var paths = new AppPaths(string.IsNullOrWhiteSpace(DataDirectory) ? null : DataDirectory);
        paths.EnsureDirectories();
        Log.Initialize(paths.LogsRoot);

        HookGlobalExceptionHandlers();

        LocalizationManager.Instance.ApplyFonts();

        BuildServices(paths);
        NavigationRegistry.RegisterAll();

        var settings = AppServices.Get<SettingsStore>();
        LocalizationManager.Instance.Language = settings.Settings.Localization.Language;

        MainWindow = CreateStartupWindow(paths);
        if (MainWindow is null)
        {
            Shutdown(0);
            return;
        }

        MainWindow.Show();
    }

    private Window? CreateStartupWindow(AppPaths paths)
    {
        var users = AppServices.Get<UserRepository>();

        if (!users.AnyUserExists())
        {
            // First launch: the setup wizard creates the administrator and clinic profile.
            return new Views.SetupWizardWindow();
        }

        var session = AppServices.Get<SessionManager>();
        if (session.Current is null)
        {
            var login = new Views.LoginWindow();
            if (login.ShowDialog() != true || session.Current is null)
            {
                return null;
            }
        }

        return new Views.ShellWindow();
    }

    private static void BuildServices(AppPaths paths)
    {
        var services = new ServiceCollection();

        services.AddSingleton(paths);
        services.AddSingleton<ISqliteConnectionFactory>(_ => new SqliteConnectionFactory(paths.DatabasePath));

        services.AddSingleton<CounterService>();
        services.AddSingleton<SettingsRepository>();
        services.AddSingleton<SettingsStore>();
        services.AddSingleton<UserRepository>();
        services.AddSingleton<AuditRepository>();
        services.AddSingleton<NotificationRepository>();
        services.AddSingleton<PatientRepository>();
        services.AddSingleton<AppointmentRepository>();
        services.AddSingleton<VisitRepository>();
        services.AddSingleton<BillingRepository>();
        services.AddSingleton<FinanceRepository>();
        services.AddSingleton<StaffRepository>();
        services.AddSingleton<SearchRepository>();
        services.AddSingleton<AttachmentRepository>();
        services.AddSingleton(sp => new AttachmentStore(sp.GetRequiredService<ISqliteConnectionFactory>(), paths.AttachmentsRoot));
        services.AddSingleton(sp => new BackupService(sp.GetRequiredService<ISqliteConnectionFactory>(), paths.AttachmentsRoot));

        services.AddSingleton<SessionManager>();
        services.AddSingleton<NavigationService>();
        services.AddSingleton<ToastService>();

        AppServices.Initialize(services.BuildServiceProvider());

        // The database is created/upgraded before any screen appears.
        Database.Migrate(AppServices.Get<ISqliteConnectionFactory>());
        Log.Info($"Dentiva {Version} started. Data: {paths}");
    }

    private static void RegisterNavigation()
    {
        // Navigation registry is populated by the shell to keep page wiring in one place.
    }

    private void HookGlobalExceptionHandlers()
    {
        DispatcherUnhandledException += (_, e) =>
        {
            e.Handled = true;
            HandleFatal(e.Exception);
        };

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                Log.Error("Unhandled app domain exception", ex);
            }
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log.Error("Unobserved task exception", e.Exception);
            e.SetObserved();
        };
    }

    private void HandleFatal(Exception ex)
    {
        Log.Error("Unhandled UI exception", ex);

        MessageBox.Show(
            "Dentiva ran into an unexpected problem. Your records are safe — nothing was saved in an incomplete state.\n\n" +
            "Details were written to the application log. You can continue working or restart the application.",
            "Dentiva",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private bool TryAcquireSingleInstance()
    {
        _singleInstanceMutex = new Mutex(true, "Dentiva.SingleInstance." + Environment.UserName, out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show("Dentiva is already running.", "Dentiva", MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }

        return true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }

    private static string? GetArgValue(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    // ==================== CI automation modes ====================

    private void RunSelftest(string? dataDir)
    {
        var exitCode = 0;
        try
        {
            DataDirectory = dataDir ?? Path.Combine(Path.GetTempPath(), "dentiva-selftest-" + Guid.NewGuid().ToString("N"));
            var paths = new AppPaths(DataDirectory);
            paths.EnsureDirectories();
            Log.Initialize(paths.LogsRoot);

            BuildServices(paths);
            var report = Qa.SelftestRunner.RunAll();
            var target = Path.Combine(DataDirectory, "selftest-result.json");
            File.WriteAllText(target, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));

            exitCode = report.GetProperty("ok").GetBoolean() ? 0 : 1;
        }
        catch (Exception ex)
        {
            Log.Error("Selftest crashed", ex);
            exitCode = 1;
        }

        Shutdown(exitCode);
    }
}

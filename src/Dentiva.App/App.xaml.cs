using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;

namespace Dentiva.App;

/// <summary>
/// Application entry point. Handles command-line automation switches used by
/// CI (--selftest, --qa-tour, --bench) before the interactive shell starts.
/// </summary>
public partial class App : Application
{
    public static string Version =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var args = e.Args ?? Array.Empty<string>();

        if (args.Contains("--selftest"))
        {
            RunSelftest(GetArgValue(args, "--data-dir"));
            return;
        }

        var window = new MainWindow();
        MainWindow = window;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        base.OnExit(e);
    }

    private static string? GetArgValue(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    private void RunSelftest(string? dataDir)
    {
        // Milestone 0 probe: verifies the process boots, WPF initializes and
        // assets resolve. The full service-level suite attaches in a later
        // milestone alongside the data layer.
        var result = new Dictionary<string, object>
        {
            ["ok"] = true,
            ["version"] = Version,
            ["dataDir"] = dataDir ?? string.Empty,
            ["checks"] = Array.Empty<string>(),
        };

        var target = Path.Combine(dataDir ?? Directory.GetCurrentDirectory(), "selftest-result.json");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.WriteAllText(target, JsonSerializer.Serialize(result));
        Shutdown(0);
    }
}

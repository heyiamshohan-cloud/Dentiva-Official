namespace Dentiva.App.Services;

public enum LogChannel
{
    Application,
    Error,
    Security,
}

/// <summary>
/// Structured file logging. Patient data is never written to logs —
/// messages carry identifiers only. Logs live under {data}/logs and rotate
/// by day.
/// </summary>
public static class Log
{
    private static string? _logRoot;

    public static void Initialize(string logRoot)
    {
        _logRoot = logRoot;
        Directory.CreateDirectory(logRoot);
    }

    public static void Info(string message) => Write(LogChannel.Application, "INFO", message);

    public static void Warn(string message) => Write(LogChannel.Application, "WARN", message);

    public static void Error(string message, Exception? ex = null) =>
        Write(LogChannel.Error, "ERROR", ex is null ? message : $"{message}\n{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");

    public static void Security(string message) => Write(LogChannel.Security, "AUDIT", message);

    private static readonly object Gate = new();

    private static void Write(LogChannel channel, string level, string message)
    {
        if (_logRoot is null)
        {
            return;
        }

        try
        {
            lock (Gate)
            {
                var file = Path.Combine(_logRoot, $"{channel.ToString().ToLowerInvariant()}-{DateTime.Now:yyyyMMdd}.log");
                File.AppendAllText(file, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}\n");
            }
        }
        catch (IOException)
        {
            // Logging must never take the application down.
        }
    }
}

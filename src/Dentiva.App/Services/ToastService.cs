using System.Collections.ObjectModel;
using System.Windows;
using Dentiva.App.Infrastructure.Common;

namespace Dentiva.App.Services;

public enum ToastKind
{
    Success,
    Info,
    Warning,
    Error,
}

public sealed class Toast : ObservableObject
{
    public ToastKind Kind { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>Transient in-app notifications (no external service involved).</summary>
public sealed class ToastService
{
    public ObservableCollection<Toast> Active { get; } = new();

    public event EventHandler<Toast>? Raised;

    public void Show(ToastKind kind, string title, string message = "")
    {
        var toast = new Toast { Kind = kind, Title = title, Message = message };
        Active.Add(toast);
        Raised?.Invoke(this, toast);

        RemoveAfterDelay(toast, kind == ToastKind.Error ? 7 : 4);
    }

    public void Success(string title, string message = "") => Show(ToastKind.Success, title, message);
    public void Info(string title, string message = "") => Show(ToastKind.Info, title, message);
    public void Warning(string title, string message = "") => Show(ToastKind.Warning, title, message);
    public void Error(string title, string message = "") => Show(ToastKind.Error, title, message);

    public void Dismiss(Toast toast)
    {
        Application.Current?.Dispatcher.Invoke(() => Active.Remove(toast));
    }

    private async void RemoveAfterDelay(Toast toast, int seconds)
    {
        await Task.Delay(TimeSpan.FromSeconds(seconds));
        await Application.Current?.Dispatcher.InvokeAsync(() => Active.Remove(toast))!;
    }
}

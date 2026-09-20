using System.Windows.Threading;
using Dentiva.Core.Domain;

namespace Dentiva.App.Services;

/// <summary>
/// Holds the authenticated session, drives inactivity auto-lock and provides
/// permission checks for the UI layer.
/// </summary>
public sealed class SessionManager
{
    private readonly DispatcherTimer _idleTimer;

    public SessionManager()
    {
        _idleTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _idleTimer.Tick += (_, _) => CheckIdleLock();
    }

    public SessionContext? Current { get; private set; }

    public DateTimeOffset LastActivity { get; private set; } = DateTimeOffset.UtcNow;

    public event EventHandler<SessionContext>? SignedIn;
    public event EventHandler? SignedOut;
    public event EventHandler? LockRequested;

    public void SignIn(SessionContext session)
    {
        Current = session ?? throw new ArgumentNullException(nameof(session));
        LastActivity = DateTimeOffset.UtcNow;
        RestartIdleTimer();
        SignedIn?.Invoke(this, session);
    }

    public void SignOut()
    {
        Current = null;
        _idleTimer.Stop();
        SignedOut?.Invoke(this, EventArgs.Empty);
    }

    public void TouchActivity()
    {
        LastActivity = DateTimeOffset.UtcNow;
    }

    public bool HasPermission(AppPermission permission) =>
        Current is null || permission == AppPermission.None || Current.Has(permission);

    public void RestartIdleTimer()
    {
        // Auto-lock is wired in Settings; the timer always runs but only asks
        // for a lock when configured and a session exists.
        _idleTimer.Start();
    }

    private void CheckIdleLock()
    {
        // The settings store is injected lazily to avoid a startup cycle.
        var store = AppServices.GetService<SettingsStore>();
        if (store is null || Current is null || !store.Settings.Security.AutoLockEnabled)
        {
            return;
        }

        if (DateTimeOffset.UtcNow - LastActivity > TimeSpan.FromMinutes(Math.Max(1, store.Settings.Security.AutoLockMinutes)))
        {
            LockRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}

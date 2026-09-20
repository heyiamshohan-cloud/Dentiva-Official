using Dentiva.Core.Settings;
using Dentiva.Infrastructure.Repositories;

namespace Dentiva.App.Services;

/// <summary>Central access to the clinic's settings with explicit save points.</summary>
public sealed class SettingsStore
{
    private readonly SettingsRepository _repository;
    private readonly object _gate = new();

    public SettingsStore(SettingsRepository repository)
    {
        _repository = repository;
        Settings = repository.Load();
        EnsureDefaults();
    }

    public AppSettings Settings { get; private set; }

    public void Reload()
    {
        lock (_gate)
        {
            Settings = _repository.Load();
            EnsureDefaults();
        }
    }

    public void Save()
    {
        lock (_gate)
        {
            Settings.UpdatedAt = DateTimeOffset.UtcNow;
            _repository.Save(Settings);
        }
    }

    public void Save(Action<AppSettings> mutate)
    {
        lock (_gate)
        {
            mutate(Settings);
            Settings.UpdatedAt = DateTimeOffset.UtcNow;
            _repository.Save(Settings);
        }
    }

    private void EnsureDefaults()
    {
        var changed = false;

        if (Settings.Dashboard.Widgets.Count == 0)
        {
            var keys = new[]
            {
                "stats", "queue", "followups", "reminders", "recent",
            };
            for (var i = 0; i < keys.Length; i++)
            {
                Settings.Dashboard.Widgets.Add(new DashboardWidgetState { WidgetKey = keys[i], Visible = true, Order = i });
            }

            changed = true;
        }

        if (changed)
        {
            Save();
        }
    }
}

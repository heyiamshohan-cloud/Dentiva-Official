using System.Windows;
using System.Windows.Controls;
using Dentiva.Core.Security;

namespace Dentiva.App.Services;

public sealed record NavItem(
    string Key,
    string TitleKey,
    string Glyph,
    bool ShowInSidebar,
    int SidebarOrder,
    AppPermission Permission,
    Func<Page> Factory);

/// <summary>
/// Page navigation with a registry, back stack and parameterized factories.
/// Pages are created fresh per navigation so views never carry stale state.
/// </summary>
public sealed class NavigationService
{
    private readonly Dictionary<string, NavItem> _registry = new();
    private readonly Stack<(string Key, Func<Page> Factory)> _back = new();

    public event EventHandler<(string Key, Page Page)>? Navigated;

    public string? CurrentKey { get; private set; }

    public bool CanGoBack => _back.Count > 0;

    public void Register(NavItem item) => _registry[item.Key] = item;

    public IReadOnlyList<NavItem> SidebarItems(SessionManager session)
    {
        return _registry.Values
            .Where(i => i.ShowInSidebar)
            .Where(i => session.HasPermission(i.Permission))
            .OrderBy(i => i.SidebarOrder)
            .ToList();
    }

    public void NavigateTo(string key, Func<Page>? factoryOverride = null, bool recordHistory = true)
    {
        if (!_registry.TryGetValue(key, out var item))
        {
            throw new InvalidOperationException($"Navigation target '{key}' is not registered.");
        }

        var factory = factoryOverride ?? item.Factory;
        var page = factory();

        if (recordHistory && CurrentKey is not null)
        {
            var current = CurrentKey;
            var currentFactory = _lastFactory;
            _back.Push((current, currentFactory));
        }

        CurrentKey = key;
        _lastFactory = factory;
        Navigated?.Invoke(this, (key, page));
    }

    public bool GoBack()
    {
        if (_back.Count == 0)
        {
            return false;
        }

        var (key, factory) = _back.Pop();
        var page = factory();

        CurrentKey = key;
        _lastFactory = factory;
        Navigated?.Invoke(this, (key, page));
        return true;
    }

    public void ClearHistory() => _back.Clear();

    private Func<Page> _lastFactory = null!;
}

using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Dentiva.App.Infrastructure;
using Dentiva.App.Infrastructure.Common;
using Dentiva.App.Services;
using Dentiva.Infrastructure.Repositories;

namespace Dentiva.App.Views;

public partial class ShellWindow : Window
{
    private readonly NavigationService _navigation;
    private readonly SessionManager _session;
    private readonly ToastService _toasts;
    private readonly NotificationRepository _notifications;
    private readonly DispatcherTimer _searchDebounce = new() { Interval = TimeSpan.FromMilliseconds(160) };
    private readonly DispatcherTimer _notificationTimer = new() { Interval = TimeSpan.FromSeconds(45) };
    private bool _suppressSearchPopup;

    public ShellWindow()
    {
        InitializeComponent();

        _navigation = AppServices.Get<NavigationService>();
        _session = AppServices.Get<SessionManager>();
        _toasts = AppServices.Get<ToastService>();
        _notifications = AppServices.Get<NotificationRepository>();

        _searchDebounce.Tick += (_, _) => RunGlobalSearch();

        _notificationTimer.Tick += (_, _) => RefreshNotificationBadge();
        _notificationTimer.Start();

        Loaded += OnLoaded;
        Closing += (_, _) =>
        {
            _searchDebounce.Stop();
            _notificationTimer.Stop();
        };

        BuildSideNavigation();
        BindSession();
        BindToasts();

        _navigation.Navigated += OnNavigated;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RefreshNotificationBadge();
        _navigation.NavigateTo(NavigationRegistry.DefaultKey, recordHistory: false);
        GlobalSearchBox.Focus();
    }

    private void BindSession()
    {
        var session = _session.Current;
        if (session is null)
        {
            return;
        }

        UserDisplayName.Text = session.DisplayName;
        UserRole.Text = session.RoleName;
        UserInitials.Text = string.IsNullOrWhiteSpace(session.DisplayName)
            ? "U"
            : session.DisplayName.Trim()[..1].ToUpperInvariant();

        var lockWatch = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        lockWatch.Tick += (_, _) =>
        {
            var store = AppServices.GetService<SettingsStore>();
            if (store is not null
                && store.Settings.Security.AutoLockEnabled
                && DateTimeOffset.UtcNow - _session.LastActivity > TimeSpan.FromMinutes(Math.Max(1, store.Settings.Security.AutoLockMinutes)))
            {
                LockNow();
            }
        };
        lockWatch.Start();

        MouseMove += (_, _) => _session.TouchActivity();
        KeyDown += (_, _) => _session.TouchActivity();
    }

    private void BuildSideNavigation()
    {
        NavList.Children.Clear();
        foreach (var item in _navigation.SidebarItems(_session))
        {
            var radio = new RadioButton
            {
                GroupName = "Nav",
                Tag = item.Glyph,
                Content = LocalizationManager.T(item.TitleKey),
                Style = (Style)Resources["NavItemRadio"],
                ToolTip = LocalizationManager.T(item.TitleKey),
            };
            radio.Checked += (_, _) =>
            {
                if (_navigation.CurrentKey != item.Key)
                {
                    _navigation.NavigateTo(item.Key);
                }
            };
            radio.Tag = item.Glyph;
            radio.Content = LocalizationManager.T(item.TitleKey);
            radio.SetValue(FrameworkElement.NameProperty, "Nav_" + item.Key);
            NavList.Children.Add(radio);
            Nav.Register(item.Key, radio);
        }
    }

    private void OnNavigated(object? sender, (string Key, Page Page) e)
    {
        var item = NavigationRegistry.Find(e.Key);
        PageTitle.Text = item is null ? e.Key : LocalizationManager.T(item.TitleKey);

        Nav.Check(e.Key);

        PageHost.Content = e.Page;

        // Gentle 150ms fade keeps navigation calm without slowing work.
        PageHost.Opacity = 0;
        var animation = new System.Windows.Media.Animation.DoubleAnimation(1, TimeSpan.FromMilliseconds(150))
        {
            EasingFunction = new System.Windows.Media.Animation.QuadraticEase(),
        };
        PageHost.BeginAnimation(OpacityProperty, animation);
    }

    // ---------- Global search ----------

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressSearchPopup)
        {
            return;
        }

        _searchDebounce.Stop();
        if (GlobalSearchBox.Text.Trim().Length >= 2)
        {
            _searchDebounce.Start();
        }
        else
        {
            SearchPopup.IsOpen = false;
        }
    }

    private void RunGlobalSearch()
    {
        var query = GlobalSearchBox.Text.Trim();
        if (query.Length < 2)
        {
            return;
        }

        var search = AppServices.Get<SearchRepository>();
        var result = search.Search(query, limit: 25);

        SearchResults.ItemsSource = result.Hits;
        SearchResults.Tag = result;
        SearchPopup.IsOpen = result.Hits.Count > 0;

        if (result.Hits.Count == 0)
        {
            SearchPopup.IsOpen = false;
        }
    }

    private void OnSearchKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (SearchResults.ItemsSource is System.Collections.Generic.IReadOnlyList<GlobalSearchHit> hits
                && hits.Count > 0)
            {
                OpenHit(hits[0]);
                SearchPopup.IsOpen = false;
            }

            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            SearchPopup.IsOpen = false;
            GlobalSearchBox.Text = string.Empty;
        }
    }

    private void OnSearchResultSelected(object sender, SelectionChangedEventArgs e)
    {
        if (SearchResults.SelectedItem is GlobalSearchHit hit)
        {
            OpenHit(hit);
            SearchPopup.IsOpen = false;
        }
    }

    private void OpenHit(GlobalSearchHit hit)
    {
        _suppressSearchPopup = true;
        GlobalSearchBox.Text = string.Empty;
        _suppressSearchPopup = false;
        SearchPopup.IsOpen = false;

        switch (hit.Kind)
        {
            case "patient":
                _navigation.NavigateTo("patient-profile", () => new PatientProfilePage(hit.RefId));
                break;
            case "invoice":
            case "payment":
                _navigation.NavigateTo("billing", () => new BillingPage(hit.PatientId));
                break;
            case "visit":
            case "medication":
            case "referral":
            case "attachment":
                if (hit.PatientId > 0)
                {
                    _navigation.NavigateTo("patient-profile", () => new PatientProfilePage(hit.PatientId));
                }

                break;
            case "appointment":
                _navigation.NavigateTo("appointments");
                break;
            case "staff":
                _navigation.NavigateTo("staff");
                break;
        }
    }

    // ---------- Notifications ----------

    private void RefreshNotificationBadge()
    {
        var unread = _notifications.UnreadCount();
        NotificationBadge.Visibility = unread > 0 ? Visibility.Visible : Visibility.Collapsed;
        NotificationCount.Text = unread > 99 ? "99+" : unread.ToString();
    }

    private void OnNotificationsClick(object sender, RoutedEventArgs e)
    {
        _navigation.NavigateTo("notifications");
    }

    // ---------- Toasts ----------

    private void BindToasts()
    {
        ToastList.ItemsSource = _toasts.Active;
        ToastHost.SetBinding(VisibilityProperty, new System.Windows.Data.Binding("Count")
        {
            Source = _toasts.Active,
            Converter = new CountToVisibilityConverter(),
        });
    }

    private sealed class CountToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is int count && count > 0 ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }

    // ---------- Window controls ----------

    private void OnDragWindow(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed && e.ClickCount == 1)
        {
            try
            {
                DragMove();
            }
            catch (InvalidOperationException)
            {
            }
        }
    }

    private void OnMinimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnMaximize(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private void OnSignOut(object sender, RoutedEventArgs e)
    {
        var confirm = ConfirmDialog.Show(
            "Sign out",
            "You will need to sign in again to access patient records. Unsaved changes in open forms will be discarded.",
            "Sign out");
        if (!confirm)
        {
            return;
        }

        var username = _session.Current?.Username ?? "unknown";
        _session.SignOut();
        AppServices.Get<AuditRepository>().RecordStandalone(
            new Dentiva.Core.Domain.SessionContext(0, username, "", "", Dentiva.Core.Security.AppPermission.None),
            "session.signedOut");

        var login = new LoginWindow();
        Application.Current.MainWindow = login;
        login.Show();
        Close();
    }

    private void LockNow()
    {
        // Auto-lock returns the user to the sign-in screen without losing data:
        // every write completes synchronously before the lock engages.
        _session.SignOut();
        var login = new LoginWindow();
        Application.Current.MainWindow = login;
        login.Show();
        Close();
    }

    // ---------- Dialog hosting ----------

    public static void ShowDialog(FrameworkElement content)
    {
        if (Application.Current.MainWindow is ShellWindow shell)
        {
            shell.DialogContent.Content = content;
            shell.DialogOverlay.Visibility = Visibility.Visible;
        }
    }

    public static void CloseDialog()
    {
        if (Application.Current.MainWindow is ShellWindow shell)
        {
            shell.DialogOverlay.Visibility = Visibility.Collapsed;
            shell.DialogContent.Content = null;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _navigation.Navigated -= OnNavigated;
        base.OnClosed(e);
    }
}

/// <summary>Static map from navigation key to its sidebar radio button.</summary>
internal static class Nav
{
    private static readonly Dictionary<string, RadioButton> Map = new();

    public static void Register(string key, RadioButton button) => Map[key] = button;

    public static void Check(string key)
    {
        if (Map.TryGetValue(key, out var button) && !button.IsChecked.GetValueOrDefault())
        {
            button.IsChecked = true;
        }
    }
}

using System.Windows.Controls;
using Dentiva.App.Views;
using Dentiva.Core.Security;

namespace Dentiva.App.Services;

/// <summary>Central navigation map — one authoritative list of pages.</summary>
public static class NavigationRegistry
{
    public const string DefaultKey = "dashboard";

    private static readonly Dictionary<string, NavItem> Items = new();

    public static NavItem? Find(string key) => Items.TryGetValue(key, out var item) ? item : null;

    public static void RegisterAll()
    {
        var navigation = AppServices.Get<NavigationService>();
        navigation.ClearHistory();

        Register(new NavItem("dashboard", "nav.dashboard", "\uE80F", true, 0, AppPermission.None,
            () => new DashboardPage()));

        if (AppServices.Get<SessionManager>().HasPermission(AppPermission.PatientsView))
        {
            Register(new NavItem("patients", "nav.patients", "\uE77B", true, 1, AppPermission.PatientsView,
                () => new PatientsPage()));
            Register(new NavItem("patient-profile", "nav.patients", "\uE77B", false, 99, AppPermission.PatientsView,
                () => new PatientsPage()));
        }

        if (AppServices.Get<SessionManager>().HasPermission(AppPermission.AppointmentsManage))
        {
            Register(new NavItem("appointments", "nav.appointments", "\uE787", true, 2, AppPermission.AppointmentsManage,
                () => new AppointmentsPage()));
        }

        if (AppServices.Get<SessionManager>().HasPermission(AppPermission.BillingManage))
        {
            Register(new NavItem("billing", "nav.billing", "\uE8C7", true, 3, AppPermission.BillingManage,
                () => new BillingPage()));
        }

        Register(new NavItem("notifications", "nav.notifications", "\uEA8F", true, 8, AppPermission.None,
            () => new NotificationsPage()));

        foreach (var item in Items.Values)
        {
            navigation.Register(item);
        }
    }

    private static void Register(NavItem item) => Items[item.Key] = item;
}

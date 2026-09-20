using System.Windows;
using System.Windows.Controls;
using Dentiva.App.Infrastructure.Common;
using Dentiva.App.Services;
using Dentiva.Core.Domain;
using Dentiva.Infrastructure.Repositories;

namespace Dentiva.App.Views;

/// <summary>View-model row for the notification list.</summary>
public sealed class NotificationRow : ObservableObject
{
    public long Id { get; init; }
    public NotificationKind Kind { get; init; }
    public NotificationPriority Priority { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
    public bool IsRead { get; set; }

    public string PriorityLabel => Priority switch
    {
        NotificationPriority.Critical => "Critical",
        NotificationPriority.Important => "Important",
        _ => "Normal",
    };
}

public partial class NotificationsPage : UserControl
{
    private readonly NotificationRepository _notifications;
    private readonly ToastService _toasts;

    public NotificationsPage()
    {
        InitializeComponent();
        _notifications = AppServices.Get<NotificationRepository>();
        _toasts = AppServices.Get<ToastService>();
        Loaded += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        var items = await Task.Run(() => _notifications.Active(100));
        List.ItemsSource = items.Select(n => new NotificationRow
        {
            Id = n.Id,
            Kind = n.Kind,
            Priority = n.Priority,
            Title = n.Title,
            Message = n.Message,
            CreatedAt = n.CreatedAt,
        }).ToList();

        if (items.Count == 0)
        {
            List.ItemsSource = new[] { new NotificationRow { Id = -1, Title = "You're all caught up", Message = "No active notifications. Reminders about appointments, follow-ups and unpaid balances will appear here." } };
        }
    }

    private async void OnMarkRead(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is long id && id > 0)
        {
            await Task.Run(() => _notifications.MarkRead(id));
            await LoadAsync();
        }
    }

    private async void OnDismiss(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is long id && id > 0)
        {
            await Task.Run(() => _notifications.Dismiss(id));
            await LoadAsync();
            _toasts.Info("Notification dismissed");
        }
    }

    private async void OnMarkAllRead(object sender, RoutedEventArgs e)
    {
        await Task.Run(() => _notifications.MarkAllRead());
        await LoadAsync();
    }
}

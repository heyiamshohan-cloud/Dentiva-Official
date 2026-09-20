using System.Windows;
using System.Windows.Controls;

namespace Dentiva.App.Views;

/// <summary>Modal confirmation with clear consequence messaging.</summary>
public static class ConfirmDialog
{
    public static bool Show(string title, string message, string confirmLabel, bool danger = false, Window? owner = null)
    {
        var result = false;

        var confirmButton = new Button
        {
            Content = confirmLabel,
            Width = 130,
            Height = 36,
        };

        if (danger)
        {
            confirmButton.SetResourceReference(FrameworkElement.StyleProperty, "DangerButton");
        }
        else
        {
            confirmButton.SetResourceReference(FrameworkElement.StyleProperty, "PrimaryButton");
        }

        var cancelButton = new Button
        {
            Content = LocalizationManager.T("common.cancel"),
            Width = 100,
            Height = 36,
        };

        var titleText = new TextBlock { Text = title, FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 12) };
        var messageText = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 420,
            Foreground = (System.Windows.Media.Brush)Application.Current.Resources["Brush.InkSecondary"],
            FontSize = 13,
            LineHeight = 20,
        };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 24, 0, 0),
        };
        buttons.Children.Add(cancelButton);
        buttons.Children.Add(confirmButton);

        var content = new StackPanel { Margin = new Thickness(28) };
        content.Children.Add(titleText);
        content.Children.Add(messageText);
        content.Children.Add(buttons);

        var card = new Border
        {
            Background = (System.Windows.Media.Brush)Application.Current.Resources["Brush.Surface"],
            CornerRadius = new CornerRadius(12),
            Effect = (System.Windows.Media.Effects.Effect)Application.Current.Resources["Shadow.Dialog"],
            Child = content,
        };

        var window = new Window
        {
            Title = title,
            Width = 500,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = owner is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            WindowStyle = WindowStyle.None,
            Background = System.Windows.Media.Brushes.Transparent,
            AllowsTransparency = true,
            Content = new Border { Background = System.Windows.Media.Brushes.Transparent, Margin = new Thickness(20), Child = card },
        };

        if (owner is not null)
        {
            window.Owner = owner;
        }

        confirmButton.Click += (_, _) =>
        {
            result = true;
            window.Close();
        };
        cancelButton.Click += (_, _) => window.Close();

        window.ShowDialog();
        return result;
    }

    private static readonly DependencyProperty StyleProperty = Control.StyleProperty.AddOwner(typeof(Button));
}

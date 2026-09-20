using System.Windows;
using System.Windows.Controls;

namespace Dentiva.App.Views;

/// <summary>Minimal styled single-value input dialog.</summary>
public static class InputDialog
{
    public static string? Show(Window? owner, string title, string label, string initialValue = "")
    {
        string? result = null;

        var input = new TextBox { Height = 36, Text = initialValue, Margin = new Thickness(0, 8, 0, 0) };

        var okButton = new Button { Content = "Save", Width = 110, Height = 36 };
        okButton.SetResourceReference(FrameworkElement.StyleProperty, "PrimaryButton");

        var cancelButton = new Button { Content = "Cancel", Width = 96, Height = 36, Margin = new Thickness(10, 0, 0, 0) };

        var content = new StackPanel { Margin = new Thickness(28) };
        content.Children.Add(new TextBlock { Text = title, FontSize = 16, FontWeight = FontWeights.SemiBold });
        content.Children.Add(new TextBlock { Text = label, FontSize = 13, Foreground = (System.Windows.Media.Brush)Application.Current.Resources["Brush.InkSecondary"], Margin = new Thickness(0, 10, 0, 0) });
        content.Children.Add(input);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 22, 0, 0) };
        buttons.Children.Add(cancelButton);
        buttons.Children.Add(okButton);
        content.Children.Add(buttons);

        var window = new Window
        {
            Title = title,
            Width = 460,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = owner is null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            WindowStyle = WindowStyle.ToolWindow,
            Content = content,
            ShowInTaskbar = false,
        };

        if (owner is not null)
        {
            window.Owner = owner;
        }

        okButton.Click += (_, _) =>
        {
            result = input.Text;
            window.Close();
        };
        cancelButton.Click += (_, _) => window.Close();
        input.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                result = input.Text;
                window.Close();
            }
        };

        window.ShowDialog();
        return result;
    }
}

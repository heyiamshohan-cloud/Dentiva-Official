using System.Windows;
using System.Windows.Input;
using Dentiva.App.Services;

namespace Dentiva.App.Views;

public partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => UsernameBox.Focus();
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private void OnPasswordKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            SignIn();
        }
    }

    private void OnSignIn(object sender, RoutedEventArgs e) => SignIn();

    private void SignIn()
    {
        ErrorText.Visibility = Visibility.Collapsed;
        SignInButton.IsEnabled = false;

        try
        {
            var users = AppServices.Get<UserRepository>();
            var sessionManager = AppServices.Get<SessionManager>();
            var session = users.Authenticate(UsernameBox.Text.Trim(), PasswordBox.Password);

            if (session is null)
            {
                ErrorText.Text = LocalizationManager.T("login.invalidCredentials");
                ErrorText.Visibility = Visibility.Visible;
                PasswordBox.Clear();
                PasswordBox.Focus();
                return;
            }

            sessionManager.SignIn(session);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            Log.Error("Sign-in failed", ex);
            ErrorText.Text = LocalizationManager.T("error.saveFailed");
            ErrorText.Visibility = Visibility.Visible;
        }
        finally
        {
            SignInButton.IsEnabled = true;
        }
    }
}

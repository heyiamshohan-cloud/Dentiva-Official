using System.Windows;
using System.Windows.Controls;
using Dentiva.App.Services;
using Dentiva.Core.Domain;
using Dentiva.Infrastructure.Repositories;

namespace Dentiva.App.Views.Dialogs;

public partial class PaymentDialog : Window
{
    private readonly Invoice _invoice;

    public PaymentDialog(Invoice invoice)
    {
        InitializeComponent();

        _invoice = invoice;

        var settings = AppServices.Get<SettingsStore>().Settings;
        var symbol = settings.Clinic.CurrencySymbol;
        var due = Dentiva.Core.MoneyMath.ToMajor(_invoice.DueMinor).ToString("N2");
        ContextLine.Text = $"{_invoice.InvoiceNumber}  ·  balance due {symbol}{due}";

        AmountBox.Text = due;

        foreach (var method in settings.Billing.PaymentMethods.Where(m => m.IsActive))
        {
            MethodBox.Items.Add(method);
        }

        if (MethodBox.Items.Count > 0)
        {
            MethodBox.SelectedIndex = 0;
        }

        DateBox.SelectedDate = DateTime.Today;
        Loaded += (_, _) => AmountBox.Focus();
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        };
    }

    private void OnFullAmount(object sender, RoutedEventArgs e)
    {
        AmountBox.Text = Dentiva.Core.MoneyMath.ToMajor(_invoice.DueMinor).ToString("0.00");
    }

    private void OnMethodChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MethodBox.SelectedItem is PaymentMethodConfig method)
        {
            ReferenceLabel.Text = string.IsNullOrWhiteSpace(method.DetailsHint) ? "Reference" : $"{method.DetailsHint} / reference";
            ReferenceBox.Text = string.Empty;
        }
    }

    private void OnCancel(object sender, RoutedEventArgs e) => Close();

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        ErrorText.Visibility = Visibility.Collapsed;

        if (!decimal.TryParse(AmountBox.Text.Trim(), out var amount) || amount <= 0)
        {
            ShowError("Enter the payment amount as a positive number.");
            return;
        }

        if (MethodBox.SelectedItem is not PaymentMethodConfig method)
        {
            ShowError("Choose the payment method.");
            return;
        }

        var amountMinor = Dentiva.Core.MoneyMath.FromMajor(amount);
        if (_invoice.Status == InvoiceStatus.Paid && amountMinor > _invoice.DueMinor)
        {
            ShowError("This invoice is already fully paid.");
            return;
        }

        SaveButton.IsEnabled = false;
        try
        {
            var billing = AppServices.Get<BillingRepository>();
            var session = AppServices.Get<SessionManager>();
            var settings = AppServices.Get<SettingsStore>().Settings;

            var payment = new Payment
            {
                InvoiceId = _invoice.Id,
                PatientId = _invoice.PatientId,
                PaidAt = DateBox.SelectedDate ?? DateTime.Today,
                AmountMinor = amountMinor,
                Method = method.Kind,
                MethodLabel = method.Label,
                Reference = ReferenceBox.Text.Trim(),
                Notes = NotesBox.Text.Trim(),
            };

            var created = await Task.Run(() => billing.RecordPayment(payment, settings, session.Current ?? SessionContext.System));
            AppServices.Get<AuditRepository>().RecordStandalone(
                session.Current ?? SessionContext.System, "payment.recorded", "payment", created.Id.ToString(),
                $"{created.ReceiptNumber} on invoice {_invoice.InvoiceNumber}");

            AppServices.Get<ToastService>().Success("Payment recorded", $"Receipt {created.ReceiptNumber}.");
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            Log.Error("Payment recording failed", ex);
            ShowError("The payment could not be recorded. Nothing has been changed. Please try again.");
        }
        finally
        {
            SaveButton.IsEnabled = true;
        }
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
}

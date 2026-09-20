using System.Windows;
using Dentiva.App.Services;
using Dentiva.Core.Domain;
using Dentiva.Infrastructure.Repositories;

namespace Dentiva.App.Views.Dialogs;

public partial class InvoiceDialog : Window
{
    private readonly long _invoiceId;
    private Invoice? _invoice;

    public InvoiceDialog(long invoiceId)
    {
        InitializeComponent();
        _invoiceId = invoiceId;
        Loaded += async (_, _) => await LoadAsync();
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        };
    }

    private async Task LoadAsync()
    {
        var billing = AppServices.Get<BillingRepository>();
        _invoice = await Task.Run(() => billing.GetInvoice(_invoiceId));
        if (_invoice is null)
        {
            MessageBox.Show("This invoice could not be found. It may have been removed.", "Dentiva",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
            return;
        }

        var patients = AppServices.Get<PatientRepository>();
        var patient = await Task.Run(() => patients.GetById(_invoice.PatientId));

        InvoiceNumber.Text = _invoice.InvoiceNumber;
        PatientLine.Text = $"{patient?.FullName}  ·  {patient?.Code}";
        DateLine.Text = _invoice.InvoiceDate.ToString("dd MMM yyyy");
        StatusBadge.Text = new Infrastructure.StatusLabelConverter().Convert(_invoice.Status, null!, null!, null!).ToString();
        StatusBadge.SetResourceReference(ForegroundProperty, "Brush.AccentInk");

        ItemsGrid.ItemsSource = _invoice.Items;

        var symbol = AppServices.Get<SettingsStore>().Settings.Clinic.CurrencySymbol;
        string Money(long minor) => symbol + Dentiva.Core.MoneyMath.ToMajor(minor).ToString("N2");

        SubtotalText.Text = Money(_invoice.SubtotalMinor);
        DiscountText.Text = "− " + Money(_invoice.DiscountMinor);
        TaxText.Text = Money(_invoice.TaxMinor);
        TotalText.Text = Money(_invoice.TotalMinor);
        PaidText.Text = Money(_invoice.PaidMinor);
        DueText.Text = Money(_invoice.DueMinor);

        var payments = await Task.Run(() => billing.GetPayments(_invoice.Id));
        PaymentsList.ItemsSource = payments;
        NoPaymentsText.Visibility = payments.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        RecordPaymentButton.Visibility = _invoice.Status == InvoiceStatus.Voided ? Visibility.Collapsed : Visibility.Visible;
        VoidButton.Visibility = _invoice.Status == InvoiceStatus.Voided ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private async void OnRecordPayment(object sender, RoutedEventArgs e)
    {
        if (_invoice is null)
        {
            return;
        }

        var dialog = new PaymentDialog(_invoice) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            await LoadAsync();
        }
    }

    private async void OnVoid(object sender, RoutedEventArgs e)
    {
        if (_invoice is null)
        {
            return;
        }

        var confirm = ConfirmDialog.Show(
            "Void this invoice",
            "The invoice will be marked as voided and excluded from outstanding balances. The record is kept for audit — invoices are never deleted.",
            "Void invoice", danger: true, owner: this);

        if (!confirm)
        {
            return;
        }

        try
        {
            var billing = AppServices.Get<BillingRepository>();
            var session = AppServices.Get<SessionManager>();
            await Task.Run(() => billing.VoidInvoice(_invoice.Id, "Voided from billing", session.Current ?? SessionContext.System));
            AppServices.Get<AuditRepository>().RecordStandalone(
                session.Current ?? SessionContext.System, "invoice.voided", "invoice", _invoice.Id.ToString(), _invoice.InvoiceNumber);
            AppServices.Get<ToastService>().Info("Invoice voided", _invoice.InvoiceNumber + " is now voided.");
            await LoadAsync();
        }
        catch (InvalidOperationException ex)
        {
            ErrorText.Text = ex.Message;
            ErrorText.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            Log.Error("Invoice void failed", ex);
            ErrorText.Text = "The invoice could not be voided. Please try again.";
            ErrorText.Visibility = Visibility.Visible;
        }
    }
}

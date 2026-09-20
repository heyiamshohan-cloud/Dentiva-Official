using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Dentiva.App.Services;
using Dentiva.Core.Domain;
using Dentiva.Core.Time;
using Dentiva.Infrastructure.Repositories;

namespace Dentiva.App.Views;

public partial class BillingPage : UserControl
{
    private readonly BillingRepository _billing;
    private readonly ToastService _toasts;
    private readonly long? _patientFilter;
    private DateRange _range;
    private int _page = 1;
    private const int PageSize = 50;
    private DispatcherTimer? _debounce;

    public BillingPage(long? patientFilter = null)
    {
        InitializeComponent();
        _billing = AppServices.Get<BillingRepository>();
        _toasts = AppServices.Get<ToastService>();
        _patientFilter = patientFilter;
        Loaded += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var scope = ScopeFilter.SelectedIndex;
        _range = scope switch
        {
            1 => DatePeriods.Resolve(DatePeriodKind.Today, today, default, default),
            2 => DatePeriods.Resolve(DatePeriodKind.Last7Days, today, default, default),
            3 => DatePeriods.Resolve(DatePeriodKind.ThisMonth, today, default, default),
            _ => new DateRange(new DateOnly(2000, 1, 1), today.AddYears(50)),
        };

        var status = StatusFilter.SelectedIndex switch
        {
            1 => InvoiceStatus.Unpaid,
            2 => InvoiceStatus.PartiallyPaid,
            3 => InvoiceStatus.Paid,
            4 => InvoiceStatus.Voided,
            _ => (InvoiceStatus?)null,
        };

        var query = new InvoiceQuery
        {
            SearchText = SearchBox.Text.Trim(),
            Status = status,
            From = _range.From,
            To = _range.To,
            PatientId = _patientFilter,
            OnlyOutstanding = OutstandingFilter.SelectedIndex == 1,
            Limit = PageSize,
        };

        try
        {
            var items = await Task.Run(() => _billing.QueryInvoices(query));
            InvoicesGrid.ItemsSource = items;

            var summary = await Task.Run(() => _billing.Summary(_range.From, _range.To));
            var symbol = AppServices.Get<SettingsStore>().Settings.Clinic.CurrencySymbol;
            SummaryLine.Text = $"{summary.InvoiceCount} invoices in range  ·  billed {symbol}{Dentiva.Core.MoneyMath.ToMajor(summary.BilledMinor):N2}  ·  collected {symbol}{Dentiva.Core.MoneyMath.ToMajor(summary.PaidMinor):N2}  ·  outstanding {symbol}{Dentiva.Core.MoneyMath.ToMajor(summary.DueMinor):N2}";
            PageInfo.Text = $"Showing {items.Count} invoices";
            PrevButton.IsEnabled = false;
            NextButton.IsEnabled = false;
        }
        catch (Exception ex)
        {
            Log.Error("Billing list failed", ex);
            _toasts.Error("Billing", "Invoices could not be loaded. Please try again.");
        }
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        _debounce ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(220) };
        _debounce.Stop();
        _debounce.Tick += async (_, _) =>
        {
            _debounce!.Stop();
            await LoadAsync();
        };
        _debounce.Start();
    }

    private async void OnFilterChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded)
        {
            await LoadAsync();
        }
    }

    private async void OnPrevPage(object sender, RoutedEventArgs e)
    {
        if (_page > 1)
        {
            _page--;
            await LoadAsync();
        }
    }

    private async void OnNextPage(object sender, RoutedEventArgs e)
    {
        _page++;
        await LoadAsync();
    }

    private void OnOpenInvoice(object sender, MouseButtonEventArgs e)
    {
        if (InvoicesGrid.SelectedItem is InvoiceListItem item)
        {
            var dialog = new Dialogs.InvoiceDialog(item.Id) { Owner = Window.GetWindow(this) };
            dialog.ShowDialog();
            _ = LoadAsync();
        }
    }
}

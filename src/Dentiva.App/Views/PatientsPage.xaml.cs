using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Dentiva.App.Services;
using Dentiva.Core.Domain;
using Dentiva.Infrastructure.Repositories;

namespace Dentiva.App.Views;

public partial class PatientsPage : UserControl
{
    private readonly PatientRepository _patients;
    private readonly ToastService _toasts;
    private PatientQuery _query = new();
    private int _page = 1;
    private const int PageSize = 50;
    private PagedPatients? _result;
    private DispatcherTimer? _debounce;

    public PatientsPage()
    {
        InitializeComponent();
        _patients = AppServices.Get<PatientRepository>();
        _toasts = AppServices.Get<ToastService>();
        Loaded += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var gender = StatusFilter.SelectedIndex switch
        {
            1 => PatientStatus.Active,
            2 => PatientStatus.Inactive,
            3 => PatientStatus.Archived,
            _ => (PatientStatus?)null,
        };

        _query = new PatientQuery
        {
            SearchText = SearchBox.Text.Trim(),
            Status = gender,
            OnlyWithOutstanding = BalanceFilter.SelectedIndex == 1,
            Page = _page,
            PageSize = PageSize,
            Today = today,
            SortBy = "name",
        };

        try
        {
            _result = await Task.Run(() => _patients.Search(_query));

            PatientsGrid.ItemsSource = _result.Items;
            CountLine.Text = _result.TotalCount == 0
                ? "No patients match the current filters."
                : $"{_result.TotalCount} patient record{(_result.TotalCount == 1 ? "" : "s")}";
            PageInfo.Text = $"Page {_result.Page} of {_result.TotalPages}";
            PrevButton.IsEnabled = _result.Page > 1;
            NextButton.IsEnabled = _result.Page < _result.TotalPages;
        }
        catch (Exception ex)
        {
            Log.Error("Patient list failed", ex);
            _toasts.Error("Patients", "The patient list could not be loaded. Please try again.");
        }
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        _debounce ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(220) };
        _debounce.Stop();
        _debounce.Tick += async (_, _) =>
        {
            _debounce!.Stop();
            _page = 1;
            await LoadAsync();
        };
        _debounce.Start();
    }

    private async void OnFilterChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        _page = 1;
        await LoadAsync();
    }

    private async void OnResetFilters(object sender, RoutedEventArgs e)
    {
        SearchBox.Text = string.Empty;
        StatusFilter.SelectedIndex = 0;
        BalanceFilter.SelectedIndex = 0;
        _page = 1;
        await LoadAsync();
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
        if (_result is not null && _page < _result.TotalPages)
        {
            _page++;
            await LoadAsync();
        }
    }

    private void OnRowDoubleClick(object sender, MouseButtonEventArgs e)
    {
        OpenSelected();
    }

    private void OnGridKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            OpenSelected();
            e.Handled = true;
        }
    }

    private void OpenSelected()
    {
        if (PatientsGrid.SelectedItem is Patient patient)
        {
            AppServices.Get<NavigationService>().NavigateTo("patient-profile", () => new PatientProfilePage(patient.Id));
        }
    }

    private void OnNewPatient(object sender, RoutedEventArgs e)
    {
        var dialog = new Dialogs.NewPatientDialog { Owner = Window.GetWindow(this) };
        if (dialog.ShowDialog() == true && dialog.CreatedPatientId is long id)
        {
            AppServices.Get<NavigationService>().NavigateTo("patient-profile", () => new PatientProfilePage(id));
        }
    }
}

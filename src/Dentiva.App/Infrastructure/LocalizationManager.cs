using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using Dentiva.Core.Localization;

namespace Dentiva.App.Infrastructure;

/// <summary>
/// Application-wide language state. Every user-facing string resolves through
/// {loc:Loc Key}; switching language swaps the font family resources and
/// re-evaluates every localized binding immediately.
/// </summary>
public sealed class LocalizationManager : ObservableObject
{
    private static readonly LocalizationManager _instance = new();
    public static LocalizationManager Instance => _instance;

    private string _language = "en";

    public string Language
    {
        get => _language;
        set
        {
            if (!LocalizationTables.IsSupported(value))
            {
                value = "en";
            }

            if (SetProperty(ref _language, value))
            {
                ApplyFonts();
                OnPropertyChanged(nameof(Tick));
                LanguageChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>Binding tick incremented on language change.</summary>
    public int Tick => 0;

    public LanguageDescriptor Descriptor => LocalizationTables.Describe(_language);

    public event EventHandler? LanguageChanged;

    public string this[string key] => LocalizationTables.Translate(_language, key);

    public string Format(string key, params object[] args) => string.Format(CultureInfo.CurrentCulture, this[key], args);

    public static string T(string key) => LocalizationTables.Translate(_instance._language, key);

    public void ApplyFonts()
    {
        var app = Application.Current;
        if (app is null)
        {
            return;
        }

        if (_language == "bn")
        {
            app.Resources["Font.Base"] = app.Resources["Font.Bengali"];
            app.Resources["Font.Medium"] = app.Resources["Font.Bengali"];
            app.Resources["Font.SemiBold"] = app.Resources["Font.BengaliSemiBold"];
            app.Resources["Font.Bold"] = app.Resources["Font.BengaliSemiBold"];
        }
        else
        {
            app.Resources["Font.Base"] = new FontFamily("pack://application:,,,/Assets/Fonts/Inter-Regular.ttf#Inter");
            app.Resources["Font.Medium"] = new FontFamily("pack://application:,,,/Assets/Fonts/Inter-Medium.ttf#Inter Medium");
            app.Resources["Font.SemiBold"] = new FontFamily("pack://application:,,,/Assets/Fonts/Inter-SemiBold.ttf#Inter SemiBold");
            app.Resources["Font.Bold"] = new FontFamily("pack://application:,,,/Assets/Fonts/Inter-Bold.ttf#Inter Bold");
        }
    }
}

public sealed class LocConverter : IValueConverter
{
    public static readonly LocConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        parameter is string key ? LocalizationManager.T(key) : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Usage: Text="{loc:Loc Nav.Patients}".</summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class LocExtension : MarkupExtension
{
    public LocExtension()
    {
    }

    public LocExtension(string key)
    {
        Key = key;
    }

    public string? Key { get; set; }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        if (Key is null)
        {
            return string.Empty;
        }

        var binding = new Binding(nameof(LocalizationManager.Tick))
        {
            Source = LocalizationManager.Instance,
            Mode = BindingMode.OneWay,
            Converter = LocConverter.Instance,
            ConverterParameter = Key,
        };

        return binding.ProvideValue(serviceProvider);
    }
}

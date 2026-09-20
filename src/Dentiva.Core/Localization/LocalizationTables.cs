using System.Reflection;
using System.Text.Json;

namespace Dentiva.Core.Localization;

/// <summary>Metadata about a supported UI language.</summary>
public sealed record LanguageDescriptor(string Code, string EnglishName, string NativeName, string FontFamilyKey)
{
    public override string ToString() => $"{NativeName} ({EnglishName})";
}

/// <summary>
/// Loads and serves localization tables. Tables are embedded JSON resources;
/// lookups fall back to English and finally to the key itself so a missing
/// translation can never produce an empty label.
/// </summary>
public static class LocalizationTables
{
    public static readonly LanguageDescriptor English =
        new("en", "English", "English", "Font.Base");

    public static readonly LanguageDescriptor Bengali =
        new("bn", "Bengali", "বাংলা", "Font.Bengali");

    public static readonly LanguageDescriptor[] Supported = { English, Bengali };

    private static readonly object Gate = new();
    private static Dictionary<string, Dictionary<string, string>>? _cache;

    public static LanguageDescriptor Describe(string code) =>
        Supported.FirstOrDefault(l => l.Code == code) ?? English;

    public static bool IsSupported(string code) => Supported.Any(l => l.Code == code);

    public static IReadOnlyDictionary<string, string> GetTable(string code)
    {
        var table = LoadAll();
        return table.TryGetValue(code, out var t) ? t : table["en"];
    }

    /// <summary>Translates a key; falls back to English, then the raw key.</summary>
    public static string Translate(string code, string key)
    {
        var all = LoadAll();
        if (all.TryGetValue(code, out var table) && table.TryGetValue(key, out var value))
        {
            return value;
        }

        if (all.TryGetValue("en", out var english) && english.TryGetValue(key, out var fallback))
        {
            return fallback;
        }

        return key;
    }

    /// <summary>Translation coverage of a non-English table relative to English.</summary>
    public static double Coverage(string code)
    {
        var all = LoadAll();
        if (!all.TryGetValue(code, out var table))
        {
            return 0;
        }

        var english = all["en"];
        if (english.Count == 0)
        {
            return 1;
        }

        var hit = english.Keys.Count(table.ContainsKey);
        return (double)hit / english.Count;
    }

    private static Dictionary<string, Dictionary<string, string>> LoadAll()
    {
        lock (Gate)
        {
            if (_cache is not null)
            {
                return _cache;
            }

            var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            var assembly = typeof(LocalizationTables).Assembly;
            foreach (var name in assembly.GetManifestResourceNames()
                         .Where(n => n.Contains(".Localization.", StringComparison.Ordinal)))
            {
                var code = Path.GetFileNameWithoutExtension(name).ToLowerInvariant();
                using var stream = assembly.GetManifestResourceStream(name)!;
                using var reader = new StreamReader(stream);
                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(reader.ReadToEnd())
                           ?? new Dictionary<string, string>();
                result[code] = dict;
            }

            _cache = result;
            return result;
        }
    }
}

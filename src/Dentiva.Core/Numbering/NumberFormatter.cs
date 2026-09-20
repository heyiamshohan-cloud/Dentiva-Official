using System.Text.RegularExpressions;

namespace Dentiva.Core.Numbering;

/// <summary>
/// Configurable document numbering. A template such as
/// "INV-{YYYY}-{SEQ:5}" is expanded with the date and the sequence value.
/// Recognised tokens: {YYYY} {YY} {MM} {DD} {SEQ} {SEQ:n}.
/// </summary>
public static partial class NumberFormatter
{
    [GeneratedRegex(@"\{(YYYY|YY|MM|DD|SEQ(?::(\d+))?)\}", RegexOptions.CultureInvariant)]
    private static partial Regex TokenRegex();

    public static string Format(string template, long sequence, DateOnly date)
    {
        Guard.NotNullOrWhiteSpace(template);

        return TokenRegex().Replace(template, match =>
        {
            switch (match.Groups[1].Value)
            {
                case "YYYY": return date.Year.ToString("0000", System.Globalization.CultureInfo.InvariantCulture);
                case "YY": return (date.Year % 100).ToString("00", System.Globalization.CultureInfo.InvariantCulture);
                case "MM": return date.Month.ToString("00", System.Globalization.CultureInfo.InvariantCulture);
                case "DD": return date.Day.ToString("00", System.Globalization.CultureInfo.InvariantCulture);
                case "SEQ":
                    var width = 4;
                    if (match.Groups[2].Success && int.TryParse(match.Groups[2].Value, out var parsed))
                    {
                        width = Math.Clamp(parsed, 1, 12);
                    }

                    return sequence.ToString(new string('0', width), System.Globalization.CultureInfo.InvariantCulture);
                default:
                    return match.Value;
            }
        });
    }

    /// <summary>Validates a template before it is saved in settings.</summary>
    public static bool IsValidTemplate(string? template, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(template))
        {
            error = "Template is empty.";
            return false;
        }

        if (!template.Contains("{SEQ", StringComparison.Ordinal))
        {
            error = "Template must contain a {SEQ} token so numbers stay unique.";
            return false;
        }

        try
        {
            Format(template, 1, new DateOnly(2026, 1, 1));
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}

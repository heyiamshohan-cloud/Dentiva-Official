namespace Dentiva.Core.Teeth;

/// <summary>A single tooth in the FDI two-digit notation.</summary>
public sealed record ToothInfo(
    string Number,
    Dentition Dentition,
    int Quadrant,
    int Position,
    string EnglishName,
    string BengaliName)
{
    public string QuadrantLabel => Quadrant switch
    {
        1 => "Upper Right",
        2 => "Upper Left",
        3 => "Lower Left",
        4 => "Lower Right",
        5 => "Upper Right (milk)",
        6 => "Upper Left (milk)",
        7 => "Lower Left (milk)",
        8 => "Lower Right (milk)",
        _ => string.Empty,
    };
}

/// <summary>
/// FDI tooth catalog for permanent (11–48) and deciduous (51–85) teeth,
/// with odontogram layout order per arch.
/// </summary>
public static class ToothCatalog
{
    private static readonly string[] PermanentNames =
    {
        "Central Incisor", "Lateral Incisor", "Canine",
        "First Premolar", "Second Premolar", "First Molar", "Second Molar", "Third Molar",
    };

    private static readonly string[] PermanentNamesBn =
    {
        "কেন্দ্রীয় কর্তনী", "পার্শ্বীয় কর্তনী", "ক্যানাইন",
        "প্রথম প্রিমোলার", "দ্বিতীয় প্রিমোলার", "প্রথম মোলার", "দ্বিতীয় মোলার", "তৃতীয় মোলার",
    };

    private static readonly string[] DeciduousNames =
    {
        "Central Incisor", "Lateral Incisor", "Canine", "First Molar", "Second Molar",
    };

    private static readonly string[] DeciduousNamesBn =
    {
        "কেন্দ্রীয় কর্তনী", "পার্শ্বীয় কর্তনী", "ক্যানাইন", "প্রথম মোলার", "দ্বিতীয় মোলার",
    };

    private static readonly Dictionary<string, ToothInfo> All;

    static ToothCatalog()
    {
        All = new Dictionary<string, ToothInfo>(32);
        var quadrants = new[] { 1, 2, 3, 4 };
        foreach (var q in quadrants)
        {
            for (var pos = 1; pos <= 8; pos++)
            {
                var number = $"{q}{pos}";
                All[number] = new ToothInfo(
                    number, Dentition.Permanent, q, pos,
                    PermanentNames[pos - 1], PermanentNamesBn[pos - 1]);
            }
        }

        foreach (var q in new[] { 5, 6, 7, 8 })
        {
            for (var pos = 1; pos <= 5; pos++)
            {
                var number = $"{q}{pos}";
                All[number] = new ToothInfo(
                    number, Dentition.Deciduous, q, pos,
                    DeciduousNames[pos - 1], DeciduousNamesBn[pos - 1]);
            }
        }
    }

    public static IReadOnlyCollection<ToothInfo> AllTeeth => All.Values;

    public static ToothInfo? Find(string? number) =>
        number is not null && All.TryGetValue(number, out var info) ? info : null;

    public static bool IsValid(string? number) => number is not null && All.ContainsKey(number);

    /// <summary>Upper arch, patient's right → left (odontogram display order).</summary>
    public static string[] UpperArch(bool deciduous) => deciduous
        ? new[] { "55", "54", "53", "52", "51", "61", "62", "63", "64", "65" }
        : new[] { "18", "17", "16", "15", "14", "13", "12", "11", "21", "22", "23", "24", "25", "26", "27", "28" };

    /// <summary>Lower arch, patient's left → right (odontogram display order).</summary>
    public static string[] LowerArch(bool deciduous) => deciduous
        ? new[] { "85", "84", "83", "82", "81", "71", "72", "73", "74", "75" }
        : new[] { "48", "47", "46", "45", "44", "43", "42", "41", "31", "32", "33", "34", "35", "36", "37", "38" };
}

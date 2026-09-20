using Dentiva.Core;
using Dentiva.Core.Numbering;
using Dentiva.Core.Security;
using Dentiva.Core.Teeth;
using Dentiva.Core.Time;
using Xunit;

namespace Dentiva.Tests;

public class MoneyTests
{
    [Theory]
    [InlineData(1000, 15, 150)]       // simple percent
    [InlineData(1000, 7.5, 75)]
    [InlineData(999, 10, 100)]        // 99.9 rounds half-up to 100
    [InlineData(0, 25, 0)]
    public void PercentOf_rounds_half_up(long amount, decimal percent, long expected)
    {
        Assert.Equal(expected, MoneyMath.PercentOf(amount, percent));
    }

    [Fact]
    public void Invoice_totals_follow_the_documented_formula()
    {
        var lines = new[]
        {
            new InvoiceCalculator.LineInput { UnitPriceMinor = 500_00, Quantity = 1, LineDiscountMinor = 50_00 },
            new InvoiceCalculator.LineInput { UnitPriceMinor = 200_00, Quantity = 2, LineDiscountMinor = 0 },
        };

        // Subtotal = 500 - 50 + 400 = 850.00
        // Invoice discount 10% of 850 = 85.00 → net 765.00
        // Tax 5% of 765 = 38.25 → grand total 803.25
        // Paid 300.00 → due 503.25
        var totals = InvoiceCalculator.Calculate(lines, 0, 10m, 5m, 300_00);

        Assert.Equal(850_00, totals.SubtotalMinor);
        Assert.Equal(50_00, totals.LineDiscountsMinor);
        Assert.Equal(85_00, totals.InvoiceDiscountMinor);
        Assert.Equal(38_25, totals.TaxMinor);
        Assert.Equal(803_25, totals.GrandTotalMinor);
        Assert.Equal(503_25, totals.DueMinor);
    }

    [Fact]
    public void Discount_cannot_push_total_below_zero()
    {
        var lines = new[]
        {
            new InvoiceCalculator.LineInput { UnitPriceMinor = 100_00, Quantity = 1 },
        };

        var totals = InvoiceCalculator.Calculate(lines, 500_00, 0m, 0m, 0);
        Assert.Equal(0, totals.GrandTotalMinor);
        Assert.Equal(0, totals.DueMinor);
    }

    [Fact]
    public void Overpayment_creates_credit_balance()
    {
        var lines = new[] { new InvoiceCalculator.LineInput { UnitPriceMinor = 100_00, Quantity = 1 } };
        var totals = InvoiceCalculator.Calculate(lines, 0, 0m, 0m, 120_00);
        Assert.Equal(-20_00, totals.DueMinor);
    }

    [Fact]
    public void FormatMajor_renders_symbol_and_thousands()
    {
        Assert.Equal("৳1,234.50", MoneyMath.FormatMajor(123_450, "৳"));
        Assert.Equal("1,234.50 ৳", MoneyMath.FormatMajor(123_450, "৳", symbolFirst: false));
    }
}

public class NumberFormatterTests
{
    [Theory]
    [InlineData("INV-{YYYY}-{SEQ:4}", 7, "INV-2026-0007")]
    [InlineData("R-{YY}{MM}-{SEQ}", 12, "R-2601-0012")]
    [InlineData("P-{SEQ:5}", 42, "P-00042")]
    public void Templates_expand_correctly(string template, long seq, string expected)
    {
        Assert.Equal(expected, NumberFormatter.Format(template, seq, new DateOnly(2026, 1, 5)));
    }

    [Fact]
    public void Template_requires_seq_token()
    {
        Assert.False(NumberFormatter.IsValidTemplate("INV-2026-0001", out _));
        Assert.True(NumberFormatter.IsValidTemplate("INV-{YYYY}-{SEQ:4}", out _));
    }
}

public class DatePeriodTests
{
    [Fact]
    public void Today_resolves_to_single_day()
    {
        var today = new DateOnly(2026, 9, 20);
        var range = DatePeriods.Resolve(DatePeriodKind.Today, today, default, default);
        Assert.Equal(today, range.From);
        Assert.Equal(today, range.To);
    }

    [Fact]
    public void Last7Days_is_inclusive()
    {
        var today = new DateOnly(2026, 9, 20);
        var range = DatePeriods.Resolve(DatePeriodKind.Last7Days, today, default, default);
        Assert.Equal(7, range.Days);
        Assert.Equal(new DateOnly(2026, 9, 14), range.From);
    }

    [Fact]
    public void Custom_range_swaps_reversed_bounds()
    {
        var today = new DateOnly(2026, 9, 20);
        var range = DatePeriods.Resolve(DatePeriodKind.Custom, today, new DateOnly(2026, 9, 10), new DateOnly(2026, 9, 1));
        Assert.Equal(new DateOnly(2026, 9, 1), range.From);
        Assert.Equal(new DateOnly(2026, 9, 10), range.To);
    }
}

public class PasswordHasherTests
{
    [Fact]
    public void Hash_and_verify_roundtrip()
    {
        var hash = PasswordHasher.Hash("S3cure!Pass");
        Assert.NotEqual("S3cure!Pass", hash, ignoreCase: true);
        Assert.True(PasswordHasher.Verify("S3cure!Pass", hash));
        Assert.False(PasswordHasher.Verify("wrong", hash));
    }

    [Fact]
    public void Hashes_are_unique_per_call()
    {
        Assert.NotEqual(PasswordHasher.Hash("same"), PasswordHasher.Hash("same"));
    }

    [Theory]
    [InlineData("", 0)]
    [InlineData("abc", 0)]
    [InlineData("abcdefgh", 1)]
    [InlineData("abcdefgh1234", 3)]
    [InlineData("Abcdefgh1234!", 3)]
    public void Strength_scores_are_sensible(string password, int expected)
    {
        Assert.Equal(expected, PasswordHasher.StrengthScore(password));
    }
}

public class ToothCatalogTests
{
    [Theory]
    [InlineData("11")]
    [InlineData("28")]
    [InlineData("38")]
    [InlineData("48")]
    [InlineData("51")]
    [InlineData("85")]
    public void Fdi_numbers_are_valid(string number)
    {
        Assert.True(ToothCatalog.IsValid(number));
        Assert.NotNull(ToothCatalog.Find(number));
    }

    [Theory]
    [InlineData("19")]
    [InlineData("99")]
    [InlineData("56")]
    [InlineData("")]
    [InlineData(null)]
    public void Invalid_numbers_are_rejected(string? number)
    {
        Assert.False(ToothCatalog.IsValid(number));
    }

    [Fact]
    public void Arches_are_symmetrical()
    {
        Assert.Equal(16, ToothCatalog.UpperArch(deciduous: false).Length);
        Assert.Equal(16, ToothCatalog.LowerArch(deciduous: false).Length);
        Assert.Equal(10, ToothCatalog.UpperArch(deciduous: true).Length);
        Assert.Equal(10, ToothCatalog.LowerArch(deciduous: true).Length);
    }
}

public class LocalizationTests
{
    [Fact]
    public void English_and_bengali_tables_are_complete()
    {
        var en = LocalizationTables.GetTable("en");
        var bn = LocalizationTables.GetTable("bn");
        Assert.True(en.Count > 50, "English table looks incomplete");
        var missing = en.Keys.Where(k => !bn.ContainsKey(k)).ToList();
        Assert.True(missing.Count == 0, $"Bengali is missing keys: {string.Join(", ", missing.Take(10))}");
    }

    [Fact]
    public void Unknown_keys_fall_back_to_the_key_itself()
    {
        Assert.Equal("some.missing.key", LocalizationTables.Translate("en", "some.missing.key"));
    }
}

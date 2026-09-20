namespace Dentiva.Core;

/// <summary>
/// Decimal-safe monetary arithmetic. All persisted amounts are integer minor
/// units (e.g. poisha/cents). Floating point is never used for money.
/// </summary>
public static class MoneyMath
{
    public const int MinorUnitsPerMajor = 100;

    public static long FromMajor(decimal major) =>
        (long)Math.Round(major * MinorUnitsPerMajor, MidpointRounding.AwayFromZero);

    public static decimal ToMajor(long minor) => minor / (decimal)MinorUnitsPerMajor;

    /// <summary>amount × percent / 100, rounded half-up to the nearest minor unit.</summary>
    public static long PercentOf(long amount, decimal percent) =>
        (long)Math.Round(amount * percent / 100m, MidpointRounding.AwayFromZero);

    public static long Multiply(long unitPriceMinor, decimal quantity) =>
        (long)Math.Round(unitPriceMinor * quantity, MidpointRounding.AwayFromZero);

    public static string FormatMajor(long minor, string symbol, bool symbolFirst = true, int decimalDigits = 2)
    {
        var major = ToMajor(minor).ToString("N" + decimalDigits);
        return symbolFirst ? $"{symbol}{major}" : $"{major} {symbol}";
    }
}

/// <summary>
/// Single source of truth for invoice arithmetic:
/// Subtotal = Σ(line gross) − Σ(line discounts)
/// Grand total = Subtotal − invoice discount + tax
/// Due = Grand total − payments.
/// </summary>
public static class InvoiceCalculator
{
    public sealed class LineInput
    {
        public long UnitPriceMinor { get; init; }
        public decimal Quantity { get; init; } = 1m;
        public long LineDiscountMinor { get; init; }
    }

    public sealed class Totals
    {
        public long SubtotalMinor { get; init; }
        public long LineDiscountsMinor { get; init; }
        public long InvoiceDiscountMinor { get; init; }
        public long TaxMinor { get; init; }
        public long GrandTotalMinor { get; init; }
        public long PaidMinor { get; init; }
        public long DueMinor { get; init; }
    }

    public static Totals Calculate(
        IReadOnlyCollection<LineInput> lines,
        long invoiceDiscountMinor,
        decimal invoiceDiscountPercent,
        decimal taxPercent,
        long paidMinor)
    {
        Guard.NotNull(lines);

        long subtotal = 0, lineDiscounts = 0;
        foreach (var line in lines)
        {
            subtotal += MoneyMath.Multiply(line.UnitPriceMinor, line.Quantity);
            lineDiscounts += line.LineDiscountMinor;
        }

        long net = subtotal - lineDiscounts;
        if (net < 0)
        {
            net = 0;
        }

        long discount = invoiceDiscountMinor;
        if (invoiceDiscountPercent > 0)
        {
            discount += MoneyMath.PercentOf(net, invoiceDiscountPercent);
        }

        if (discount > net)
        {
            discount = net; // a discount can never push the total below zero
        }

        long taxable = net - discount;
        long tax = MoneyMath.PercentOf(taxable, taxPercent);
        long grand = taxable + tax;

        return new Totals
        {
            SubtotalMinor = subtotal,
            LineDiscountsMinor = lineDiscounts,
            InvoiceDiscountMinor = discount,
            TaxMinor = tax,
            GrandTotalMinor = grand,
            PaidMinor = paidMinor,
            DueMinor = grand - paidMinor,
        };
    }
}

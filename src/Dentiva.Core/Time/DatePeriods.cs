using Dentiva.Core.Domain;

namespace Dentiva.Core.Time;

public readonly record struct DateRange(DateOnly From, DateOnly To)
{
    public int Days => To.DayNumber - From.DayNumber + 1;
    public bool Contains(DateOnly date) => date >= From && date <= To;
}

public static class DatePeriods
{
    /// <summary>Resolves a UI period selection to a concrete inclusive range.</summary>
    public static DateRange Resolve(DatePeriodKind kind, DateOnly today, DateOnly customFrom, DateOnly customTo)
    {
        switch (kind)
        {
            case DatePeriodKind.Today:
                return new DateRange(today, today);
            case DatePeriodKind.Last7Days:
                return new DateRange(today.AddDays(-6), today);
            case DatePeriodKind.ThisMonth:
                return new DateRange(new DateOnly(today.Year, today.Month, 1), today);
            case DatePeriodKind.Last3Months:
                return new DateRange(today.AddMonths(-3).AddDays(1), today);
            case DatePeriodKind.Last6Months:
                return new DateRange(today.AddMonths(-6).AddDays(1), today);
            case DatePeriodKind.Last12Months:
                return new DateRange(today.AddMonths(-12).AddDays(1), today);
            case DatePeriodKind.Custom:
                if (customFrom > customTo)
                {
                    (customFrom, customTo) = (customTo, customFrom);
                }

                return new DateRange(customFrom, customTo);
            default:
                return new DateRange(today, today);
        }
    }

    public static string Describe(DatePeriodKind kind) => kind switch
    {
        DatePeriodKind.Today => "Today",
        DatePeriodKind.Last7Days => "Last 7 Days",
        DatePeriodKind.ThisMonth => "Current Month",
        DatePeriodKind.Last3Months => "Last 3 Months",
        DatePeriodKind.Last6Months => "Last 6 Months",
        DatePeriodKind.Last12Months => "Last 12 Months",
        DatePeriodKind.Custom => "Custom",
        _ => "Today",
    };
}

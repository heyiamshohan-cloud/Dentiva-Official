using System.Data;
using System.Globalization;
using Dapper;

namespace Dentiva.Infrastructure.Persistence;

/// <summary>
/// Dapper type handlers giving every value an exact, culture-invariant
/// persistence format. Dates are ISO-8601 TEXT; decimals are stored as
/// invariant TEXT so percentages and quantities round-trip bit-exactly.
/// </summary>
public static class TypeHandlers
{
    private static bool _registered;

    public static void Register()
    {
        if (_registered)
        {
            return;
        }

        SqlMapper.RemoveTypeMap(typeof(DateOnly));
        SqlMapper.RemoveTypeMap(typeof(DateOnly?));
        SqlMapper.AddTypeHandler(new DateOnlyHandler());

        SqlMapper.RemoveTypeMap(typeof(TimeOnly));
        SqlMapper.RemoveTypeMap(typeof(TimeOnly?));
        SqlMapper.AddTypeHandler(new TimeOnlyHandler());

        SqlMapper.RemoveTypeMap(typeof(DateTimeOffset));
        SqlMapper.RemoveTypeMap(typeof(DateTimeOffset?));
        SqlMapper.AddTypeHandler(new DateTimeOffsetHandler());

        SqlMapper.RemoveTypeMap(typeof(decimal));
        SqlMapper.RemoveTypeMap(typeof(decimal?));
        SqlMapper.AddTypeHandler(new DecimalAsTextHandler());

        _registered = true;
    }
}

public sealed class DateOnlyHandler : SqlMapper.TypeHandler<DateOnly>
{
    public const string Format = "yyyy-MM-dd";

    public override DateOnly Parse(object value) =>
        DateOnly.ParseExact((string)value, Format, CultureInfo.InvariantCulture);

    public override void SetValue(IDbDataParameter parameter, DateOnly value) =>
        parameter.Value = value.ToString(Format, CultureInfo.InvariantCulture);
}

public sealed class TimeOnlyHandler : SqlMapper.TypeHandler<TimeOnly>
{
    public const string Format = "HH:mm";

    public override TimeOnly Parse(object value) =>
        TimeOnly.ParseExact((string)value, Format, CultureInfo.InvariantCulture);

    public override void SetValue(IDbDataParameter parameter, TimeOnly value) =>
        parameter.Value = value.ToString(Format, CultureInfo.InvariantCulture);
}

public sealed class DateTimeOffsetHandler : SqlMapper.TypeHandler<DateTimeOffset>
{
    public override DateTimeOffset Parse(object value) =>
        DateTimeOffset.Parse((string)value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    public override void SetValue(IDbDataParameter parameter, DateTimeOffset value) =>
        parameter.Value = value.ToString("o", CultureInfo.InvariantCulture);
}

public sealed class DecimalAsTextHandler : SqlMapper.TypeHandler<decimal>
{
    public static string ToText(decimal value) =>
        value.ToString("0.############", CultureInfo.InvariantCulture);

    public static decimal FromText(object value)
    {
        if (value is decimal d)
        {
            return d;
        }

        if (value is long or int or double)
        {
            return Convert.ToDecimal(value, CultureInfo.InvariantCulture);
        }

        return decimal.Parse((string)value, CultureInfo.InvariantCulture);
    }

    public override decimal Parse(object value) => FromText(value);

    public override void SetValue(IDbDataParameter parameter, decimal value) =>
        parameter.Value = ToText(value);
}

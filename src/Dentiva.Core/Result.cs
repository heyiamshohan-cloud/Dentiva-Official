namespace Dentiva.Core;

/// <summary>Operation result without exceptions for expected user-facing failures.</summary>
public sealed class Result
{
    public bool Success { get; }
    public string ErrorKey { get; }
    public string[] ErrorArgs { get; }
    public string? TechnicalDetail { get; }

    private Result(bool success, string errorKey, string[] errorArgs, string? technicalDetail)
    {
        Success = success;
        ErrorKey = errorKey;
        ErrorArgs = errorArgs;
        TechnicalDetail = technicalDetail;
    }

    public static Result Ok() => new(true, string.Empty, Array.Empty<string>(), null);

    public static Result Fail(string errorKey, params string[] args) =>
        new(false, errorKey, args, null);

    public static Result FailTechnical(string errorKey, string technicalDetail, params string[] args) =>
        new(false, errorKey, args, technicalDetail);

    public static Result<T> Ok<T>(T value) => Result<T>.Ok(value);
}

public sealed class Result<T>
{
    public bool Success { get; }
    public T? Value { get; }
    public string ErrorKey { get; }
    public string[] ErrorArgs { get; }
    public string? TechnicalDetail { get; }

    private Result(bool success, T? value, string errorKey, string[] errorArgs, string? technicalDetail)
    {
        Success = success;
        Value = value;
        ErrorKey = errorKey;
        ErrorArgs = errorArgs;
        TechnicalDetail = technicalDetail;
    }

    public static Result<T> Ok(T value) => new(true, value, string.Empty, Array.Empty<string>(), null);

    public static Result<T> Fail(string errorKey, params string[] args) =>
        new(false, default, errorKey, args, null);

    public static Result<T> FailTechnical(string errorKey, string technicalDetail, params string[] args) =>
        new(false, default, errorKey, args, technicalDetail);
}

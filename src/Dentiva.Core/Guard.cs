using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Dentiva.Core;

/// <summary>
/// Central argument/state validation helpers so failure messages stay
/// consistent and developer errors surface immediately.
/// </summary>
public static class Guard
{
    public static T NotNull<T>([NotNull] T? value, [CallerArgumentExpression(nameof(value))] string? name = null)
        where T : class
    {
        if (value is null)
        {
            throw new ArgumentNullException(name ?? "value");
        }

        return value;
    }

    public static string NotNullOrWhiteSpace([NotNull] string? value, [CallerArgumentExpression(nameof(value))] string? name = null)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A value is required.", name ?? "value");
        }

        return value;
    }

    public static void Against(bool condition, string message)
    {
        if (condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}

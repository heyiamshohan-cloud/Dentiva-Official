using System.Security.Cryptography;

namespace Dentiva.Core.Security;

/// <summary>
/// PBKDF2-SHA256 password hashing (OWASP-recommended parameters). Stored
/// format: PBKDF2-SHA256.{iterations}.{base64 salt}.{base64 key}. Verification
/// uses a fixed-time comparison. Plain-text passwords are never stored.
/// </summary>
public static class PasswordHasher
{
    public const int Iterations = 210_000;
    public const int SaltSizeBytes = 16;
    public const int KeySizeBytes = 32;
    private const string Prefix = "PBKDF2-SHA256";

    public static string Hash(string password)
    {
        Guard.NotNullOrWhiteSpace(password);
        var salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySizeBytes);
        return $"{Prefix}.{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(key)}";
    }

    public static bool Verify(string password, string stored)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(stored))
        {
            return false;
        }

        var parts = stored.Split('.');
        if (parts.Length != 4 || parts[0] != Prefix
            || !int.TryParse(parts[1], out var iterations) || iterations < 1)
        {
            return false;
        }

        byte[] salt, expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    /// <summary>Returns the estimated password strength 0..3 for setup feedback.</summary>
    public static int StrengthScore(string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return 0;
        }

        int score = 0;
        if (password.Length >= 8) score++;
        if (password.Length >= 12) score++;
        if (password.Any(char.IsDigit) && password.Any(char.IsLetter)) score++;
        if (password.Any(c => !char.IsLetterOrDigit(c))) score++;
        return Math.Min(score, 3);
    }
}

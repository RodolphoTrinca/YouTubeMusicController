using System.Security.Cryptography;
using System.Text;

namespace YtMusicController.Core.Configuration;

public static class TokenAuthenticator
{
    public static string GenerateToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    public static bool Validate(string? supplied, string expected)
    {
        if (string.IsNullOrWhiteSpace(supplied) || string.IsNullOrEmpty(expected))
            return false;

        var suppliedBytes = Encoding.UTF8.GetBytes(supplied);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        return suppliedBytes.Length == expectedBytes.Length &&
               CryptographicOperations.FixedTimeEquals(suppliedBytes, expectedBytes);
    }
}

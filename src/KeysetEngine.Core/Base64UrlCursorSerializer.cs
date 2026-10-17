using System.Text;
using System.Text.Json;

namespace KeysetEngine.Core;

/// <summary>
/// Provides Base64Url serialization for <see cref="KeysetCursorToken"/> as a fallback for testing
/// and development environments without active cryptographic providers.
/// </summary>
public static class Base64UrlCursorSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>
    /// Serializes a <see cref="KeysetCursorToken"/> into a URL-safe Base64 encoded JSON string.
    /// </summary>
    /// <param name="token">The token to serialize.</param>
    /// <returns>A URL-safe Base64 string.</returns>
    /// <exception cref="ArgumentNullException">Thrown if token is null.</exception>
    public static string Serialize(KeysetCursorToken token)
    {
        ArgumentNullException.ThrowIfNull(token);

        string json = JsonSerializer.Serialize(token, JsonOptions);
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        return ToBase64Url(bytes);
    }

    /// <summary>
    /// Deserializes a URL-safe Base64 encoded string into a <see cref="KeysetCursorToken"/>.
    /// Returns null if the cursor is null, empty, corrupt, or invalid JSON.
    /// </summary>
    /// <param name="opaqueCursor">The URL-safe Base64 encoded string.</param>
    /// <returns>The deserialized <see cref="KeysetCursorToken"/>, or null.</returns>
    public static KeysetCursorToken? Deserialize(string? opaqueCursor)
    {
        if (string.IsNullOrWhiteSpace(opaqueCursor))
        {
            return null;
        }

        try
        {
            byte[] bytes = FromBase64Url(opaqueCursor);
            return JsonSerializer.Deserialize<KeysetCursorToken>(bytes, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private static string ToBase64Url(byte[] bytes)
    {
        string base64 = Convert.ToBase64String(bytes);
        return base64
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static byte[] FromBase64Url(string base64Url)
    {
        string base64 = base64Url
            .Replace('-', '+')
            .Replace('_', '/');

        switch (base64.Length % 4)
        {
            case 2:
                base64 += "==";
                break;
            case 3:
                base64 += "=";
                break;
        }

        return Convert.FromBase64String(base64);
    }
}

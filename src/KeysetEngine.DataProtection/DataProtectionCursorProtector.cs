using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KeysetEngine.Core;
using Microsoft.AspNetCore.DataProtection;

namespace KeysetEngine.DataProtection;

/// <summary>
/// Cryptographic cursor protector that provides authenticated encryption, tamper detection,
/// and endpoint purpose isolation using ASP.NET Core Data Protection.
/// </summary>
public sealed class DataProtectionCursorProtector : ICursorProtector
{
    private const string BasePurpose = "KeysetEngine";
    private const string PurposeVersion = "v1";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly IDataProtectionProvider _dataProtectionProvider;

    /// <summary>
    /// Initializes a new instance of <see cref="DataProtectionCursorProtector"/>.
    /// </summary>
    /// <param name="dataProtectionProvider">The ASP.NET Core data protection provider.</param>
    public DataProtectionCursorProtector(IDataProtectionProvider dataProtectionProvider)
    {
        ArgumentNullException.ThrowIfNull(dataProtectionProvider);
        _dataProtectionProvider = dataProtectionProvider;
    }

    /// <inheritdoc />
    public string Protect(KeysetCursorToken token, string entityPurpose)
    {
        ArgumentNullException.ThrowIfNull(token);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityPurpose);

        IDataProtector protector = CreateProtector(entityPurpose);

        byte[] jsonBytes = JsonSerializer.SerializeToUtf8Bytes(token, JsonOptions);
        byte[] protectedBytes = protector.Protect(jsonBytes);

        return ToBase64Url(protectedBytes);
    }

    /// <inheritdoc />
    public KeysetCursorToken? Unprotect(string? opaqueCursor, string entityPurpose)
    {
        if (string.IsNullOrWhiteSpace(opaqueCursor) || string.IsNullOrWhiteSpace(entityPurpose))
        {
            return null;
        }

        try
        {
            IDataProtector protector = CreateProtector(entityPurpose);
            byte[] protectedBytes = FromBase64Url(opaqueCursor);
            byte[] unprotectedBytes = protector.Unprotect(protectedBytes);

            return JsonSerializer.Deserialize<KeysetCursorToken>(unprotectedBytes, JsonOptions);
        }
        catch (CryptographicException)
        {
            // Tampered, expired, or wrong purpose token
            return null;
        }
        catch (FormatException)
        {
            // Invalid Base64 payload
            return null;
        }
        catch (JsonException)
        {
            // Corrupt or incompatible token JSON
            return null;
        }
        catch (Exception)
        {
            // Defensive fallback for any unexpected decode failure
            return null;
        }
    }

    private IDataProtector CreateProtector(string entityPurpose)
    {
        return _dataProtectionProvider.CreateProtector(BasePurpose, entityPurpose, PurposeVersion);
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

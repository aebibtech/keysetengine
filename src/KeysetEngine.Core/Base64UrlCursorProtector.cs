namespace KeysetEngine.Core;

/// <summary>
/// A non-cryptographic implementation of <see cref="ICursorProtector"/> that uses <see cref="Base64UrlCursorSerializer"/>.
/// Suitable for testing, development, and unit test environments without ASP.NET Core Data Protection configured.
/// </summary>
public sealed class Base64UrlCursorProtector : ICursorProtector
{
    /// <inheritdoc />
    public string Protect(KeysetCursorToken token, string entityPurpose)
    {
        return Base64UrlCursorSerializer.Serialize(token);
    }

    /// <inheritdoc />
    public KeysetCursorToken? Unprotect(string? opaqueCursor, string entityPurpose)
    {
        return Base64UrlCursorSerializer.Deserialize(opaqueCursor);
    }
}

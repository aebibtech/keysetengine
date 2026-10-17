namespace KeysetEngine.Core;

/// <summary>
/// Contract for cryptographically protecting and unprotecting keyset cursor tokens.
/// </summary>
public interface ICursorProtector
{
    /// <summary>
    /// Encrypts and encodes a keyset cursor token into an opaque, tamper-evident string scoped to an entity purpose.
    /// </summary>
    /// <param name="token">The cursor token containing edge boundary coordinates.</param>
    /// <param name="entityPurpose">The purpose string isolating the token to a specific entity or endpoint.</param>
    /// <returns>An opaque string token suitable for public API consumption.</returns>
    string Protect(KeysetCursorToken token, string entityPurpose);

    /// <summary>
    /// Decodes and validates an opaque cursor token scoped to the specified entity purpose.
    /// </summary>
    /// <param name="opaqueCursor">The opaque cursor string submitted by the client.</param>
    /// <param name="entityPurpose">The expected entity purpose string.</param>
    /// <returns>The deserialized <see cref="KeysetCursorToken"/> if valid; otherwise null.</returns>
    KeysetCursorToken? Unprotect(string? opaqueCursor, string entityPurpose);
}

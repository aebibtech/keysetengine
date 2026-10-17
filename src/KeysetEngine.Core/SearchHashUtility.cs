using System.Security.Cryptography;
using System.Text;

namespace KeysetEngine.Core;

/// <summary>
/// Provides utility methods to generate and validate search term signatures.
/// Stale cursors minted under different search filters are detected via mismatching hashes.
/// </summary>
public static class SearchHashUtility
{
    /// <summary>
    /// Computes a deterministic compact hash of a search term.
    /// Returns null if the search term is null, empty, or whitespace.
    /// </summary>
    /// <param name="search">The search term to hash.</param>
    /// <returns>A hex string representing the search signature, or null.</returns>
    public static string? ComputeHash(string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return null;
        }

        // Normalize by trimming
        byte[] bytes = Encoding.UTF8.GetBytes(search.Trim());
        byte[] hashBytes = SHA256.HashData(bytes);

        // Truncate to 16 hex chars (8 bytes) for compact cursor payload
        return Convert.ToHexString(hashBytes, 0, 8).ToLowerInvariant();
    }

    /// <summary>
    /// Verifies whether the provided search term matches the cursor's recorded search hash.
    /// </summary>
    /// <param name="search">The active search query parameter.</param>
    /// <param name="expectedHash">The hash stored in the cursor token.</param>
    /// <returns>True if the active search corresponds to the cursor's search context; otherwise false.</returns>
    public static bool Matches(string? search, string? expectedHash)
    {
        string? activeHash = ComputeHash(search);
        return string.Equals(activeHash, expectedHash, StringComparison.OrdinalIgnoreCase);
    }
}

namespace KeysetEngine.Core;

/// <summary>
/// Strongly-typed cursor token capturing boundary values for keyset pagination.
/// </summary>
/// <param name="SortValueJson">Serialized JSON literal of the sorted property value at the edge.</param>
/// <param name="TieBreakerValueJson">Serialized JSON literal of the primary key / unique tie-breaker property value.</param>
/// <param name="IsAscending">True if the original query sort order was ascending; false if descending.</param>
/// <param name="SearchHash">Optional hash of the search term active when this cursor was minted.</param>
public sealed record KeysetCursorToken(
    string SortValueJson,
    string TieBreakerValueJson,
    bool IsAscending,
    string? SearchHash
);

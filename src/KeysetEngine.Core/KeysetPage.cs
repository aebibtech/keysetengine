namespace KeysetEngine.Core;

/// <summary>
/// Envelope containing paginated items and edge cursor tokens.
/// </summary>
/// <typeparam name="T">The entity or projected DTO type.</typeparam>
/// <param name="Items">The items belonging to the current page slice.</param>
/// <param name="StartCursor">The opaque cursor representing the first item of this page slice, or null if empty.</param>
/// <param name="EndCursor">The opaque cursor representing the last item of this page slice, or null if empty.</param>
/// <param name="HasNextPage">Indicates if subsequent pages exist beyond the EndCursor in the forward direction.</param>
/// <param name="HasPreviousPage">Indicates if preceding pages exist prior to the StartCursor in the backward direction.</param>
public sealed record KeysetPage<T>(
    IReadOnlyList<T> Items,
    string? StartCursor,
    string? EndCursor,
    bool HasNextPage,
    bool HasPreviousPage
);

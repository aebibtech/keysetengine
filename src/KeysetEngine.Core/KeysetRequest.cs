namespace KeysetEngine.Core;

/// <summary>
/// Encapsulates incoming pagination parameters for keyset-based query execution.
/// </summary>
/// <param name="Cursor">The opaque cursor token identifying the edge of the previous page.</param>
/// <param name="Direction">The navigation direction (Forward or Backward).</param>
/// <param name="Limit">The maximum number of items requested per page.</param>
/// <param name="Search">The optional search term to filter results.</param>
public sealed record KeysetRequest(
    string? Cursor = null,
    PageDirection Direction = PageDirection.Forward,
    int Limit = 20,
    string? Search = null
);

using System.Text.Json;
using KeysetEngine.Core.Configuration;
using KeysetEngine.Core.Expressions;
using Microsoft.EntityFrameworkCore;

namespace KeysetEngine.Core;

/// <summary>
/// Provides extension methods for performing keyset-based pagination and search on <see cref="IQueryable{T}"/>.
/// </summary>
public static class KeysetQueryableExtensions
{
    /// <summary>
    /// Executes keyset pagination on an <see cref="IQueryable{T}"/>, applying full-text search,
    /// dynamic keyset boundary predicates, bidirectional ordering, and N+1 slice evaluation.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="source">The source queryable.</param>
    /// <param name="request">The incoming pagination request.</param>
    /// <param name="config">The entity's keyset configuration.</param>
    /// <param name="cursorProtector">The cursor encryption/serialization provider.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="KeysetPage{T}"/> envelope containing the page slice and edge boundary cursors.</returns>
    public static async Task<KeysetPage<T>> ToKeysetPageAsync<T>(
        this IQueryable<T> source,
        KeysetRequest request,
        KeysetConfig<T> config,
        ICursorProtector cursorProtector,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(cursorProtector);

        int limit = request.Limit <= 0 ? 20 : request.Limit;
        string entityPurpose = config.EntityPurpose;

        // 1. Dynamic Text Search
        var searchPredicate = KeysetExpressionBuilder.ApplySearch<T>(
            config.SearchSelectors,
            request.Search,
            config.SearchStrategy);

        if (searchPredicate is not null)
        {
            source = source.Where(searchPredicate);
        }

        string? currentSearchHash = SearchHashUtility.ComputeHash(request.Search);

        // 2. Keyset Boundary Invalidation & Predicate Formulation
        KeysetCursorToken? cursorToken = null;
        if (!string.IsNullOrWhiteSpace(request.Cursor))
        {
            cursorToken = cursorProtector.Unprotect(request.Cursor, entityPurpose);

            // Stale cursor detection: if search filter changed between page requests, invalidate cursor and reset to Page 1
            if (cursorToken is not null && !SearchHashUtility.Matches(request.Search, cursorToken.SearchHash))
            {
                cursorToken = null;
            }

            if (cursorToken is not null)
            {
                var boundaryPredicate = KeysetExpressionBuilder.BuildPredicate<T>(
                    config.SortSelector,
                    config.TieBreakerSelector,
                    cursorToken,
                    request.Direction);

                source = source.Where(boundaryPredicate);
            }
        }

        // 3. Dynamic Ordering with Automatic Bidirectional Inversion
        source = DynamicOrderHelper.ApplyOrdering(
            source,
            config.SortSelector,
            config.TieBreakerSelector,
            config.IsAscending,
            request.Direction);

        // 4. Fetch N + 1 items to determine hasNextPage / hasPreviousPage
        source = source.Take(limit + 1);

        List<T> items = await ExecuteQueryToListAsync(source, cancellationToken).ConfigureAwait(false);

        // 5. Slice Evaluation & In-Memory Reversal
        bool hasMoreInDirection = items.Count > limit;
        if (hasMoreInDirection)
        {
            items.RemoveAt(items.Count - 1);
        }

        bool hasNextPage;
        bool hasPreviousPage;

        if (request.Direction == PageDirection.Forward)
        {
            hasPreviousPage = cursorToken is not null;
            hasNextPage = hasMoreInDirection;
        }
        else
        {
            // Backward traversal: items were queried in reversed SQL order
            hasPreviousPage = hasMoreInDirection;
            hasNextPage = true;
            items.Reverse(); // Restore natural forward sequence
        }

        // 6. Cryptographic Edge Cursor Generation
        string? startCursor = null;
        string? endCursor = null;

        if (items.Count > 0)
        {
            T firstItem = items[0];
            object firstSortVal = config.GetSortValue(firstItem);
            object firstTieVal = config.GetTieBreakerValue(firstItem);

            var startToken = new KeysetCursorToken(
                SortValueJson: JsonSerializer.Serialize(firstSortVal),
                TieBreakerValueJson: JsonSerializer.Serialize(firstTieVal),
                IsAscending: config.IsAscending,
                SearchHash: currentSearchHash);

            startCursor = cursorProtector.Protect(startToken, entityPurpose);

            T lastItem = items[^1];
            object lastSortVal = config.GetSortValue(lastItem);
            object lastTieVal = config.GetTieBreakerValue(lastItem);

            var endToken = new KeysetCursorToken(
                SortValueJson: JsonSerializer.Serialize(lastSortVal),
                TieBreakerValueJson: JsonSerializer.Serialize(lastTieVal),
                IsAscending: config.IsAscending,
                SearchHash: currentSearchHash);

            endCursor = cursorProtector.Protect(endToken, entityPurpose);
        }

        return new KeysetPage<T>(
            Items: items.AsReadOnly(),
            StartCursor: startCursor,
            EndCursor: endCursor,
            HasNextPage: hasNextPage,
            HasPreviousPage: hasPreviousPage);
    }

    private static async Task<List<T>> ExecuteQueryToListAsync<T>(IQueryable<T> source, CancellationToken ct)
    {
        // Support EF Core asynchronous execution while allowing in-memory LINQ execution in unit tests
        if (source is IAsyncEnumerable<T>)
        {
            return await EntityFrameworkQueryableExtensions.ToListAsync(source, ct).ConfigureAwait(false);
        }

        return source.ToList();
    }
}

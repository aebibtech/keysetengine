# Dynamic Search & Stale Cursors

::: tip Direct Answer (Executive Summary)
KeysetEngine enables multi-column full-text search across entities (`builder.SearchIn(...)`) while preserving keyset boundaries. To prevent corrupted page offsets when users change filters mid-pagination, KeysetEngine embeds a search hash in every cursor token via `SearchHashUtility`; when the search term changes, the cursor is automatically invalidated and safely restarts from Page 1.
:::

KeysetEngine combines dynamic multi-column text search with keyset pagination while preventing stale cursor mismatches and client-side evaluation warnings.

---

## Configuring Search Fields

Use `SearchIn()` on `KeysetConfigBuilder<T>` to declare which string properties should be included in full-text filtering:

```csharp
var config = new KeysetConfigBuilder<Product>()
    .SortBy(x => x.CreatedAt, ascending: false)
    .TieBreakBy(x => x.Id)
    .SearchIn(
        x => x.Name,
        x => x.Sku,
        x => x.Description)
    .Build();
```

When a user provides a search term in `KeysetRequest.Search`:

```http
GET /api/products?search=wireless&limit=20 HTTP/1.1
```

KeysetEngine constructs an `Expression.OrElse` tree combining all configured property selectors:

```sql
WHERE (
    LOWER("Name") LIKE '%wireless%' OR
    LOWER("Sku") LIKE '%wireless%' OR
    LOWER("Description") LIKE '%wireless%'
)
AND (
    ("CreatedAt" < @p0) OR ("CreatedAt" = @p0 AND "Id" < @p1)
)
```

---

## The Stale Cursor Problem

In interactive web applications, users frequently type new search queries while holding an active cursor token from a previous search.

### The Danger:
If an application blindly applies a cursor from `search="laptop"` to a new query with `search="keyboard"`, the cursor boundary points to an arbitrary point in the newly filtered dataset, skipping items or returning inconsistent results.

---

## Automatic Invalidation with `SearchHashUtility`

KeysetEngine embeds a cryptographic query signature hash into every emitted cursor token:

```csharp
public sealed record KeysetCursorToken(
    string SortValueJson,
    string TieBreakerValueJson,
    bool IsAscending,
    string? SearchHash // Non-cryptographic hash of current search term
);
```

### Invalidation Algorithm
When processing incoming requests in `ToKeysetPageAsync`:
1. The incoming `request.Search` is hashed.
2. The cursor token's `SearchHash` is compared against the current search hash using `SearchHashUtility.Matches()`.
3. **If the search term changed**, KeysetEngine automatically discards the cursor and restarts cleanly from **Page 1**:

```csharp
// Stale cursor detection in KeysetQueryableExtensions
if (cursorToken is not null && !SearchHashUtility.Matches(request.Search, cursorToken.SearchHash))
{
    cursorToken = null; // Seamlessly reset to Page 1
}
```

::: tip Safe & Resilient
Users never encounter 500 exceptions, corrupted pages, or out-of-order data when rapidly modifying search filters.
:::

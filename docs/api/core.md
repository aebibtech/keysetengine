# KeysetEngine.Core API Reference

Namespace: `KeysetEngine.Core` & `KeysetEngine.Core.Configuration`

---

## Records

### `KeysetRequest`
Encapsulates incoming pagination and search query parameters.

```csharp
public sealed record KeysetRequest(
    string? Cursor = null,
    PageDirection Direction = PageDirection.Forward,
    int Limit = 20,
    string? Search = null
);
```

| Parameter | Type | Default | Description |
| :--- | :--- | :--- | :--- |
| `Cursor` | `string?` | `null` | The opaque cursor token identifying the page boundary. |
| `Direction` | `PageDirection` | `PageDirection.Forward` | `PageDirection.Forward` or `PageDirection.Backward`. |
| `Limit` | `int` | `20` | Maximum number of items requested per page (minimum 1). |
| `Search` | `string?` | `null` | Optional text search term to filter results. |

---

### `KeysetPage<T>`
Immutable envelope returned by `ToKeysetPageAsync`.

```csharp
public sealed record KeysetPage<T>(
    IReadOnlyList<T> Items,
    string? StartCursor,
    string? EndCursor,
    bool HasNextPage,
    bool HasPreviousPage
);
```

| Property | Type | Description |
| :--- | :--- | :--- |
| `Items` | `IReadOnlyList<T>` | The requested page slice in natural chronological order. |
| `StartCursor` | `string?` | Cursor identifying the first item in the slice. |
| `EndCursor` | `string?` | Cursor identifying the last item in the slice. |
| `HasNextPage` | `bool` | True if more items exist in the forward direction. |
| `HasPreviousPage` | `bool` | True if preceding items exist in the backward direction. |

---

### `KeysetCursorToken`
Internal data contract serialized within opaque cursor tokens.

```csharp
public sealed record KeysetCursorToken(
    string SortValueJson,
    string TieBreakerValueJson,
    bool IsAscending,
    string? SearchHash
);
```

---

## Enums

### `PageDirection`
```csharp
public enum PageDirection
{
    Forward,
    Backward
}
```

---

## Classes & Builders

### `KeysetConfigBuilder<T>`
Fluent builder for constructing immutable entity keyset configurations.

#### Methods
- **`SortBy<TSort>(Expression<Func<T, TSort>> sortSelector, bool ascending = true)`**: Configures the primary sort column and direction. Validates that the property is non-nullable.
- **`TieBreakBy<TTie>(Expression<Func<T, TTie>> tieBreakerSelector)`**: Configures the unique tie-breaker property (typically primary key). Validates that the property is non-nullable.
- **`SearchIn(params Expression<Func<T, string?>>[] searchSelectors)`**: Configures string properties to include in dynamic text search.
- **`UseSearchStrategy(ISearchStrategy searchStrategy)`**: Overrides the default search strategy for this entity.
- **`WithEntityPurpose(string purpose)`**: Sets a custom purpose string for Data Protection isolation.
- **`Build()`**: Validates configuration invariants and returns the compiled `KeysetConfig<T>`.

---

### `KeysetQueryableExtensions`

#### Extension Methods
```csharp
public static Task<KeysetPage<T>> ToKeysetPageAsync<T>(
    this IQueryable<T> source,
    KeysetRequest request,
    ICursorProtector cursorProtector,
    CancellationToken cancellationToken = default);

public static Task<KeysetPage<T>> ToKeysetPageAsync<T>(
    this IQueryable<T> source,
    KeysetRequest request,
    KeysetConfig<T> config,
    ICursorProtector cursorProtector,
    CancellationToken cancellationToken = default);
```

---

### `KeysetServiceCollectionExtensions`

```csharp
public static IServiceCollection AddKeysetPagination(
    this IServiceCollection services,
    Action<KeysetEngineOptions>? configure = null);
```

Registers entity configurations in `KeysetConfigRegistry` and sets application-wide search options.

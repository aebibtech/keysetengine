# Pipeline & Expressions

::: tip Direct Answer (Executive Summary)
KeysetEngine compiles pagination queries dynamically into database-native LINQ expressions in five stages: (1) Decrypt cursor with `ICursorProtector`, (2) Append multi-column search predicate, (3) Append universal nested boolean keyset inequality (`(SortCol > val) OR (SortCol = val AND Id > tieVal)`), (4) Apply dynamic `OrderBy/ThenBy` with bidirectional inversion, and (5) Slice with `.Take(limit + 1)` to evaluate `HasNextPage` without running `COUNT(*)`.
:::

KeysetEngine translates incoming pagination requests and configuration definitions into pure, index-seekable LINQ expression trees without triggering EF Core client-side evaluation.

---

## High-Level Execution Lifecycle

```
[ HTTP Request (Cursor, Limit, Direction, Search) ]
                         │
                         ▼
             [ ICursorProtector ]
         (Decrypt & Validate Opaque Token)
                         │
                         ▼
        [ Dynamic Text Search Predicate ]
            (EF.Functions or Lower/Contains)
                         │
                         ▼
        [ Keyset Inequality Predicate ]
         (Universal Nested Boolean Tree)
                         │
                         ▼
       [ Dynamic OrderBy / ThenBy Inversion ]
     (Inverted if Direction == Backward)
                         │
                         ▼
               [ Take(limit + 1) ]
       (Single SQL Roundtrip to Database)
                         │
                         ▼
        [ Slice Evaluation & In-Memory Reversal ]
    (Restore chronological order if Backward)
                         │
                         ▼
        [ Cryptographic Edge Cursor Generation ]
           (startCursor & endCursor Tokens)
                         │
                         ▼
               [ KeysetPage<T> ]
```

---

## 1. Universal Keyset Inequalities

When a valid cursor token is supplied, `KeysetExpressionBuilder` dynamically constructs an `Expression<Func<T, bool>>` representing the boundary condition:

### Ascending Order
- **Forward Traversal**:
  ```csharp
  x => (x.SortCol > sortVal) || (x.SortCol == sortVal && x.TieBreaker > tieVal)
  ```
- **Backward Traversal**:
  ```csharp
  x => (x.SortCol < sortVal) || (x.SortCol == sortVal && x.TieBreaker < tieVal)
  ```

### Descending Order
- **Forward Traversal**:
  ```csharp
  x => (x.SortCol < sortVal) || (x.SortCol == sortVal && x.TieBreaker < tieVal)
  ```
- **Backward Traversal**:
  ```csharp
  x => (x.SortCol > sortVal) || (x.SortCol == sortVal && x.TieBreaker > tieVal)
  ```

### Invariant Property Typing
`KeysetExpressionBuilder` automatically detects the underlying runtime types of `SortProperty` and `TieBreakerProperty` (including primitives, `DateTime`, `DateTimeOffset`, `DateOnly`, `Guid`, and `string`), deserializes JSON values from the token into strongly-typed constants, and invokes `Expression.GreaterThan`, `Expression.LessThan`, and `Expression.Equal` appropriately.

---

## 2. Dynamic Ordering Helper

`DynamicOrderHelper` dynamically constructs `IQueryable<T>.OrderBy`, `OrderByDescending`, `ThenBy`, and `ThenByDescending` calls using reflection and expression compilation.

### Automatic Direction Inversion
When traversing **backward**, the SQL `ORDER BY` clause must be inverted so the database scans in reverse order from the cursor boundary:

| Config Direction | Requested Navigation | Emitted SQL Order |
| :--- | :--- | :--- |
| Ascending (`true`) | `PageDirection.Forward` | `ORDER BY SortCol ASC, TieBreaker ASC` |
| Ascending (`true`) | `PageDirection.Backward` | `ORDER BY SortCol DESC, TieBreaker DESC` |
| Descending (`false`) | `PageDirection.Forward` | `ORDER BY SortCol DESC, TieBreaker DESC` |
| Descending (`false`) | `PageDirection.Backward` | `ORDER BY SortCol ASC, TieBreaker ASC` |

---

## 3. The $N + 1$ Slicing Strategy

Standard pagination often requires a separate `COUNT(*)` query to determine whether subsequent pages exist. On large tables, `COUNT(*)` is an expensive full-table or full-index scan.

KeysetEngine completely avoids `COUNT(*)` by requesting **$N + 1$** items:

```csharp
source = source.Take(limit + 1);
List<T> items = await source.ToListAsync(cancellationToken);
```

### Page Boundary Detection
- If `items.Count > limit`, a subsequent page exists. KeysetEngine removes the extra item before returning the slice.
- `HasNextPage` and `HasPreviousPage` are calculated immediately based on traversal direction and cursor presence.

```csharp
bool hasMoreInDirection = items.Count > limit;
if (hasMoreInDirection)
{
    items.RemoveAt(items.Count - 1);
}

if (request.Direction == PageDirection.Forward)
{
    hasPreviousPage = cursorToken is not null;
    hasNextPage = hasMoreInDirection;
}
else
{
    hasPreviousPage = hasMoreInDirection;
    hasNextPage = true;
    items.Reverse(); // In-memory reversal
}
```

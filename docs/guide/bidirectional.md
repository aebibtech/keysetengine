# Bidirectional Traversal

KeysetEngine natively supports bidirectional traversal (moving forward to next pages and backward to previous pages) without storing server-side state or sessions.

---

## The Navigation Model

Every `KeysetPage<T>` returns two edge boundary tokens:
- **`StartCursor`**: Represents the first item in the current page slice.
- **`EndCursor`**: Represents the last item in the current page slice.

```
                  ┌───────────────────────────────┐
                  │          Current Page         │
                  │ [Item 1] ... [Item 20]        │
                  └──────┬────────────────┬───────┘
                         │                │
                    StartCursor        EndCursor
                         │                │
        Direction: Backward               Direction: Forward
                 │                                │
                 ▼                                ▼
       ┌───────────────────┐            ┌───────────────────┐
       │   Previous Page   │            │     Next Page     │
       └───────────────────┘            └───────────────────┘
```

---

## Moving Forward

To fetch the next page of results, pass the `EndCursor` and specify `PageDirection.Forward` (the default):

```http
GET /api/orders?cursor={EndCursor}&direction=Forward&limit=20 HTTP/1.1
```

KeysetEngine applies:
1. Inequality predicate seeking items *after* the `EndCursor`.
2. Standard SQL `ORDER BY` matching your entity configuration.
3. Takes `limit + 1` items in natural forward sequence.

---

## Moving Backward

When the user clicks "Previous Page", send the `StartCursor` and specify `PageDirection.Backward`:

```http
GET /api/orders?cursor={StartCursor}&direction=Backward&limit=20 HTTP/1.1
```

### The Inversion & In-Memory Reversal Process
To navigate backward efficiently without rescanning from the beginning of the table:
1. **Inequality Inversion**: The comparison operator is inverted ($< \rightarrow >$ or $> \rightarrow <$) to look backwards from `StartCursor`.
2. **SQL `ORDER BY` Inversion**: The query orders items backwards from the cursor edge to retrieve the preceding 20 items.
3. **In-Memory Reversal**: Because the database returned the items in reverse chronological order, KeysetEngine automatically invokes `.Reverse()` on the returned list before returning `KeysetPage<T>`.

::: tip Zero Client-Side Complexity
Your frontend or consumer application always receives items in natural, expected order (e.g., newest to oldest). You never need to sort or reverse items on the client.
:::

---

## Full Traversal Loop Example

```csharp
// 1. Fetch First Page
var page1 = await db.Orders.ToKeysetPageAsync(
    new KeysetRequest(Limit: 20),
    config,
    protector);

// 2. Fetch Second Page (Forward)
var page2 = await db.Orders.ToKeysetPageAsync(
    new KeysetRequest(Cursor: page1.EndCursor, Direction: PageDirection.Forward, Limit: 20),
    config,
    protector);

// 3. Navigate back to First Page (Backward)
var backToPage1 = await db.Orders.ToKeysetPageAsync(
    new KeysetRequest(Cursor: page2.StartCursor, Direction: PageDirection.Backward, Limit: 20),
    config,
    protector);

// Assert: backToPage1.Items contains identical entities as page1.Items in exact same order!
```

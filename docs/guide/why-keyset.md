# Why Keyset Pagination?

::: tip Direct Answer (Executive Summary)
**SQL OFFSET degrades linearly $O(N)$** because database engines must scan and discard all preceding rows (e.g. `OFFSET 100000` scans 100,020 rows), causing high latency, CPU spikes, and data drift during concurrent writes. **Keyset pagination runs in constant time $O(1)$** by seeking directly to index boundaries via values (`WHERE CreatedAt < @lastDate`). KeysetEngine eliminates SQL OFFSET performance cliffs while preventing missing or duplicate rows.
:::

Traditional offset-based pagination (using `.Skip(page * pageSize).Take(pageSize)` in LINQ or `OFFSET x ROWS FETCH NEXT y ROWS` in SQL) suffers from fatal scalability and concurrency issues when querying modern, high-volume datasets.

---

## The $O(N)$ Pitfall of SQL `OFFSET`

Relational database engines (PostgreSQL, SQL Server, MySQL, SQLite) do not have random-access memory address pointers to arbitrary row offsets. When you execute:

```sql
SELECT * FROM "Orders"
ORDER BY "CreatedAt" DESC
OFFSET 100000 ROWS FETCH NEXT 20 ROWS ONLY;
```

The database query engine must:
1. Traverse the index (or scan the entire table) for **100,020 rows**.
2. Sort and evaluate all 100,020 rows in memory or disk worktables.
3. Discard the first 100,000 rows.
4. Return only the final 20 rows to the client.

As the offset increases, query execution time scales linearly $O(N)$, causing severe latency spikes, buffer cache thrashing, and high database CPU utilization.

```
Execution Time vs. Page Depth
Latency (ms)
    ▲
500 ┼                                        ● Offset (Degrades O(N))
400 ┼                                   ●
300 ┼                             ●
200 ┼                       ●
100 ┼                 ●
 10 ┼───────────────●──────────────────────── Keyset Engine (Stable O(1))
    └────────────────────────────────────────► Page Depth / Offset
      1     1,000   10,000  50,000  100,000
```

---

## Keyset Pagination: The $O(1)$ Indexed Seek

Keyset pagination (also known as the **cursor pattern** or **seek method**) replaces arbitrary numerical offsets with value-based boundary conditions based on the sorted values of the last retrieved item.

Instead of asking the database to count past 100,000 rows, KeysetEngine asks:
> *"Give me the next 20 rows that appear immediately after the item with `CreatedAt = '2026-10-17 12:00:00'` and `Id = 45981'`."*

The resulting SQL query directly uses a composite index seek:

```sql
SELECT * FROM "Orders"
WHERE ("CreatedAt" < @p0) OR ("CreatedAt" = @p0 AND "Id" < @p1)
ORDER BY "CreatedAt" DESC, "Id" DESC
FETCH NEXT 21 ROWS ONLY;
```

Because the database engine navigates directly to the specific leaf node in the B-Tree index, **execution time is virtually identical whether querying row 0 or row 10,000,000**.

---

## Real-World Concurrency: The Missing / Duplicate Rows Bug

Consider what happens in an offset-paginated system when new rows are inserted or deleted while a user is browsing:

1. A user views Page 1 (rows 1–20).
2. While reading, 5 new orders are inserted at the top of the table.
3. The user navigates to Page 2 (`OFFSET 20`).
4. **Result:** The user sees rows 16–20 again! Rows shift down, resulting in duplicate or missed records.

**Keyset pagination is immune to this issue.** Because boundaries are pinned to immutable edge values, newly inserted rows never cause existing items to shift between pages.

---

## Why Not SQL Row-Value Tuples `(a, b) > (x, y)`?

Some SQL dialects (PostgreSQL and SQLite) support row-value comparison syntax:

```sql
WHERE ("CreatedAt", "Id") < (@val1, @val2)
```

However:
1. **SQL Server does NOT support SQL row-value tuple comparisons.**
2. **Entity Framework Core cannot reliably translate C# tuple expressions** across different relational database providers, often leading to client-side evaluation or unindexed nested subqueries.

### KeysetEngine's Solution: Universal Nested Booleans

KeysetEngine generates portable, index-seekable boolean disjunction trees:

$$\text{Forward Ascending} = (SortCol > val) \lor (SortCol == val \land TieBreaker > tieVal)$$
$$\text{Forward Descending} = (SortCol < val) \lor (SortCol == val \land TieBreaker < tieVal)$$

This generates identical, optimal execution plans across **PostgreSQL**, **Microsoft SQL Server**, **SQLite**, and **MySQL**.

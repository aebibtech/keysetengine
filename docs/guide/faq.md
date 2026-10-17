# Frequently Asked Questions (FAQ)

Direct, comprehensive answers to common questions about **KeysetEngine**, keyset pagination mechanics, security, database compatibility, and troubleshooting.

---

## General Concepts & Architecture

### What is Keyset Pagination and how does it differ from SQL OFFSET?
Keyset pagination (also called the cursor or seek method) avoids numeric row offsets by filtering queries based on the values of the last record seen (e.g. `WHERE CreatedAt < @lastDate OR (CreatedAt = @lastDate AND Id < @lastId)`). 

Unlike SQL `OFFSET 100000`, which requires the database engine to scan and discard 100,000 rows ($O(N)$ execution), keyset pagination performs a direct B-tree **Index Seek** ($O(1)$ execution), maintaining sub-millisecond query latency regardless of table size.

---

### Why does SQL OFFSET degrade on large tables?
Relational databases do not store physical memory pointers to arbitrary row offsets. When `OFFSET 50000 FETCH NEXT 20` is executed, the database engine must traverse the index, evaluate 50,020 rows, hold or sort them in temporary buffers, discard the first 50,000, and return only 20. This causes linear performance degradation, high CPU utilization, and buffer pool churn.

---

### Why does KeysetEngine use nested boolean disjunctions instead of SQL row-value syntax `(a, b) > (x, y)`?
While PostgreSQL and SQLite support SQL row-value comparisons:
```sql
WHERE ("CreatedAt", "Id") < (@p0, @p1)
```
**Microsoft SQL Server does not support row-value comparison syntax.** Furthermore, Entity Framework Core relational translators cannot reliably map C# tuple comparisons to SQL row values across database providers without causing client-side evaluation warnings.

KeysetEngine generates universal nested boolean trees:
```csharp
(SortCol > val) || (SortCol == val && TieBreaker > tieVal)
```
which translate identically and cleanly into index seeks across **PostgreSQL**, **SQL Server**, **SQLite**, **MySQL**, and **Oracle**.

---

### How does the $N+1$ slicing algorithm eliminate `COUNT(*)` queries?
Standard pagination UI often runs a separate `SELECT COUNT(*) FROM Table` query to determine if next pages exist or to display page counts. On large tables, `COUNT(*)` causes expensive sequential scans.

KeysetEngine queries `Take(limit + 1)`. If the database returns `limit + 1` records, KeysetEngine knows immediately that a subsequent page exists, removes the extra item from the slice, sets `HasNextPage = true`, and returns the page—all in **a single database roundtrip**.

---

## Traversal & Security

### How does backward pagination work without extra database roundtrips?
When traversing backward (`PageDirection.Backward`):
1. KeysetEngine inverts comparison operators ($< \rightarrow >$ or $> \rightarrow <$).
2. KeysetEngine inverts the SQL `ORDER BY` clause so the database seeks backward from the start cursor.
3. Once the database returns the preceding slice, KeysetEngine performs an **in-memory reversal** (`items.Reverse()`) before returning `KeysetPage<T>`.

The consumer application always receives items in natural, chronological order without any client-side sorting required.

---

### How does KeysetEngine protect cursor tokens against tampering and ID enumeration?
`KeysetEngine.DataProtection` uses **ASP.NET Core Data Protection** to encrypt and cryptographically sign every cursor token using authenticated encryption (AES-256-GCM / HMAC-SHA256).

- **Purpose Isolation**: Tokens are scoped with hierarchical sub-purposes: `KeysetEngine:{entityPurpose}:v1`. A token created for `/api/orders` cannot be decrypted or reused on `/api/customers`.
- **Tenant Isolation**: Developers can append tenant IDs to `entityPurpose` to prevent cross-tenant token replay attacks.
- **Graceful Fallback**: If an attacker tampers with a token, or if a token is malformed or expired, `DataProtectionCursorProtector` catches cryptographic exceptions safely and returns `null`. KeysetEngine treats this as a fresh query and returns Page 1, preventing 500 errors.

---

### What happens if a user submits an active cursor after modifying their search term? (Stale Cursor Invalidation)
When users paginate through search results, they may change their query text while holding a cursor from a previous search. Applying an old cursor to a new filter skips records or yields invalid boundaries.

KeysetEngine calculates a query signature hash (`SearchHashUtility`) and embeds it within the cursor token. If `request.Search` differs from the token's `SearchHash`, KeysetEngine **automatically invalidates the cursor and restarts cleanly from Page 1**.

---

## Databases & Performance

### What database indexes are required for keyset pagination?
To achieve $O(1)$ index seek performance, you must create a **composite B-tree index** covering both the sort column and the tie-breaker:

```csharp
// EF Core Fluent API
modelBuilder.Entity<Order>()
    .HasIndex(o => new { o.CreatedAt, o.Id })
    .HasDatabaseName("IX_Orders_CreatedAt_Id");
```

For multi-tenant systems, place the tenant partition column first:
```csharp
modelBuilder.Entity<Order>()
    .HasIndex(o => new { o.TenantId, o.CreatedAt, o.Id });
```

---

### How do I use PostgreSQL-native ILIKE or tsvector full-text search?
Install `KeysetEngine.PostgreSql` and register the strategy in `Program.cs`:

```csharp
builder.Services.AddKeysetPagination(options =>
{
    // Use EF.Functions.ILike (pairs with pg_trgm GIN indexes)
    options.UsePostgreSqlSearch();

    // Or use PostgreSQL tsvector full-text search
    // options.UsePostgreSqlTsVectorSearch(regConfig: "english");
});
```

---

### How do I use SQL Server Full-Text Search?
Install `KeysetEngine.SqlServer` and register the strategy in `Program.cs`:

```csharp
builder.Services.AddKeysetPagination(options =>
{
    // Uses EF.Functions.FreeText() or EF.Functions.Contains()
    options.UseSqlServerFullTextSearch(useContains: false);

    // Or use collation-aware LIKE search (avoids ToLower())
    // options.UseSqlServerCollationSearch();
});
```

---

### How do I unit test KeysetEngine without ASP.NET Core Data Protection?
Use `Base64UrlCursorProtector.Instance` from `KeysetEngine.Core`:

```csharp
var page = await db.Orders.ToKeysetPageAsync(
    request,
    config,
    Base64UrlCursorProtector.Instance);
```
This performs simple URL-safe serialization without requiring cryptographic key storage during tests.

---

### Which .NET and Entity Framework Core versions are supported?
KeysetEngine multi-targets:
- **.NET 8.0 (LTS)** with EF Core 8.x
- **.NET 9.0 (Current)** with EF Core 9.x
- **.NET 10.0 (LTS)** with EF Core 10.x

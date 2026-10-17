# PostgreSQL Search Adapter

`KeysetEngine.PostgreSql` provides PostgreSQL-optimized search strategies utilizing `EF.Functions.ILike` and PostgreSQL's built-in `tsvector` / `tsquery` full-text search engine.

---

## Installation

```bash
dotnet add package KeysetEngine.PostgreSql
```

---

## 1. Case-Insensitive Pattern Search (`ILike`)

By default, universal search emits `ToLower().Contains()`, which can prevent index usage in PostgreSQL.

`PostgreSqlSearchStrategy` translates property searches into PostgreSQL's native `ILIKE` operator:

```csharp
// Program.cs
builder.Services.AddKeysetPagination(options =>
{
    // Set ILIKE as the default search strategy across all entities
    options.UsePostgreSqlSearch();

    options.Configure<Customer>(b => b
        .SortBy(x => x.CreatedAt)
        .TieBreakBy(x => x.Id)
        .SearchIn(x => x.FullName, x => x.Email));
});
```

### Generated SQL
```sql
WHERE ("FullName" ILIKE ('%' || @term || '%')) OR ("Email" ILIKE ('%' || @term || '%'))
```

### PostgreSQL Index Recommendation: `pg_trgm` GIN Index
To accelerate substring searches with `ILIKE`, enable the `pg_trgm` extension and add GIN trigram indexes:

```sql
CREATE EXTENSION IF NOT EXISTS pg_trgm;

CREATE INDEX "IX_Customers_FullName_Trgm" ON "Customers" USING GIN ("FullName" gin_trgm_ops);
CREATE INDEX "IX_Customers_Email_Trgm" ON "Customers" USING GIN ("Email" gin_trgm_ops);
```

---

## 2. Full-Text Search with `tsvector`

For advanced full-text linguistic matching (stemming, dictionaries, stop words), use `UsePostgreSqlTsVectorSearch()`:

```csharp
// Program.cs
builder.Services.AddKeysetPagination(options =>
{
    // Configures tsvector search with the "english" dictionary (default)
    options.UsePostgreSqlTsVectorSearch(regConfig: "english");
});
```

### Generated SQL
```sql
WHERE to_tsvector('english', "FullName") @@ plainto_tsquery('english', @term)
```

### PostgreSQL GIN Index for `to_tsvector`
```sql
CREATE INDEX "IX_Customers_FullName_FTS" 
ON "Customers" USING GIN (to_tsvector('english', "FullName"));
```

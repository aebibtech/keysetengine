# SQL Server Search Adapter

`KeysetEngine.SqlServer` provides Microsoft SQL Server-optimized search strategies utilizing SQL Server Full-Text Search (`FREETEXT` / `CONTAINS`) and default collation-aware filtering.

---

## Installation

```bash
dotnet add package KeysetEngine.SqlServer
```

---

## 1. Full-Text Search Strategy (`FREETEXT` & `CONTAINS`)

To query SQL Server Full-Text Catalogs via `EF.Functions.FreeText()` or `EF.Functions.Contains()`:

```csharp
// Program.cs
builder.Services.AddKeysetPagination(options =>
{
    // Use FREETEXT (default)
    options.UseSqlServerFullTextSearch(useContains: false);

    // Or use CONTAINS for prefix / boolean matching:
    // options.UseSqlServerFullTextSearch(useContains: true);

    options.Configure<Product>(b => b
        .SortBy(x => x.Price)
        .TieBreakBy(x => x.Id)
        .SearchIn(x => x.Name, x => x.Description));
});
```

### Generated T-SQL
When `useContains: false` (FREETEXT):
```sql
WHERE FREETEXT([p].[Name], @term) OR FREETEXT([p].[Description], @term)
```

When `useContains: true` (CONTAINS):
```sql
WHERE CONTAINS([p].[Name], @term) OR CONTAINS([p].[Description], @term)
```

::: warning SQL Server Requirement
`FREETEXT` and `CONTAINS` require an active SQL Server Full-Text Catalog and Full-Text Index on the target columns.
:::

---

## 2. Collation-Aware Search Strategy

Standard SQL Server databases typically use a case-insensitive collation (such as `SQL_Latin1_General_CP1_CI_AS`). Calling `.ToLower()` in LINQ forces SQL Server to evaluate functions on every row, preventing index seeks.

`SqlServerCollationSearchStrategy` emits standard SQL `LIKE` expressions without calling `ToLower()`, allowing SQL Server to leverage column collations and indexes:

```csharp
// Program.cs
builder.Services.AddKeysetPagination(options =>
{
    options.UseSqlServerCollationSearch();
});
```

### Generated T-SQL
```sql
WHERE [p].[Name] LIKE ('%' + @term + '%') OR [p].[Description] LIKE ('%' + @term + '%')
```

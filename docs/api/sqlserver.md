# KeysetEngine.SqlServer API Reference

Namespace: `KeysetEngine.SqlServer`

---

## Classes

### `SqlServerFullTextSearchStrategy`
Search strategy utilizing SQL Server `EF.Functions.FreeText` or `EF.Functions.Contains`.

```csharp
public sealed class SqlServerFullTextSearchStrategy : ISearchStrategy
{
    public SqlServerFullTextSearchStrategy(bool useContains = false);

    public Expression BuildPredicate(Expression propertyAccess, string searchTerm);
}
```

Translates to:
- `useContains == false`: `FREETEXT(property, searchTerm)`
- `useContains == true`: `CONTAINS(property, searchTerm)`

---

### `SqlServerCollationSearchStrategy`
Search strategy leveraging SQL Server's default case-insensitive collation without calling `ToLower()`.

```csharp
public sealed class SqlServerCollationSearchStrategy : ISearchStrategy
{
    public static SqlServerCollationSearchStrategy Instance { get; }

    public Expression BuildPredicate(Expression propertyAccess, string searchTerm);
}
```

Translates to:
```sql
property LIKE ('%' + searchTerm + '%')
```

---

## Dependency Injection Extensions

### `KeysetEngineSqlServerExtensions`

```csharp
public static KeysetEngineOptions UseSqlServerFullTextSearch(
    this KeysetEngineOptions options, 
    bool useContains = false);

public static KeysetEngineOptions UseSqlServerCollationSearch(
    this KeysetEngineOptions options);
```

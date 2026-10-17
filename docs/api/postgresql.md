# KeysetEngine.PostgreSql API Reference

Namespace: `KeysetEngine.PostgreSql`

---

## Classes

### `PostgreSqlSearchStrategy`
Search strategy utilizing PostgreSQL `EF.Functions.ILike`.

```csharp
public sealed class PostgreSqlSearchStrategy : ISearchStrategy
{
    public static PostgreSqlSearchStrategy Instance { get; }

    public Expression BuildPredicate(Expression propertyAccess, string searchTerm);
}
```

Translates to:
```sql
property ILIKE ('%' || searchTerm || '%')
```

---

### `PostgreSqlTsVectorSearchStrategy`
Search strategy utilizing PostgreSQL `tsvector` and `to_tsquery`.

```csharp
public sealed class PostgreSqlTsVectorSearchStrategy : ISearchStrategy
{
    public PostgreSqlTsVectorSearchStrategy(string regConfig = "english");

    public Expression BuildPredicate(Expression propertyAccess, string searchTerm);
}
```

Translates to:
```sql
to_tsvector(regConfig, property) @@ plainto_tsquery(regConfig, searchTerm)
```

---

## Dependency Injection Extensions

### `KeysetEnginePostgreSqlExtensions`

```csharp
public static KeysetEngineOptions UsePostgreSqlSearch(
    this KeysetEngineOptions options);

public static KeysetEngineOptions UsePostgreSqlTsVectorSearch(
    this KeysetEngineOptions options, 
    string regConfig = "english");
```

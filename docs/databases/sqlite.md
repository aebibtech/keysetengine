# SQLite & In-Memory Testing

`KeysetEngine.Core` includes built-in universal search and comparison strategies that function out of the box with **SQLite** and in-memory LINQ datasets without needing additional database-specific packages.

---

## Universal Behavior in SQLite

Because SQLite does not require specialized full-text packages for standard queries, the default `DefaultSearchStrategy` in `KeysetEngine.Core` emits:

```csharp
x.Property.ToLower().Contains(term.ToLower())
```

Which EF Core translates to SQLite as:
```sql
WHERE instr(lower("Property"), lower(@term)) > 0
```

And KeysetEngine's universal nested boolean disjunctions:
```sql
WHERE ("CreatedAt" < @val) OR ("CreatedAt" = @val AND "Id" < @tieVal)
```
work flawlessly on SQLite B-Tree indexes!

---

## Writing Integration Tests with SQLite

You can test keyset pagination flows quickly in your CI pipelines using SQLite in-memory:

```csharp
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using KeysetEngine.Core;
using KeysetEngine.Core.Configuration;

// 1. Create SQLite in-memory connection
using var connection = new SqliteConnection("DataSource=:memory:");
connection.Open();

var options = new DbContextOptionsBuilder<AppDbContext>()
    .UseSqlite(connection)
    .Options;

using var db = new AppDbContext(options);
db.Database.EnsureCreated();

// 2. Configure keyset configuration
var config = new KeysetConfigBuilder<Order>()
    .SortBy(x => x.CreatedAt, ascending: false)
    .TieBreakBy(x => x.Id)
    .Build();

// 3. Paginate
var page1 = await db.Orders
    .ToKeysetPageAsync(
        new KeysetRequest(Limit: 10),
        config,
        Base64UrlCursorProtector.Instance);

Assert.Equal(10, page1.Items.Count);
Assert.True(page1.HasNextPage);
```

::: tip Fast CI Testing
Using SQLite in-memory allows you to write lightning-fast unit and integration tests without spinning up Docker containers or external database servers.
:::

# KeysetEngine

[![CI](https://github.com/stewardaxis/keysetengine/actions/workflows/ci.yml/badge.svg)](https://github.com/stewardaxis/keysetengine/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://opensource.org/licenses/MIT)
[![NuGet](https://img.shields.io/nuget/v/KeysetEngine.Core.svg)](https://www.nuget.org/packages/KeysetEngine.Core)

High-performance, database-agnostic keyset pagination and universal search library for .NET 8, .NET 9, and .NET 10 with Entity Framework Core.

Keyset pagination avoids the $O(N)$ execution degradation inherent to SQL `OFFSET` on deep datasets by establishing stable, indexed boundaries using composite keys and opaque cursors.

---

## Packages

| Package | NuGet | Description |
| :--- | :--- | :--- |
| **`KeysetEngine.Core`** | [![NuGet](https://img.shields.io/nuget/v/KeysetEngine.Core.svg)](https://www.nuget.org/packages/KeysetEngine.Core) | Core contracts, expression builders, $N+1$ slicing, universal search, and `IQueryable<T>` extensions. |
| **`KeysetEngine.DataProtection`** | [![NuGet](https://img.shields.io/nuget/v/KeysetEngine.DataProtection.svg)](https://www.nuget.org/packages/KeysetEngine.DataProtection) | Cryptographic ASP.NET Core Data Protection cursor tokens with purpose isolation. |
| **`KeysetEngine.PostgreSql`** | [![NuGet](https://img.shields.io/nuget/v/KeysetEngine.PostgreSql.svg)](https://www.nuget.org/packages/KeysetEngine.PostgreSql) | PostgreSQL search adapter with `EF.Functions.ILike` and `tsvector` full-text search. |
| **`KeysetEngine.SqlServer`** | [![NuGet](https://img.shields.io/nuget/v/KeysetEngine.SqlServer.svg)](https://www.nuget.org/packages/KeysetEngine.SqlServer) | SQL Server search adapter with `EF.Functions.FreeText` / `CONTAINS` and collation strategies. |

---

## Features

- **True $O(1)$ Keyset Pagination**: No slow `OFFSET` scans; uses nested boolean comparison trees compatible with SQL Server, PostgreSQL, and SQLite.
- **Bidirectional Traversal**: Seamless forward and backward page navigation with automated in-memory ordering reversal.
- **Tamper-Proof Opaque Cursors**: Cryptographic protection via ASP.NET Core Data Protection with purpose and entity isolation.
- **Dynamic Multi-Column Search**: Search across multiple properties without breaking keyset boundaries or client-side evaluation.
- **Multi-Targeting Support**: Native support for modern LTS and Current .NET runtimes (`net8.0`, `net9.0`, `net10.0`).

---

## Quickstart

### 1. Register Services

```csharp
// In Program.cs / Startup.cs
builder.Services.AddKeysetPagination();

// Optional: Register database-specific search strategy
builder.Services.AddKeysetPostgreSqlSearch(); // or AddKeysetSqlServerSearch()
```

### 2. Configure Entity Keyset

```csharp
var config = new KeysetConfigBuilder<Order>()
    .SortBy(x => x.CreatedAt, ascending: false)
    .TieBreakBy(x => x.Id)
    .SearchIn(x => x.CustomerName, x => x.OrderNumber)
    .Build();
```

### 3. Query Paginated Data

```csharp
app.MapGet("/orders", async (
    [AsParameters] KeysetRequest request,
    AppDbContext db,
    ICursorProtector protector,
    CancellationToken ct) =>
{
    var page = await db.Orders
        .AsNoTracking()
        .ToKeysetPageAsync(request, config, protector, ct);

    return Results.Ok(page);
});
```

---

## Semantic Versioning & Releases

KeysetEngine adheres strictly to [Semantic Versioning 2.0.0](https://semver.org/).
Releases and packages are tagged and published automatically using **MinVer** and **NuGet Trusted Publishing (OIDC)**.

---

## License

This project is licensed under the [MIT License](LICENSE).

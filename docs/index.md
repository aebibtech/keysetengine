---
layout: home

hero:
  name: KeysetEngine
  text: High-Performance Keyset Pagination & Search
  tagline: Database-agnostic, zero-degradation pagination with cryptographic cursors for .NET 8, 9, and 10 with EF Core.
  actions:
    - theme: brand
      text: Get Started
      link: /guide/getting-started
    - theme: alt
      text: Why Keyset?
      link: /guide/why-keyset
    - theme: alt
      text: GitHub
      link: https://github.com/aebibtech/keysetengine

features:
  - icon: ⚡
    title: True O(1) Performance
    details: Eliminates SQL OFFSET execution degradation on deep datasets by establishing indexed boundaries using composite keys.
  - icon: 🔄
    title: Bidirectional Traversal
    details: Seamless forward and backward page navigation with automated SQL ordering reversal and in-memory slice ordering.
  - icon: 🔒
    title: Cryptographic Opaque Cursors
    details: ASP.NET Core Data Protection integration provides tamper-proof tokens with sub-purpose tenant and entity isolation.
  - icon: 🔍
    title: Universal Dynamic Search
    details: Compose multi-column search predicates without breaking index scans or triggering client-side evaluation.
  - icon: 🗄️
    title: Native Database Adapters
    details: Out-of-the-box strategies for PostgreSQL (ILike & tsvector) and SQL Server (FreeText, CONTAINS & Collation).
  - icon: 🚀
    title: Multi-Targeting .NET 8 / 9 / 10
    details: Native builds targeting .NET 8.0, .NET 9.0, and .NET 10.0 with centralized package management.
---

## Quick Look

```csharp
// 1. Configure pagination boundary in Program.cs
builder.Services.AddKeysetPagination(options =>
{
    options.Configure<Order>(builder => builder
        .SortBy(x => x.CreatedAt, ascending: false)
        .TieBreakBy(x => x.Id)
        .SearchIn(x => x.CustomerName, x => x.OrderNumber));
});

// 2. Query in an ASP.NET Core Minimal API
app.MapGet("/api/orders", async (
    [AsParameters] KeysetRequest request,
    AppDbContext db,
    ICursorProtector protector,
    CancellationToken ct) =>
{
    var page = await db.Orders
        .AsNoTracking()
        .ToKeysetPageAsync(request, protector, ct);

    return Results.Ok(page);
});
```

::: tip High Throughput at Deep Offsets
Unlike standard SQL `OFFSET 100000 LIMIT 20` which forces your database engine to scan and discard 100,000 index entries, `KeysetEngine` translates queries directly into composite index seeks:
```sql
WHERE ("CreatedAt" < @val) OR ("CreatedAt" = @val AND "Id" < @tieVal)
ORDER BY "CreatedAt" DESC, "Id" DESC
LIMIT 21
```
Execution time remains consistently **under 1 millisecond**, regardless of table size.
:::

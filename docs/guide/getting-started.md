# Overview & Installation

::: tip Direct Answer (Executive Summary)
**KeysetEngine** is a high-performance keyset pagination and search engine for .NET 8, 9, and 10 with Entity Framework Core. It replaces $O(N)$ SQL `OFFSET` with $O(1)$ composite index seeks (`WHERE SortCol < @val OR (SortCol = @val AND Id < @tieVal)`). It supports two configuration styles: **Registry Configuration** (centralized via DI, requiring zero config objects at endpoints) and **Explicit Configuration** (direct `KeysetConfig<T>` instances, ideal for multiple sorting presets and CQRS handlers).
:::

**KeysetEngine** is a high-throughput, database-agnostic keyset pagination and search engine for .NET and Entity Framework Core. It targets modern LTS and Current .NET runtimes: **.NET 8.0**, **.NET 9.0**, and **.NET 10.0**.

---

## Package Topology

The library is divided into specialized packages:

| Package | NuGet | Purpose |
| :--- | :--- | :--- |
| **`KeysetEngine.Core`** | [![NuGet](https://img.shields.io/nuget/v/KeysetEngine.Core.svg)](https://www.nuget.org/packages/KeysetEngine.Core) | Core domain models, expression tree generation, universal search, and `IQueryable<T>` extensions. |
| **`KeysetEngine.DataProtection`** | [![NuGet](https://img.shields.io/nuget/v/KeysetEngine.DataProtection.svg)](https://www.nuget.org/packages/KeysetEngine.DataProtection) | Cryptographic token protection using ASP.NET Core Data Protection with purpose isolation. |
| **`KeysetEngine.PostgreSql`** | [![NuGet](https://img.shields.io/nuget/v/KeysetEngine.PostgreSql.svg)](https://www.nuget.org/packages/KeysetEngine.PostgreSql) | PostgreSQL-native search strategies (`EF.Functions.ILike` and `tsvector` full-text search). |
| **`KeysetEngine.SqlServer`** | [![NuGet](https://img.shields.io/nuget/v/KeysetEngine.SqlServer.svg)](https://www.nuget.org/packages/KeysetEngine.SqlServer) | SQL Server-native search strategies (`EF.Functions.FreeText`, `CONTAINS`, and collation). |

---

## Installation

Install the core package and the cryptographic data protection provider via the .NET CLI:

::: code-group

```bash [Core + DataProtection]
dotnet add package KeysetEngine.Core
dotnet add package KeysetEngine.DataProtection
```

```bash [PostgreSQL Stack]
dotnet add package KeysetEngine.Core
dotnet add package KeysetEngine.DataProtection
dotnet add package KeysetEngine.PostgreSql
```

```bash [SQL Server Stack]
dotnet add package KeysetEngine.Core
dotnet add package KeysetEngine.DataProtection
dotnet add package KeysetEngine.SqlServer
```

:::

---

## 3-Step Quickstart

KeysetEngine supports two ways to configure entities: **Registry Configuration** (centralized via DI) and **Explicit Configuration** (standalone instance). Choose the tab that best matches your architecture below.

### Step 1: Configure Keyset Boundaries

::: code-group

```csharp [Registry Configuration (Recommended)]
// In Program.cs: Register DI and configure entities into KeysetConfigRegistry
using KeysetEngine.Core.Configuration;
using KeysetEngine.DataProtection;

var builder = WebApplication.CreateBuilder(args);

// Register ASP.NET Core Data Protection (required for secure cursor tokens)
builder.Services.AddDataProtection();
builder.Services.AddKeysetDataProtection();

// Register KeysetEngine and configure entity mappings centrally
builder.Services.AddKeysetPagination(options =>
{
    options.Configure<Order>(b => b
        .SortBy(x => x.CreatedAt, ascending: false)
        .TieBreakBy(x => x.Id)
        .SearchIn(x => x.CustomerName, x => x.OrderNumber));
});
```

```csharp [Explicit Configuration]
// Define a standalone KeysetConfig<T> (e.g., in your endpoint, service, or static helper)
using KeysetEngine.Core.Configuration;
using KeysetEngine.DataProtection;

// Register services in Program.cs
builder.Services.AddDataProtection();
builder.Services.AddKeysetDataProtection();
builder.Services.AddKeysetPagination();

// Build an explicit immutable configuration instance
public static class OrderKeysetDefinitions
{
    public static readonly KeysetConfig<Order> Default = new KeysetConfigBuilder<Order>()
        .SortBy(x => x.CreatedAt, ascending: false)
        .TieBreakBy(x => x.Id)
        .SearchIn(x => x.CustomerName, x => x.OrderNumber)
        .Build();
}
```

:::

---

### Step 2: Define Endpoint with `KeysetRequest`

Expose an endpoint accepting `[AsParameters] KeysetRequest` and invoke `ToKeysetPageAsync`:

::: code-group

```csharp [Using Registry]
// No config object needed at the endpoint! It is resolved from KeysetConfigRegistry.
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

```csharp [Using Explicit Config]
// Pass the explicit KeysetConfig<Order> instance directly to the query.
app.MapGet("/api/orders", async (
    [AsParameters] KeysetRequest request,
    AppDbContext db,
    ICursorProtector protector,
    CancellationToken ct) =>
{
    var page = await db.Orders
        .AsNoTracking()
        .ToKeysetPageAsync(request, OrderKeysetDefinitions.Default, protector, ct);

    return Results.Ok(page);
});
```

:::

---

### Step 3: Consume Paginated Response

The returned `KeysetPage<T>` contains the current slice and cryptographic edge cursors:

```json
{
  "items": [
    {
      "id": "018f4a1a-3e81-7d12-9842-8871ab14299b",
      "customerName": "Alice Johnson",
      "orderNumber": "ORD-1002",
      "createdAt": "2026-10-17T12:00:00Z"
    }
  ],
  "startCursor": "CfDJ8MvV2...",
  "endCursor": "CfDJ8MvV2...",
  "hasNextPage": true,
  "hasPreviousPage": false
}
```

- **Next Page (Forward)**: Send the `endCursor` as `cursor`:
  ```http
  GET /api/orders?cursor=CfDJ8MvV2...&limit=20 HTTP/1.1
  ```
- **Previous Page (Backward)**: Send the `startCursor` with `direction=Backward`:
  ```http
  GET /api/orders?cursor=CfDJ8MvV2...&direction=Backward&limit=20 HTTP/1.1
  ```

---

## Registry vs. Explicit Configuration: When to Use Which?

Both approaches provide identical $O(1)$ query performance and zero-cost runtime expression caching. Choose the style that best aligns with your application structure:

| Dimension | Registry Configuration | Explicit Configuration |
| :--- | :--- | :--- |
| **Declaration Site** | `Program.cs` / `AddKeysetPagination(options => ...)` | `new KeysetConfigBuilder<T>()...Build()` (static field or class) |
| **Endpoint Invocation** | `query.ToKeysetPageAsync(request, protector)` | `query.ToKeysetPageAsync(request, config, protector)` |
| **Multiple Sort Presets** | One configuration per entity type | Multiple configurations per entity type |
| **Ideal For** | Standard CRUD REST APIs, single-view dashboards | Multi-sort grids, CQRS/MediatR handlers, multi-tenant isolation |

### Example: Multiple Sorting Presets on the Same Entity

When an entity requires multiple distinct sort options (for example, sorting orders by `CreatedAt` vs. by `TotalAmount`), use **Explicit Configuration**:

```csharp
public static class OrderSortPresets
{
    // Preset 1: Newest to Oldest
    public static readonly KeysetConfig<Order> ByDateDesc = new KeysetConfigBuilder<Order>()
        .SortBy(x => x.CreatedAt, ascending: false)
        .TieBreakBy(x => x.Id)
        .SearchIn(x => x.CustomerName)
        .Build();

    // Preset 2: Highest to Lowest Amount
    public static readonly KeysetConfig<Order> ByAmountDesc = new KeysetConfigBuilder<Order>()
        .SortBy(x => x.TotalAmount, ascending: false)
        .TieBreakBy(x => x.Id)
        .SearchIn(x => x.CustomerName)
        .Build();
}

// In Endpoint: Select preset based on query parameter
app.MapGet("/api/orders", async (
    [AsParameters] KeysetRequest request,
    [FromQuery] string? sortBy,
    AppDbContext db,
    ICursorProtector protector,
    CancellationToken ct) =>
{
    var config = sortBy?.ToLowerInvariant() == "amount"
        ? OrderSortPresets.ByAmountDesc
        : OrderSortPresets.ByDateDesc;

    var page = await db.Orders
        .AsNoTracking()
        .ToKeysetPageAsync(request, config, protector, ct);

    return Results.Ok(page);
});
```

### Example: CQRS / MediatR Query Handlers

In CQRS architectures, encapsulating the keyset definition inside the query handler maintains clean separation of concerns:

```csharp
public record GetOrdersQuery(KeysetRequest Request) : IRequest<KeysetPage<OrderDto>>;

public class GetOrdersQueryHandler : IRequestHandler<GetOrdersQuery, KeysetPage<OrderDto>>
{
    private static readonly KeysetConfig<Order> Keyset = new KeysetConfigBuilder<Order>()
        .SortBy(x => x.CreatedAt, ascending: false)
        .TieBreakBy(x => x.Id)
        .SearchIn(x => x.CustomerName, x => x.OrderNumber)
        .Build();

    private readonly AppDbContext _db;
    private readonly ICursorProtector _protector;

    public GetOrdersQueryHandler(AppDbContext db, ICursorProtector protector)
    {
        _db = db;
        _protector = protector;
    }

    public async Task<KeysetPage<OrderDto>> Handle(GetOrdersQuery query, CancellationToken ct)
    {
        return await _db.Orders
            .AsNoTracking()
            .Select(o => new OrderDto(o.Id, o.CustomerName, o.CreatedAt))
            .ToKeysetPageAsync(query.Request, Keyset, _protector, ct);
    }
}
```

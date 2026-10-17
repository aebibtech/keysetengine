# Overview & Installation

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

### Step 1: Register Services

In your application entry point (`Program.cs`), register KeysetEngine and configure your entity boundaries:

```csharp
using KeysetEngine.Core.Configuration;
using KeysetEngine.DataProtection;

var builder = WebApplication.CreateBuilder(args);

// Register ASP.NET Core Data Protection (required for secure cursor tokens)
builder.Services.AddDataProtection();
builder.Services.AddKeysetDataProtection();

// Register KeysetEngine and configure entity mappings
builder.Services.AddKeysetPagination(options =>
{
    options.Configure<Order>(b => b
        .SortBy(x => x.CreatedAt, ascending: false)
        .TieBreakBy(x => x.Id)
        .SearchIn(x => x.CustomerName, x => x.OrderNumber));
});
```

::: tip Explicit vs Registry Configuration
Configuring entities via `options.Configure<T>()` registers them into `KeysetConfigRegistry`, allowing you to invoke `ToKeysetPageAsync(request, protector)` without passing the config instance to every endpoint.
:::

---

### Step 2: Define Endpoint with `KeysetRequest`

Expose an endpoint accepting `[AsParameters] KeysetRequest`:

```csharp
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

---

### Step 3: Consume Paginated Response

The returned `KeysetPage<T>` contains:

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

To fetch the next page, send the `endCursor` as the `cursor` query parameter:

```http
GET /api/orders?cursor=CfDJ8MvV2...&limit=20 HTTP/1.1
```

To navigate backward, specify `direction=Backward` with the `startCursor`:

```http
GET /api/orders?cursor=CfDJ8MvV2...&direction=Backward&limit=20 HTTP/1.1
```

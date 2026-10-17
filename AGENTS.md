# .NET Keyset Pagination & Search Engine
## Technical Specification, Architecture & Implementation Playbook

---

## 1. Executive Summary & Architectural Scope

The objective of this project is to create an intuitive, high-throughput, and database-agnostic keyset pagination and search backend library targeting `IQueryable<T>` and ASP.NET Core with Entity Framework Core. 

Keyset pagination avoids the $O(N)$ execution degradation inherent to SQL `OFFSET` on deep datasets by establishing stable, indexed boundaries using composite keys and opaque cursors.

```
                      +------------------------------------------+
                      |         Consumer Application             |
                      |   (ASP.NET Core Minimal APIs / Web API)  |
                      +--------------------+---------------------+
                                           |
                    +----------------------+----------------------+
                    |                                             |
                    v                                             v
  +------------------------------------+       +------------------------------------+
  |         KeysetEngine.Core          |       |    KeysetEngine.DataProtection     |
  | - IQueryable<T> Extensions         |       | - ICursorProtector                 |
  | - Dynamic Expression Generator     |       | - DataProtectionCursorProtector    |
  | - Request / Response Page Envelopes|       | - Sub-Purpose Tenant/Entity Scope  |
  | - Invariant Universal Search       |       +------------------------------------+
  +-----------------+------------------+
                    |
                    +--------------------+--------------------+
                    |                                         |
                    v                                         v
  +------------------------------------+   +------------------------------------+
  |      KeysetEngine.PostgreSql       |   |       KeysetEngine.SqlServer       |
  | - ILike String Pattern Strategy    |   | - FreeText / CONTAINS Strategy     |
  | - GIN / tsvector Full-Text Support |   | - Default Collation Optimizations  |
  +------------------------------------+   +------------------------------------+
```

### End-to-End Execution Flow
1. **HTTP / API Layer**: Decodes query string parameters and opaque cursor tokens into strongly-typed pagination requests (`KeysetRequest`).
2. **Query Pipeline / Specification**: Dynamically translates search terms and cursor states into LINQ expression trees without triggering client-side evaluation.
3. **EF Core Execution**: Appends filters, composite keyset inequalities, bidirectional ordering, and `.Take(limit + 1)` into a single SQL statement.
4. **Page Envelope & Cursor Encoding**: Inspects the $N+1$ result slice, determines `HasNextPage` and `HasPreviousPage`, reverses in-memory order if traversing backward, and serializes cryptographic edge cursors.

---

## 2. Solution & Package Topology

All libraries multi-target modern LTS and Current .NET runtimes using NuGet Central Package Management (CPM).

| Project / Assembly | Target Frameworks | Key Dependencies | Purpose |
| :--- | :--- | :--- | :--- |
| **`KeysetEngine.Core`** | `net8.0; net9.0; net10.0` | `Microsoft.EntityFrameworkCore` | Core contracts, expression compilation, composite keyset inequality generator, universal search fallback, and `IQueryable<T>` extension methods. |
| **`KeysetEngine.DataProtection`** | `net8.0; net9.0; net10.0` | `KeysetEngine.Core`<br>`Microsoft.AspNetCore.DataProtection.Abstractions` | Cryptographic signing, encryption, tamper detection, and endpoint-isolated cursor tokens. |
| **`KeysetEngine.PostgreSql`** | `net8.0; net9.0; net10.0` | `KeysetEngine.Core`<br>`Npgsql.EntityFrameworkCore.PostgreSQL` | Postgres-native search adapters (`EF.Functions.ILike`, `tsvector`). |
| **`KeysetEngine.SqlServer`** | `net8.0; net9.0; net10.0` | `KeysetEngine.Core`<br>`Microsoft.EntityFrameworkCore.SqlServer` | SQL Server-native full-text search adapters (`EF.Functions.FreeText`, `CONTAINS`). |
| **`KeysetEngine.Tests.Unit`** | `net8.0; net9.0; net10.0` | `xunit`, `FluentAssertions`, `Moq` | Unit tests for token encoding, expression shape validation, and in-memory evaluation. |
| **`KeysetEngine.Tests.Integration`** | `net8.0; net9.0; net10.0` | `Testcontainers.PostgreSql`<br>`Testcontainers.MsSql`<br>`Microsoft.EntityFrameworkCore.Sqlite` | Real database tests verifying translated SQL execution plans and pagination boundaries. |

---

## 3. Database Compatibility & Strategy Design

| Database Provider | Keyset Comparison Syntax | Text Search Strategy | Index Requirements |
| :--- | :--- | :--- | :--- |
| **Universal / SQLite** | Nested boolean disjunctions `(A > a) OR (A == a AND B > b)` | `LOWER(col) LIKE '%' + LOWER(term) + '%'` | Composite index matching `(SortColumn, Id)` |
| **PostgreSQL** | Nested boolean disjunctions | `EF.Functions.ILike(col, '%term%')` or `tsvector` | Composite B-tree `(SortColumn, Id)`; GIN/pg_trgm for search |
| **SQL Server** | Nested boolean disjunctions | `EF.Functions.FreeText` / `CONTAINS` / standard `LIKE` | Composite index `(SortColumn, Id)`; Full-Text Catalog |
| **MySQL / MariaDB** | Nested boolean disjunctions | Standard `LIKE` / Collation-based CI | Composite index `(SortColumn, Id)` |

### Why Nested Boolean Trees?
While some engines (PostgreSQL, SQLite) support SQL row-value syntax `(a, b) > (x, y)`, SQL Server does not, and EF Core relational translators do not uniformly map C# tuples to SQL tuple comparisons. Emitting nested boolean trees ensures identical, index-seekable SQL generation across all relational database engines:

$$\text{Forward Ascending} = (Key_A > val_A) \lor (Key_A == val_A \land Key_B > val_B)$$
$$\text{Forward Descending} = (Key_A < val_A) \lor (Key_A == val_A \land Key_B < val_B)$$

---

## 4. Cursor Token Architecture & Security

### Token Schema
```csharp
public sealed record KeysetCursorToken(
    string SortValueJson,
    string TieBreakerValueJson,
    bool IsAscending,
    string? SearchHash
);
```

* **`SortValueJson`**: Serialized JSON literal of the sorted property value at the edge.
* **`TieBreakerValueJson`**: Serialized unique identifier (primary key).
* **`IsAscending`**: Sort direction flag of the query that produced the cursor.
* **`SearchHash`**: Non-cryptographic hash (e.g., `xxHash64` or `SHA-256`) of the search term to invalidate stale cursors when filters change.

### Cryptographic Protection Layer
* **Purpose Isolation**: Scopes keys using sub-purposes:
  ```csharp
  provider.CreateProtector("KeysetEngine", entityPurpose, "v1");
  ```
  This ensures tokens generated for an `Order` endpoint cannot be submitted to a `Customer` endpoint.
* **Tamper Prevention**: Authenticated encryption (AES-256-GCM / HMACSHA256 via ASP.NET Core Data Protection) prevents clients from viewing or modifying internal database keys.
* **Resilient Fallback**: Corrupted, modified, or expired tokens catch `CryptographicException` safely and fall back to the first page rather than throwing 500 errors.

---

## 5. Execution Pipeline Details

1. **Dynamic Search**: Combines all configured property search predicates using `Expression.OrElse` and attaches via `.Where()`.
2. **Keyset Inequality Formulation**:
   * Evaluates comparison based on sort direction and requested navigation direction.
   * Backward navigation flips inequalities ($> \leftrightarrow <$) and reverses the SQL `ORDER BY` directions.
3. **$N + 1$ Slicing**: Fetches `limit + 1` items using `.Take(limit + 1).ToListAsync()`. If count exceeds `limit`, a subsequent page exists.
4. **In-Memory Reversal**: When navigating backward, the resulting batch is reversed in memory before returning to maintain correct sequential order.

---

## 6. Implementation Phasing & Milestones

* **Milestone 1: Core Domain & Tokens**
  * Define `KeysetRequest`, `KeysetPage<T>`, and `KeysetCursorToken`.
  * Implement `ICursorProtector` and `DataProtectionCursorProtector`.
  * Add query signature hashing for filter change detection.
* **Milestone 2: Dynamic Expression Engine**
  * Implement `KeysetExpressionBuilder` generating universal nested boolean disjunctions.
  * Implement dynamic `OrderBy` and `ThenBy` expression application supporting bidirectional flipping.
* **Milestone 3: IQueryable Pipeline & Slicing**
  * Implement `ToKeysetPageAsync` extension method.
  * Integrate $N+1$ slicing and page boundary cursor creation.
* **Milestone 4: Fluent Configuration Builder & Caching**
  * Implement `KeysetConfigBuilder<T>` for declarative entity mapping.
  * Cache compiled property access expressions in `ConcurrentDictionary<Type, KeysetDefinition>`.
* **Milestone 5: Multi-DB Strategies & Integration Testing**
  * Implement `PostgreSqlSearchStrategy` and `SqlServerSearchStrategy`.
  * Set up Testcontainers test suite validating identical behavior on PostgreSQL, SQL Server, and SQLite.

---

## 7. Structured Development Prompts (AI Coding Assistant Playbook)

### Prompt 1: Solution Topology & Workspace Scaffolding
```text
Act as a Principal .NET Architect. Set up the complete project scaffolding for a modular .NET keyset pagination and search engine.

Requirements:
1. Target Frameworks: Multi-target net8.0, net9.0, and net10.0 across all library projects.
2. Structure:
   - Directory.Build.props (centralizing Nullable=enable, ImplicitUsings=enable, TreatWarningsAsErrors=true, LangVersion=preview)
   - Directory.Packages.props (using NuGet Central Package Management)
   - src/KeysetEngine.Core (references Microsoft.EntityFrameworkCore)
   - src/KeysetEngine.DataProtection (references KeysetEngine.Core and Microsoft.AspNetCore.DataProtection.Abstractions)
   - src/KeysetEngine.PostgreSql (references KeysetEngine.Core and Npgsql.EntityFrameworkCore.PostgreSQL)
   - src/KeysetEngine.SqlServer (references KeysetEngine.Core and Microsoft.EntityFrameworkCore.SqlServer)
   - tests/KeysetEngine.Tests.Unit
   - tests/KeysetEngine.Tests.Integration
3. Provide the complete Directory.Build.props, Directory.Packages.props, and .csproj files for all projects.
```

### Prompt 2: Contracts, Token Schema & Security (`KeysetEngine.Core` & `DataProtection`)
```text
Implement Milestone 1 (Core Domain Models & Cryptographic Token Protection) for KeysetEngine.

Requirements:
1. In KeysetEngine.Core:
   - Create record KeysetRequest(string? Cursor = null, PageDirection Direction = PageDirection.Forward, int Limit = 20, string? Search = null).
   - Create record KeysetPage<T>(IReadOnlyList<T> Items, string? StartCursor, string? EndCursor, bool HasNextPage, bool HasPreviousPage).
   - Create KeysetCursorToken containing SortValueJson, TieBreakerValueJson, IsAscending, and SearchHash.
   - Include a fallback Base64Url serializer for testing without Data Protection.
2. In KeysetEngine.DataProtection:
   - Create interface ICursorProtector with methods string Protect(KeysetCursorToken token, string entityPurpose) and KeysetCursorToken? Unprotect(string? opaqueCursor, string entityPurpose).
   - Implement DataProtectionCursorProtector using IDataProtectionProvider.
   - Ensure purpose isolation using sub-purposes: provider.CreateProtector("KeysetEngine", entityPurpose, "v1").
   - Catch CryptographicException, FormatException, and JsonException inside Unprotect, returning null on invalid, tampered, or expired tokens.
3. Write xUnit unit tests verifying token roundtrips, purpose mismatches, and tampered token resilience.
```

### Prompt 3: Keyset Expression Tree Generator (`KeysetEngine.Core`)
```text
Implement Milestone 2: Dynamic Expression Builders for Keyset Inequalities and Ordering.

Requirements:
1. Keyset Predicate Builder:
   - Do NOT use SQL row-value syntax (tuples). Generate universal nested boolean disjunctions compatible with SQL Server, SQLite, and PostgreSQL.
   - Forward Ascending: (SortCol > val) OR (SortCol == val AND TieBreaker > tieVal)
   - Forward Descending: (SortCol < val) OR (SortCol == val AND TieBreaker < tieVal)
   - Backward Navigation: Invert comparison operators (< to >, > to <) relative to sort direction.
2. Dynamic Ordering Helper:
   - Dynamically emit IQueryable.OrderBy / OrderByDescending and ThenBy / ThenByDescending calls using System.Linq.Expressions without hardcoded reflection strings.
   - Automatically invert SQL ORDER BY sequence when PageDirection is Backward.
3. Write unit tests validating generated Expression tree shapes and compile checks against in-memory datasets.
```

### Prompt 4: Search Strategy & IQueryable Pipeline (`KeysetEngine.Core`)
```text
Implement Milestone 3: The Search Strategy Interface and the IQueryable<T> Extension Method.

Requirements:
1. Create interface ISearchStrategy:
   - Expression BuildPredicate(Expression propertyAccess, string searchTerm);
2. Implement DefaultSearchStrategy using Universal Lower/Contains:
   - Emits targetProperty.ToLower().Contains(searchTerm.ToLower()).
3. Implement KeysetExpressionBuilder.ApplySearch:
   - Combine multiple PropertyInfo target properties using Expression.OrElse into a single lambda x => (x.Col1.ToLower().Contains(...) || x.Col2.ToLower().Contains(...)).
4. Implement the full extension method:
   - public static async Task<KeysetPage<T>> ToKeysetPageAsync<T>(this IQueryable<T> source, KeysetRequest request, KeysetConfig<T> config, ICursorProtector cursorProtector, CancellationToken ct = default)
   - Integrate: Search predicate -> Keyset boundary predicate -> Dynamic OrderBy/ThenBy -> .Take(limit + 1).ToListAsync(ct).
   - Implement in-memory reversal on backward traversal and edge cursor generation.
```

### Prompt 5: Fluent Configuration Builder & Expression Caching (`KeysetEngine.Core`)
```text
Implement Milestone 4: Fluent API Configuration & Property Expression Caching.

Requirements:
1. Create a fluent KeysetConfigBuilder<T>:
   - builder.SortBy(x => x.CreatedAt, ascending: false)
   - builder.TieBreakBy(x => x.Id)
   - builder.SearchIn(x => x.Title, x => x.Description)
   - builder.UseSearchStrategy(new PostgreSqlSearchStrategy())
2. Enforce compile-time and runtime validation:
   - Tie-breaker property must be non-nullable and unique.
   - Sort property must be non-nullable.
3. Performance Caching:
   - Cache compiled lambda accessors and PropertyInfo metadata in a static ConcurrentDictionary<Type, KeysetDefinition> so reflection is executed only once per entity type across the application lifetime.
4. Provide ASP.NET Core DI extensions: services.AddKeysetPagination().
```

### Prompt 6: Database Dialect Adapters (`KeysetEngine.PostgreSql` & `SqlServer`)
```text
Implement Milestone 5A: Database-Specific Search Adapters.

Requirements:
1. In KeysetEngine.PostgreSql:
   - Implement PostgreSqlSearchStrategy : ISearchStrategy using EF.Functions.ILike(property, $"%{term}%").
   - Add tsvector full-text search strategy for Postgres tsvector columns.
2. In KeysetEngine.SqlServer:
   - Implement SqlServerFullTextSearchStrategy : ISearchStrategy using EF.Functions.FreeText(property, term) or EF.Functions.Contains(property, term).
   - Implement SqlServerCollationSearchStrategy relying on SQL Server's default case-insensitive collation.
3. Provide DI extension methods:
   - services.AddKeysetPagination(options => options.UsePostgreSqlSearch());
   - services.AddKeysetPagination(options => options.UseSqlServerFullTextSearch());
```

### Prompt 7: Integration Test Suite with Testcontainers
```text
Implement Milestone 5B: Multi-Database Integration Tests using Testcontainers.

Requirements:
1. Set up integration test fixtures in tests/KeysetEngine.Tests.Integration using:
   - Testcontainers.PostgreSql
   - Testcontainers.MsSql
   - Microsoft.EntityFrameworkCore.Sqlite (in-memory)
2. Seed identical test datasets (10,000 entities with duplicate sort dates, randomized strings, and UUID/long IDs).
3. Test Scenarios:
   - Seamless forward pagination through entire dataset (no missing items, no duplicate keys).
   - Continuous bidirectional pagination (Page 1 -> Page 2 -> Page 3 -> Page 2 -> Page 1).
   - Dynamic multi-column text search combined with active keyset cursors.
   - Tampered/invalid cursor gracefully resets to Page 1.
   - Verify that EF Core produces a single SQL query without client-side evaluation warnings.
```
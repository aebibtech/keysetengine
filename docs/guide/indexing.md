# Index Design & Optimization

::: tip Direct Answer (Executive Summary)
Keyset pagination requires a **composite B-Tree index** on `(SortColumn, TieBreakerColumn)` (e.g. `(CreatedAt DESC, Id DESC)`). For multi-tenant databases, the tenant partition ID must be the leading column: `(TenantId, SortColumn, TieBreaker)`. This allows the database engine to perform an immediate **Index Seek** directly to the requested page boundary in $< 1\text{ms}$.
:::

To achieve true $O(1)$ query latency with KeysetEngine, relational database engines require properly designed composite B-Tree indexes matching your sort order and tie-breaker definitions.

---

## The Composite Index Rule

For every paginated entity, define a composite index on:
1. **Sort Property** (Primary sort order)
2. **Tie-Breaker Property** (Unique identifier, typically Primary Key)

### PostgreSQL (EF Core Fluent API)
```csharp
modelBuilder.Entity<Order>()
    .HasIndex(o => new { o.CreatedAt, o.Id })
    .HasDatabaseName("IX_Orders_CreatedAt_Id");
```

Generated PostgreSQL DDL:
```sql
CREATE INDEX "IX_Orders_CreatedAt_Id" ON "Orders" ("CreatedAt" DESC, "Id" DESC);
```

### SQL Server (EF Core Fluent API)
```csharp
modelBuilder.Entity<Order>()
    .HasIndex(o => new { o.CreatedAt, o.Id })
    .IsClustered(false)
    .HasDatabaseName("IX_Orders_CreatedAt_Id");
```

Generated T-SQL DDL:
```sql
CREATE NONCLUSTERED INDEX [IX_Orders_CreatedAt_Id] 
ON [dbo].[Orders] ([CreatedAt] DESC, [Id] DESC);
```

---

## Multi-Tenant Indexing

When partitioning data by tenant or organization ID, ensure the tenant filter is the **leading column** in the composite index:

```csharp
modelBuilder.Entity<Order>()
    .HasIndex(o => new { o.TenantId, o.CreatedAt, o.Id })
    .HasDatabaseName("IX_Orders_Tenant_CreatedAt_Id");
```

This guarantees that:
- The database engine immediately seeks directly to the tenant's partition.
- It then seeks directly into the keyset boundary using `(CreatedAt, Id)`.

---

## Verifying Index Seeks

Always inspect your database execution plans to confirm that the database performs an **Index Seek** rather than an **Index Scan**:

### PostgreSQL EXPLAIN ANALYZE
```sql
EXPLAIN ANALYZE
SELECT * FROM "Orders"
WHERE ("CreatedAt" < '2026-10-17 12:00:00') 
   OR ("CreatedAt" = '2026-10-17 12:00:00' AND "Id" < '018f4a1a-3e81-7d12-9842-8871ab14299b')
ORDER BY "CreatedAt" DESC, "Id" DESC
LIMIT 21;
```

**Expected Result:**
```
->  Limit  (cost=0.43..8.45 rows=21 width=128) (actual time=0.035..0.048 rows=21 loops=1)
      ->  Index Scan Backward using "IX_Orders_CreatedAt_Id" on "Orders"
```
Total execution time: **< 0.1ms**.

### SQL Server Execution Plan
Look for:
- Operator: **Index Seek (NonClustered)**
- Seek Predicates: `Prefix: [dbo].[Orders].[CreatedAt], [dbo].[Orders].[Id]`
- Number of Executions: `1`

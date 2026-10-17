using KeysetEngine.Tests.Integration.Models;
using Microsoft.EntityFrameworkCore;

namespace KeysetEngine.Tests.Integration.Data;

public class TestDbContext : DbContext
{
    public DbSet<TestOrder> Orders => Set<TestOrder>();

    public TestDbContext(DbContextOptions<TestDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TestOrder>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();

            // B-Tree Composite Index matching the Keyset boundary (SortColumn, Id)
            b.HasIndex(x => new { x.CreatedAt, x.Id })
                .HasDatabaseName("IX_Orders_CreatedAt_Id");

            b.Property(x => x.CustomerName)
                .HasMaxLength(200)
                .IsRequired();

            b.Property(x => x.Description)
                .HasMaxLength(500)
                .IsRequired();

            b.Property(x => x.Amount)
                .HasPrecision(18, 2);
        });
    }
}

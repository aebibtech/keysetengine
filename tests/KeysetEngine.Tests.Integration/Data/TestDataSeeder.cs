using KeysetEngine.Tests.Integration.Models;

namespace KeysetEngine.Tests.Integration.Data;

public static class TestDataSeeder
{
    public static List<TestOrder> GenerateOrders(int count)
    {
        var baseDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var orders = new List<TestOrder>(count);

        string[] companies = ["Acme Corp", "Global Logistics", "Quantum Dynamics", "Apex Industries", "Steward Nexus"];
        string[] categories = ["Enterprise Cloud License", "Database Server Migration", "Keyset Pagination Cluster", "API Gateway Subscription"];

        for (int i = 1; i <= count; i++)
        {
            // Create collisions: every 4 items share the exact same CreatedAt timestamp
            DateTime date = baseDate.AddMinutes((i - 1) / 4);

            string company = companies[(i - 1) % companies.Length];
            string category = categories[(i - 1) % categories.Length];

            orders.Add(new TestOrder
            {
                Id = i,
                CreatedAt = date,
                CustomerName = $"{company} #{i}",
                Description = $"{category} - Order Sequence {i}",
                Amount = 100m + (i * 0.5m)
            });
        }

        return orders;
    }

    public static async Task SeedDatabaseAsync(TestDbContext context, int count = 2000)
    {
        await context.Database.EnsureCreatedAsync();

        if (context.Orders.Any())
        {
            return;
        }

        List<TestOrder> orders = GenerateOrders(count);

        // Batch insert
        const int batchSize = 1000;
        for (int i = 0; i < orders.Count; i += batchSize)
        {
            var batch = orders.Skip(i).Take(batchSize);
            await context.Orders.AddRangeAsync(batch);
            await context.SaveChangesAsync();
        }
    }
}

using KeysetEngine.Core.Search;
using KeysetEngine.PostgreSql;
using KeysetEngine.Tests.Integration.Data;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace KeysetEngine.Tests.Integration;

public class PostgreSqlKeysetIntegrationTests : KeysetIntegrationTestBase, IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .Build();

    private DbContextOptions<TestDbContext> _options = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        _options = new DbContextOptionsBuilder<TestDbContext>()
            .UseNpgsql(_container.GetConnectionString())
            .Options;

        using var context = CreateDbContext();
        await TestDataSeeder.SeedDatabaseAsync(context, count: 2000);
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    protected override TestDbContext CreateDbContext()
    {
        return new TestDbContext(_options);
    }

    protected override ISearchStrategy GetSearchStrategy()
    {
        return PostgreSqlSearchStrategy.Instance;
    }

    [Fact]
    public async Task PostgreSql_SeamlessForwardPagination_NoDuplicatesNoGaps()
    {
        await ExecuteSeamlessForwardPaginationTestAsync(totalExpected: 2000, pageSize: 100);
    }

    [Fact]
    public async Task PostgreSql_ContinuousBidirectionalPagination_VerifiesNaturalOrderAcrossPages()
    {
        await ExecuteContinuousBidirectionalPaginationTestAsync(pageSize: 25);
    }

    [Fact]
    public async Task PostgreSql_DynamicSearch_WithKeysetBoundaries()
    {
        await ExecuteDynamicSearchWithKeysetBoundariesTestAsync("Quantum Dynamics", pageSize: 50);
    }

    [Fact]
    public async Task PostgreSql_TamperedCursor_GracefullyResetsToPage1()
    {
        await ExecuteTamperedCursorGracefulResetTestAsync();
    }
}

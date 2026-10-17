using KeysetEngine.Core.Search;
using KeysetEngine.SqlServer;
using KeysetEngine.Tests.Integration.Data;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;
using Xunit;

namespace KeysetEngine.Tests.Integration;

public class MsSqlKeysetIntegrationTests : KeysetIntegrationTestBase, IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    private DbContextOptions<TestDbContext> _options = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        _options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlServer(_container.GetConnectionString())
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
        // Use Collation search strategy for standard SQL Server relational indexing without requiring FTS service provisioned
        return SqlServerCollationSearchStrategy.Instance;
    }

    [Fact]
    public async Task MsSql_SeamlessForwardPagination_NoDuplicatesNoGaps()
    {
        await ExecuteSeamlessForwardPaginationTestAsync(totalExpected: 2000, pageSize: 100);
    }

    [Fact]
    public async Task MsSql_ContinuousBidirectionalPagination_VerifiesNaturalOrderAcrossPages()
    {
        await ExecuteContinuousBidirectionalPaginationTestAsync(pageSize: 25);
    }

    [Fact]
    public async Task MsSql_DynamicSearch_WithKeysetBoundaries()
    {
        await ExecuteDynamicSearchWithKeysetBoundariesTestAsync("Quantum Dynamics", pageSize: 50);
    }

    [Fact]
    public async Task MsSql_TamperedCursor_GracefullyResetsToPage1()
    {
        await ExecuteTamperedCursorGracefulResetTestAsync();
    }
}

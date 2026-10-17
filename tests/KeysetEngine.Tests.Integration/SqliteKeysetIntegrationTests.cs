using KeysetEngine.Core.Search;
using KeysetEngine.Tests.Integration.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KeysetEngine.Tests.Integration;

public class SqliteKeysetIntegrationTests : KeysetIntegrationTestBase, IAsyncLifetime, IDisposable
{
    private SqliteConnection _connection = null!;
    private DbContextOptions<TestDbContext> _options = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();

        _options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var context = CreateDbContext();
        await TestDataSeeder.SeedDatabaseAsync(context, count: 2000);
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
    }

    public void Dispose()
    {
        _connection?.Dispose();
        GC.SuppressFinalize(this);
    }

    protected override TestDbContext CreateDbContext()
    {
        return new TestDbContext(_options);
    }

    protected override ISearchStrategy GetSearchStrategy()
    {
        return DefaultSearchStrategy.Instance;
    }

    [Fact]
    public async Task Sqlite_SeamlessForwardPagination_NoDuplicatesNoGaps()
    {
        await ExecuteSeamlessForwardPaginationTestAsync(totalExpected: 2000, pageSize: 100);
    }

    [Fact]
    public async Task Sqlite_ContinuousBidirectionalPagination_VerifiesNaturalOrderAcrossPages()
    {
        await ExecuteContinuousBidirectionalPaginationTestAsync(pageSize: 25);
    }

    [Fact]
    public async Task Sqlite_DynamicSearch_WithKeysetBoundaries()
    {
        await ExecuteDynamicSearchWithKeysetBoundariesTestAsync("Quantum Dynamics", pageSize: 50);
    }

    [Fact]
    public async Task Sqlite_TamperedCursor_GracefullyResetsToPage1()
    {
        await ExecuteTamperedCursorGracefulResetTestAsync();
    }
}

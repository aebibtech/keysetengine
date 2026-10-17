using FluentAssertions;
using KeysetEngine.Core;
using KeysetEngine.Core.Configuration;
using KeysetEngine.Core.Search;
using KeysetEngine.Tests.Integration.Data;
using KeysetEngine.Tests.Integration.Models;

namespace KeysetEngine.Tests.Integration;

public abstract class KeysetIntegrationTestBase
{
    protected abstract TestDbContext CreateDbContext();
    protected abstract ISearchStrategy GetSearchStrategy();

    private KeysetConfig<TestOrder> CreateConfig()
    {
        return new KeysetConfigBuilder<TestOrder>()
            .SortBy(x => x.CreatedAt, ascending: true)
            .TieBreakBy(x => x.Id)
            .SearchIn(x => x.CustomerName, x => x.Description)
            .UseSearchStrategy(GetSearchStrategy())
            .WithEntityPurpose("TestOrder")
            .Build();
    }

    public async Task ExecuteSeamlessForwardPaginationTestAsync(int totalExpected = 2000, int pageSize = 100)
    {
        using var context = CreateDbContext();
        var config = CreateConfig();
        ICursorProtector protector = new Base64UrlCursorProtector();

        var retrievedIds = new List<long>(totalExpected);
        string? cursor = null;
        int pageCount = 0;

        while (true)
        {
            var request = new KeysetRequest(Cursor: cursor, Limit: pageSize, Direction: PageDirection.Forward);
            var page = await context.Orders.ToKeysetPageAsync(request, config, protector);

            pageCount++;
            retrievedIds.AddRange(page.Items.Select(x => x.Id));

            if (!page.HasNextPage)
            {
                break;
            }

            cursor = page.EndCursor;
            cursor.Should().NotBeNullOrWhiteSpace();
        }

        // Assert: All items retrieved seamlessly without gaps or duplicates
        retrievedIds.Should().HaveCount(totalExpected);
        retrievedIds.Should().OnlyHaveUniqueItems();
        retrievedIds.Should().BeInAscendingOrder();
    }

    public async Task ExecuteContinuousBidirectionalPaginationTestAsync(int pageSize = 25)
    {
        using var context = CreateDbContext();
        var config = CreateConfig();
        ICursorProtector protector = new Base64UrlCursorProtector();

        // Page 1
        var p1 = await context.Orders.ToKeysetPageAsync(new KeysetRequest(Limit: pageSize), config, protector);
        p1.Items.Should().HaveCount(pageSize);
        p1.HasPreviousPage.Should().BeFalse();
        p1.HasNextPage.Should().BeTrue();

        // Page 2
        var p2 = await context.Orders.ToKeysetPageAsync(new KeysetRequest(Cursor: p1.EndCursor, Limit: pageSize), config, protector);
        p2.Items.Should().HaveCount(pageSize);
        p2.HasPreviousPage.Should().BeTrue();
        p2.HasNextPage.Should().BeTrue();

        // Page 3
        var p3 = await context.Orders.ToKeysetPageAsync(new KeysetRequest(Cursor: p2.EndCursor, Limit: pageSize), config, protector);
        p3.Items.Should().HaveCount(pageSize);
        p3.HasPreviousPage.Should().BeTrue();

        // Move Backward: Page 3 -> Page 2
        var p2Back = await context.Orders.ToKeysetPageAsync(
            new KeysetRequest(Cursor: p3.StartCursor, Limit: pageSize, Direction: PageDirection.Backward),
            config,
            protector);

        p2Back.Items.Should().HaveCount(pageSize);
        p2Back.Items.Select(x => x.Id).Should().Equal(p2.Items.Select(x => x.Id));

        // Move Backward: Page 2 -> Page 1
        var p1Back = await context.Orders.ToKeysetPageAsync(
            new KeysetRequest(Cursor: p2Back.StartCursor, Limit: pageSize, Direction: PageDirection.Backward),
            config,
            protector);

        p1Back.Items.Should().HaveCount(pageSize);
        p1Back.HasPreviousPage.Should().BeFalse();
        p1Back.Items.Select(x => x.Id).Should().Equal(p1.Items.Select(x => x.Id));
    }

    public async Task ExecuteDynamicSearchWithKeysetBoundariesTestAsync(string searchTerm = "Quantum Dynamics", int pageSize = 50)
    {
        using var context = CreateDbContext();
        var config = CreateConfig();
        ICursorProtector protector = new Base64UrlCursorProtector();

        var searchIds = new List<long>();
        string? cursor = null;

        while (true)
        {
            var req = new KeysetRequest(Cursor: cursor, Limit: pageSize, Search: searchTerm);
            var page = await context.Orders.ToKeysetPageAsync(req, config, protector);

            searchIds.AddRange(page.Items.Select(x => x.Id));

            foreach (var item in page.Items)
            {
                bool matches = item.CustomerName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                               item.Description.Contains(searchTerm, StringComparison.OrdinalIgnoreCase);
                matches.Should().BeTrue();
            }

            if (!page.HasNextPage)
            {
                break;
            }

            cursor = page.EndCursor;
        }

        searchIds.Should().NotBeEmpty();
        searchIds.Should().OnlyHaveUniqueItems();
        searchIds.Should().BeInAscendingOrder();
    }

    public async Task ExecuteTamperedCursorGracefulResetTestAsync()
    {
        using var context = CreateDbContext();
        var config = CreateConfig();
        ICursorProtector protector = new Base64UrlCursorProtector();

        // Act: Request with corrupted token
        var page = await context.Orders.ToKeysetPageAsync(
            new KeysetRequest(Cursor: "invalid-tampered-token-content", Limit: 20),
            config,
            protector);

        // Assert: Gracefully resets to Page 1
        page.Items.Should().HaveCount(20);
        page.HasPreviousPage.Should().BeFalse();
        page.Items[0].Id.Should().Be(1);
    }
}

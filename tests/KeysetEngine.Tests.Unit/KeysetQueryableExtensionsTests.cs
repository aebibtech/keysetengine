using System.Linq.Expressions;
using FluentAssertions;
using KeysetEngine.Core;
using KeysetEngine.Core.Configuration;
using Xunit;

namespace KeysetEngine.Tests.Unit;

public class KeysetQueryableExtensionsTests
{
    private sealed record Customer(int Id, string Name, DateTimeOffset CreatedAt, string Region);

    private static List<Customer> GenerateCustomers(int count)
    {
        var baseDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var list = new List<Customer>(count);

        for (int i = 1; i <= count; i++)
        {
            // Give every 3 items identical CreatedAt to test tie-breaker sorting
            DateTimeOffset date = baseDate.AddHours((i - 1) / 3);
            string region = (i % 2 == 0) ? "West" : "East";
            list.Add(new Customer(i, $"Customer_{i:D3}", date, region));
        }

        return list;
    }

    private static KeysetConfig<Customer> CreateCustomerConfig()
    {
        Expression<Func<Customer, DateTimeOffset>> sortExpr = x => x.CreatedAt;
        Expression<Func<Customer, int>> tieExpr = x => x.Id;
        Expression<Func<Customer, string>> nameExpr = x => x.Name;
        Expression<Func<Customer, string>> regionExpr = x => x.Region;

        return new KeysetConfig<Customer>(
            sortSelector: sortExpr,
            tieBreakerSelector: tieExpr,
            isAscending: true,
            searchSelectors: [nameExpr, regionExpr],
            entityPurpose: "Customer"
        );
    }

    [Fact]
    public async Task ToKeysetPageAsync_ForwardPagination_ThroughWholeDataset_NoDuplicatesNoGaps()
    {
        // Arrange
        var customers = GenerateCustomers(25);
        var config = CreateCustomerConfig();
        ICursorProtector protector = new Base64UrlCursorProtector();

        // --- Page 1 (Items 1 to 10) ---
        var req1 = new KeysetRequest(Limit: 10, Direction: PageDirection.Forward);
        var page1 = await customers.AsQueryable().ToKeysetPageAsync(req1, config, protector);

        page1.Items.Should().HaveCount(10);
        page1.HasPreviousPage.Should().BeFalse();
        page1.HasNextPage.Should().BeTrue();
        page1.StartCursor.Should().NotBeNull();
        page1.EndCursor.Should().NotBeNull();
        page1.Items.Select(x => x.Id).Should().Equal(1, 2, 3, 4, 5, 6, 7, 8, 9, 10);

        // --- Page 2 (Items 11 to 20) ---
        var req2 = new KeysetRequest(Cursor: page1.EndCursor, Limit: 10, Direction: PageDirection.Forward);
        var page2 = await customers.AsQueryable().ToKeysetPageAsync(req2, config, protector);

        page2.Items.Should().HaveCount(10);
        page2.HasPreviousPage.Should().BeTrue();
        page2.HasNextPage.Should().BeTrue();
        page2.Items.Select(x => x.Id).Should().Equal(11, 12, 13, 14, 15, 16, 17, 18, 19, 20);

        // --- Page 3 (Items 21 to 25) ---
        var req3 = new KeysetRequest(Cursor: page2.EndCursor, Limit: 10, Direction: PageDirection.Forward);
        var page3 = await customers.AsQueryable().ToKeysetPageAsync(req3, config, protector);

        page3.Items.Should().HaveCount(5);
        page3.HasPreviousPage.Should().BeTrue();
        page3.HasNextPage.Should().BeFalse();
        page3.Items.Select(x => x.Id).Should().Equal(21, 22, 23, 24, 25);
    }

    [Fact]
    public async Task ToKeysetPageAsync_BidirectionalNavigation_MaintainsForwardOrderAfterReversal()
    {
        // Arrange
        var customers = GenerateCustomers(25);
        var config = CreateCustomerConfig();
        ICursorProtector protector = new Base64UrlCursorProtector();

        // 1. Advance to Page 2
        var p1 = await customers.AsQueryable().ToKeysetPageAsync(
            new KeysetRequest(Limit: 10), config, protector);

        var p2 = await customers.AsQueryable().ToKeysetPageAsync(
            new KeysetRequest(Cursor: p1.EndCursor, Limit: 10), config, protector);

        // 2. Advance to Page 3
        var p3 = await customers.AsQueryable().ToKeysetPageAsync(
            new KeysetRequest(Cursor: p2.EndCursor, Limit: 10), config, protector);

        // 3. Move Backward from Page 3 StartCursor -> should get Page 2 (Items 11 to 20)
        var p2Back = await customers.AsQueryable().ToKeysetPageAsync(
            new KeysetRequest(Cursor: p3.StartCursor, Limit: 10, Direction: PageDirection.Backward),
            config,
            protector);

        p2Back.Items.Should().HaveCount(10);
        p2Back.HasPreviousPage.Should().BeTrue();
        p2Back.HasNextPage.Should().BeTrue();
        // Crucial: Must be in ascending order (11..20), NOT reversed (20..11)
        p2Back.Items.Select(x => x.Id).Should().Equal(11, 12, 13, 14, 15, 16, 17, 18, 19, 20);

        // 4. Move Backward from Page 2 StartCursor -> should get Page 1 (Items 1 to 10)
        var p1Back = await customers.AsQueryable().ToKeysetPageAsync(
            new KeysetRequest(Cursor: p2Back.StartCursor, Limit: 10, Direction: PageDirection.Backward),
            config,
            protector);

        p1Back.Items.Should().HaveCount(10);
        p1Back.HasPreviousPage.Should().BeFalse();
        p1Back.HasNextPage.Should().BeTrue();
        p1Back.Items.Select(x => x.Id).Should().Equal(1, 2, 3, 4, 5, 6, 7, 8, 9, 10);
    }

    [Fact]
    public async Task ToKeysetPageAsync_WithSearchTerm_FiltersCorrectly()
    {
        // Arrange
        var customers = GenerateCustomers(30);
        var config = CreateCustomerConfig();
        ICursorProtector protector = new Base64UrlCursorProtector();

        // Act: Search for "West" (even numbers: 2, 4, 6...)
        var page = await customers.AsQueryable().ToKeysetPageAsync(
            new KeysetRequest(Limit: 5, Search: "West"),
            config,
            protector);

        // Assert
        page.Items.Should().HaveCount(5);
        page.Items.Select(x => x.Id).Should().Equal(2, 4, 6, 8, 10);
        page.HasNextPage.Should().BeTrue();
    }

    [Fact]
    public async Task ToKeysetPageAsync_SearchChange_InvalidatesStaleCursor_AndRestartsAtPage1()
    {
        // Arrange
        var customers = GenerateCustomers(30);
        var config = CreateCustomerConfig();
        ICursorProtector protector = new Base64UrlCursorProtector();

        // 1. Get page 1 under search "West"
        var p1 = await customers.AsQueryable().ToKeysetPageAsync(
            new KeysetRequest(Limit: 5, Search: "West"), config, protector);

        // 2. Submit p1.EndCursor but with a DIFFERENT search filter "East" (odd numbers: 1, 3, 5...)
        var p2WithNewSearch = await customers.AsQueryable().ToKeysetPageAsync(
            new KeysetRequest(Cursor: p1.EndCursor, Limit: 5, Search: "East"), config, protector);

        // Assert: Cursor was invalidated because search changed -> restarted at Page 1 of "East"
        p2WithNewSearch.HasPreviousPage.Should().BeFalse();
        p2WithNewSearch.Items.Select(x => x.Id).Should().Equal(1, 3, 5, 7, 9);
    }

    [Fact]
    public async Task ToKeysetPageAsync_InvalidOrTamperedCursor_GracefullyFallsBackToPage1()
    {
        // Arrange
        var customers = GenerateCustomers(15);
        var config = CreateCustomerConfig();
        ICursorProtector protector = new Base64UrlCursorProtector();

        // Act: Provide corrupted garbage cursor
        var page = await customers.AsQueryable().ToKeysetPageAsync(
            new KeysetRequest(Cursor: "corrupt_token_payload", Limit: 5),
            config,
            protector);

        // Assert: Gracefully falls back to Page 1
        page.HasPreviousPage.Should().BeFalse();
        page.Items.Select(x => x.Id).Should().Equal(1, 2, 3, 4, 5);
    }

    [Fact]
    public async Task ToKeysetPageAsync_EmptyDataset_ReturnsEmptyEnvelopeWithNullCursors()
    {
        // Arrange
        var emptyList = new List<Customer>();
        var config = CreateCustomerConfig();
        ICursorProtector protector = new Base64UrlCursorProtector();

        // Act
        var page = await emptyList.AsQueryable().ToKeysetPageAsync(
            new KeysetRequest(Limit: 10), config, protector);

        // Assert
        page.Items.Should().BeEmpty();
        page.HasNextPage.Should().BeFalse();
        page.HasPreviousPage.Should().BeFalse();
        page.StartCursor.Should().BeNull();
        page.EndCursor.Should().BeNull();
    }
}

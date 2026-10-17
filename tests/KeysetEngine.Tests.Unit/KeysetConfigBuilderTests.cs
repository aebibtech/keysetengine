using FluentAssertions;
using KeysetEngine.Core;
using KeysetEngine.Core.Configuration;
using KeysetEngine.Core.Search;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace KeysetEngine.Tests.Unit;

public class KeysetConfigBuilderTests
{
    private sealed record Order(int Id, DateTime CreatedAt, string CustomerName, string? Notes);
    private sealed record InvalidOrder(int? NullableId, DateTime? NullableDate, string Customer);

    public KeysetConfigBuilderTests()
    {
        KeysetConfigRegistry.Clear();
    }

    [Fact]
    public void Builder_ConfiguresAndBuildsKeysetConfig_WithCachedDefinition()
    {
        // Arrange
        var builder = new KeysetConfigBuilder<Order>();

        // Act
        var config = builder
            .SortBy(x => x.CreatedAt, ascending: false)
            .TieBreakBy(x => x.Id)
            .SearchIn(x => x.CustomerName, x => x.Notes)
            .WithEntityPurpose("Order_v1")
            .Build();

        // Assert
        config.Should().NotBeNull();
        config.IsAscending.Should().BeFalse();
        config.EntityPurpose.Should().Be("Order_v1");
        config.SearchSelectors.Should().HaveCount(2);

        // Verify cached definition
        config.Definition.Should().NotBeNull();
        config.Definition!.SortProperty.Name.Should().Be(nameof(Order.CreatedAt));
        config.Definition.TieBreakerProperty.Name.Should().Be(nameof(Order.Id));
        config.Definition.SearchProperties.Select(p => p.Name).Should().Equal(nameof(Order.CustomerName), nameof(Order.Notes));
        config.Definition.IsAscending.Should().BeFalse();
        config.Definition.EntityPurpose.Should().Be("Order_v1");

        // Verify registry cache
        KeysetConfigRegistry.Get<Order>().Should().BeSameAs(config);
        KeysetConfigRegistry.GetDefinition(typeof(Order)).Should().BeSameAs(config.Definition);
    }

    [Fact]
    public void Builder_WithoutSortBy_ThrowsInvalidOperationException()
    {
        var builder = new KeysetConfigBuilder<Order>();
        builder.TieBreakBy(x => x.Id);

        Action act = () => builder.Build();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*must specify a Sort property via SortBy()*");
    }

    [Fact]
    public void Builder_WithoutTieBreakBy_ThrowsInvalidOperationException()
    {
        var builder = new KeysetConfigBuilder<Order>();
        builder.SortBy(x => x.CreatedAt);

        Action act = () => builder.Build();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*must specify a Tie-breaker property via TieBreakBy()*");
    }

    [Fact]
    public void Builder_WithNullableSortProperty_ThrowsInvalidOperationException()
    {
        var builder = new KeysetConfigBuilder<InvalidOrder>();

        Action act = () => builder.SortBy(x => x.NullableDate);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*must be non-nullable*");
    }

    [Fact]
    public void Builder_WithNullableTieBreakerProperty_ThrowsInvalidOperationException()
    {
        var builder = new KeysetConfigBuilder<InvalidOrder>();

        Action act = () => builder.TieBreakBy(x => x.NullableId);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*must be non-nullable*");
    }

    [Fact]
    public void Builder_WithComplexExpression_ThrowsArgumentException()
    {
        var builder = new KeysetConfigBuilder<Order>();

        Action act = () => builder.SortBy(x => x.Id + 1);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*must be a member property access expression*");
    }

    [Fact]
    public async Task AddKeysetPagination_RegistersInDI_AndAllowsImplicitConfigPagination()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddKeysetPagination(options =>
        {
            options.Configure<Order>(b => b
                .SortBy(x => x.CreatedAt, ascending: true)
                .TieBreakBy(x => x.Id)
                .SearchIn(x => x.CustomerName));
        });

        // Act & Assert: config is registered in registry
        KeysetConfig<Order>? registered = KeysetConfigRegistry.Get<Order>();
        registered.Should().NotBeNull();
        registered!.IsAscending.Should().BeTrue();

        // Verify implicit overload pagination (without explicitly passing config)
        var orders = new List<Order>
        {
            new(1, DateTime.UtcNow.AddMinutes(-10), "Alice", null),
            new(2, DateTime.UtcNow.AddMinutes(-5), "Bob", null),
            new(3, DateTime.UtcNow, "Charlie", null)
        };

        ICursorProtector protector = new Base64UrlCursorProtector();
        var page = await orders.AsQueryable().ToKeysetPageAsync(new KeysetRequest(Limit: 2), protector);

        page.Items.Should().HaveCount(2);
        page.Items.Select(x => x.CustomerName).Should().Equal("Alice", "Bob");
        page.HasNextPage.Should().BeTrue();
    }
}

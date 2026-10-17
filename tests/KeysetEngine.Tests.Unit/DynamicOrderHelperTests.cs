using System.Linq.Expressions;
using FluentAssertions;
using KeysetEngine.Core;
using KeysetEngine.Core.Expressions;
using Xunit;

namespace KeysetEngine.Tests.Unit;

public class DynamicOrderHelperTests
{
    private sealed record Product(int Id, string Category, decimal Price);

    private static List<Product> CreateSampleProducts() =>
    [
        new(1, "Electronics", 100m),
        new(2, "Books", 20m),
        new(3, "Electronics", 50m),
        new(4, "Books", 100m),
        new(5, "Clothing", 50m)
    ];

    [Fact]
    public void ApplyOrdering_ForwardAscending_OrdersBySortAscendingThenTieBreakerAscending()
    {
        // Arrange
        IQueryable<Product> query = CreateSampleProducts().AsQueryable();
        Expression<Func<Product, decimal>> sortExpr = x => x.Price;
        Expression<Func<Product, int>> tieExpr = x => x.Id;

        // Act
        IQueryable<Product> ordered = DynamicOrderHelper.ApplyOrdering(
            query,
            sortExpr,
            tieExpr,
            isAscending: true,
            direction: PageDirection.Forward);

        List<Product> result = ordered.ToList();

        // Assert: Price 20 (id 2), Price 50 (id 3, then 5), Price 100 (id 1, then 4)
        result.Select(x => x.Id).Should().Equal(2, 3, 5, 1, 4);
    }

    [Fact]
    public void ApplyOrdering_ForwardDescending_OrdersBySortDescendingThenTieBreakerDescending()
    {
        // Arrange
        IQueryable<Product> query = CreateSampleProducts().AsQueryable();
        Expression<Func<Product, decimal>> sortExpr = x => x.Price;
        Expression<Func<Product, int>> tieExpr = x => x.Id;

        // Act
        IQueryable<Product> ordered = DynamicOrderHelper.ApplyOrdering(
            query,
            sortExpr,
            tieExpr,
            isAscending: false,
            direction: PageDirection.Forward);

        List<Product> result = ordered.ToList();

        // Assert: Price 100 (id 4, then 1), Price 50 (id 5, then 3), Price 20 (id 2)
        result.Select(x => x.Id).Should().Equal(4, 1, 5, 3, 2);
    }

    [Fact]
    public void ApplyOrdering_BackwardAscending_InvertsSqlOrderToDescending()
    {
        // Arrange
        IQueryable<Product> query = CreateSampleProducts().AsQueryable();
        Expression<Func<Product, decimal>> sortExpr = x => x.Price;
        Expression<Func<Product, int>> tieExpr = x => x.Id;

        // Act
        IQueryable<Product> ordered = DynamicOrderHelper.ApplyOrdering(
            query,
            sortExpr,
            tieExpr,
            isAscending: true,
            direction: PageDirection.Backward);

        List<Product> result = ordered.ToList();

        // Assert: Inverted sequence matches Forward Descending
        result.Select(x => x.Id).Should().Equal(4, 1, 5, 3, 2);
    }

    [Fact]
    public void ApplyOrdering_BackwardDescending_InvertsSqlOrderToAscending()
    {
        // Arrange
        IQueryable<Product> query = CreateSampleProducts().AsQueryable();
        Expression<Func<Product, decimal>> sortExpr = x => x.Price;
        Expression<Func<Product, int>> tieExpr = x => x.Id;

        // Act
        IQueryable<Product> ordered = DynamicOrderHelper.ApplyOrdering(
            query,
            sortExpr,
            tieExpr,
            isAscending: false,
            direction: PageDirection.Backward);

        List<Product> result = ordered.ToList();

        // Assert: Inverted sequence matches Forward Ascending
        result.Select(x => x.Id).Should().Equal(2, 3, 5, 1, 4);
    }

    [Fact]
    public void ApplyOrdering_NullArguments_ThrowsArgumentNullException()
    {
        IQueryable<Product> query = CreateSampleProducts().AsQueryable();
        Expression<Func<Product, decimal>> sortExpr = x => x.Price;
        Expression<Func<Product, int>> tieExpr = x => x.Id;

        Action act1 = () => DynamicOrderHelper.ApplyOrdering<Product>(
            null!, sortExpr, tieExpr, true, PageDirection.Forward);
        Action act2 = () => DynamicOrderHelper.ApplyOrdering(
            query, null!, tieExpr, true, PageDirection.Forward);
        Action act3 = () => DynamicOrderHelper.ApplyOrdering(
            query, sortExpr, null!, true, PageDirection.Forward);

        act1.Should().Throw<ArgumentNullException>();
        act2.Should().Throw<ArgumentNullException>();
        act3.Should().Throw<ArgumentNullException>();
    }
}

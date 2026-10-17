using System.Linq.Expressions;
using System.Text.Json;
using FluentAssertions;
using KeysetEngine.Core;
using KeysetEngine.Core.Expressions;
using Xunit;

namespace KeysetEngine.Tests.Unit;

public class KeysetExpressionBuilderTests
{
    private sealed record TestEntity(int Id, string Category, DateTimeOffset CreatedAt);
    private sealed record GuidEntity(Guid Id, int Score);

    [Fact]
    public void BuildPredicate_GeneratesUniversalNestedBooleanDisjunction_Shape()
    {
        // Arrange
        var date = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        // Act
        Expression<Func<TestEntity, bool>> lambda = KeysetExpressionBuilder.BuildPredicate<TestEntity, DateTimeOffset, int>(
            x => x.CreatedAt,
            x => x.Id,
            date,
            10,
            isAscending: true,
            direction: PageDirection.Forward);

        // Assert expression tree shape: (A > a) OR (A == a AND B > b)
        lambda.Body.Should().BeAssignableTo<BinaryExpression>();
        var orElse = (BinaryExpression)lambda.Body;
        orElse.NodeType.Should().Be(ExpressionType.OrElse);

        // Left of OR: (A > a)
        orElse.Left.Should().BeAssignableTo<BinaryExpression>();
        orElse.Left.NodeType.Should().Be(ExpressionType.GreaterThan);

        // Right of OR: (A == a AND B > b)
        orElse.Right.Should().BeAssignableTo<BinaryExpression>();
        var andAlso = (BinaryExpression)orElse.Right;
        andAlso.NodeType.Should().Be(ExpressionType.AndAlso);

        andAlso.Left.NodeType.Should().Be(ExpressionType.Equal);
        andAlso.Right.NodeType.Should().Be(ExpressionType.GreaterThan);
    }

    [Theory]
    [InlineData(true, PageDirection.Forward, ExpressionType.GreaterThan)]
    [InlineData(false, PageDirection.Forward, ExpressionType.LessThan)]
    [InlineData(true, PageDirection.Backward, ExpressionType.LessThan)]
    [InlineData(false, PageDirection.Backward, ExpressionType.GreaterThan)]
    public void BuildPredicate_OperatorInversion_MatchesDirectionAndSortOrder(
        bool isAscending,
        PageDirection direction,
        ExpressionType expectedInequalityType)
    {
        // Act
        Expression<Func<TestEntity, bool>> lambda = KeysetExpressionBuilder.BuildPredicate<TestEntity, DateTimeOffset, int>(
            x => x.CreatedAt,
            x => x.Id,
            DateTimeOffset.UtcNow,
            1,
            isAscending,
            direction);

        var orElse = (BinaryExpression)lambda.Body;
        var andAlso = (BinaryExpression)orElse.Right;

        // Primary inequality operator
        orElse.Left.NodeType.Should().Be(expectedInequalityType);
        // Tie-breaker inequality operator
        andAlso.Right.NodeType.Should().Be(expectedInequalityType);
        // Equality operator is always Equal
        andAlso.Left.NodeType.Should().Be(ExpressionType.Equal);
    }

    [Fact]
    public void BuildPredicate_EvaluatesCorrectly_OnInMemoryDataset_WithDuplicateSortKeys()
    {
        // Arrange: 5 items, some sharing identical CreatedAt timestamps
        var t0 = new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
        var t1 = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var t2 = new DateTimeOffset(2026, 1, 1, 14, 0, 0, TimeSpan.Zero);

        var items = new List<TestEntity>
        {
            new(1, "A", t0),
            new(2, "B", t1),
            new(3, "C", t1), // Same date as item 2, higher ID
            new(4, "D", t1), // Same date as item 2 & 3, higher ID
            new(5, "E", t2)
        };

        // Forward Ascending from Item 2 (t1, Id: 2)
        var predicate = KeysetExpressionBuilder.BuildPredicate<TestEntity, DateTimeOffset, int>(
            x => x.CreatedAt,
            x => x.Id,
            t1,
            2,
            isAscending: true,
            direction: PageDirection.Forward);

        // Act
        var filtered = items.AsQueryable().Where(predicate).ToList();

        // Assert: Should include Item 3 (same date, id 3), Item 4 (same date, id 4), and Item 5 (higher date)
        filtered.Select(x => x.Id).Should().Equal(3, 4, 5);
    }

    [Fact]
    public void BuildPredicate_WithStrings_EmitsStringCompare_AndEvaluatesCorrectly()
    {
        // Arrange
        var items = new List<TestEntity>
        {
            new(1, "Alpha", DateTimeOffset.UtcNow),
            new(2, "Bravo", DateTimeOffset.UtcNow),
            new(3, "Charlie", DateTimeOffset.UtcNow),
            new(4, "Delta", DateTimeOffset.UtcNow)
        };

        // Forward Ascending after "Bravo", tie-breaker 2
        var predicate = KeysetExpressionBuilder.BuildPredicate<TestEntity, string, int>(
            x => x.Category,
            x => x.Id,
            "Bravo",
            2,
            isAscending: true,
            direction: PageDirection.Forward);

        // Act
        var filtered = items.AsQueryable().Where(predicate).ToList();

        // Assert
        filtered.Select(x => x.Category).Should().Equal("Charlie", "Delta");
    }

    [Fact]
    public void BuildPredicate_WithGuidTieBreaker_EvaluatesCorrectly()
    {
        // Arrange
        var g1 = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var g2 = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var g3 = Guid.Parse("00000000-0000-0000-0000-000000000003");

        var items = new List<GuidEntity>
        {
            new(g1, 100),
            new(g2, 100), // Same score, higher Guid
            new(g3, 200)
        };

        // Forward Ascending from (100, g1)
        var predicate = KeysetExpressionBuilder.BuildPredicate<GuidEntity, int, Guid>(
            x => x.Score,
            x => x.Id,
            100,
            g1,
            isAscending: true,
            direction: PageDirection.Forward);

        // Act
        var filtered = items.AsQueryable().Where(predicate).ToList();

        // Assert
        filtered.Select(x => x.Id).Should().Equal(g2, g3);
    }

    [Fact]
    public void BuildPredicate_FromCursorToken_DeserializesAndFiltersCorrectly()
    {
        // Arrange
        var date = new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);
        var token = new KeysetCursorToken(
            SortValueJson: JsonSerializer.Serialize(date),
            TieBreakerValueJson: JsonSerializer.Serialize(3),
            IsAscending: true,
            SearchHash: null
        );

        var items = new List<TestEntity>
        {
            new(1, "A", date.AddDays(-1)),
            new(2, "B", date),
            new(3, "C", date),
            new(4, "D", date),
            new(5, "E", date.AddDays(1))
        };

        Expression<Func<TestEntity, DateTimeOffset>> sortExpr = x => x.CreatedAt;
        Expression<Func<TestEntity, int>> tieExpr = x => x.Id;

        // Act
        var predicate = KeysetExpressionBuilder.BuildPredicate<TestEntity>(
            sortExpr,
            tieExpr,
            token,
            PageDirection.Forward);

        var filtered = items.AsQueryable().Where(predicate).ToList();

        // Assert
        filtered.Select(x => x.Id).Should().Equal(4, 5);
    }

    [Fact]
    public void BuildPredicate_NullParameters_ThrowsArgumentNullException()
    {
        Expression<Func<TestEntity, string>> sortExpr = x => x.Category;
        Expression<Func<TestEntity, int>> tieExpr = x => x.Id;

        Action act1 = () => KeysetExpressionBuilder.BuildPredicate<TestEntity, string, int>(
            null!, tieExpr, "val", 1, true, PageDirection.Forward);
        Action act2 = () => KeysetExpressionBuilder.BuildPredicate<TestEntity, string, int>(
            sortExpr, null!, "val", 1, true, PageDirection.Forward);
        Action act3 = () => KeysetExpressionBuilder.BuildPredicate<TestEntity, string, int>(
            sortExpr, tieExpr, null!, 1, true, PageDirection.Forward);

        act1.Should().Throw<ArgumentNullException>();
        act2.Should().Throw<ArgumentNullException>();
        act3.Should().Throw<ArgumentNullException>();
    }
}

using System.Linq.Expressions;
using FluentAssertions;
using KeysetEngine.Core.Expressions;
using KeysetEngine.Core.Search;
using Xunit;

namespace KeysetEngine.Tests.Unit;

public class SearchStrategyTests
{
    private sealed record Article(int Id, string Title, string? Content);

    [Fact]
    public void DefaultSearchStrategy_MatchesSubstring_CaseInsensitive()
    {
        // Arrange
        var strategy = DefaultSearchStrategy.Instance;
        ParameterExpression param = Expression.Parameter(typeof(Article), "x");
        MemberExpression propAccess = Expression.Property(param, nameof(Article.Title));

        Expression predicateBody = strategy.BuildPredicate(propAccess, "NET");
        var lambda = Expression.Lambda<Func<Article, bool>>(predicateBody, param);

        var articles = new List<Article>
        {
            new(1, "Modern .NET Keyset Pagination", "High performance"),
            new(2, "PostgreSQL Tuning", "Index optimization"),
            new(3, "ASP.NET Core Web APIs", "Minimal endpoints")
        };

        // Act
        var result = articles.AsQueryable().Where(lambda).ToList();

        // Assert
        result.Select(x => x.Id).Should().Equal(1, 3);
    }

    [Fact]
    public void DefaultSearchStrategy_WithNullPropertyValue_DoesNotThrowNullReferenceException()
    {
        // Arrange
        var strategy = DefaultSearchStrategy.Instance;
        ParameterExpression param = Expression.Parameter(typeof(Article), "x");
        MemberExpression propAccess = Expression.Property(param, nameof(Article.Content));

        Expression predicateBody = strategy.BuildPredicate(propAccess, "optimization");
        var lambda = Expression.Lambda<Func<Article, bool>>(predicateBody, param);

        var articles = new List<Article>
        {
            new(1, "Article 1", null), // null Content
            new(2, "Article 2", "Index optimization"),
            new(3, "Article 3", null)
        };

        // Act
        var result = articles.AsQueryable().Where(lambda).ToList();

        // Assert
        result.Select(x => x.Id).Should().Equal(2);
    }

    [Fact]
    public void ApplySearch_CombinesMultipleProperties_WithOrElseDisjunction()
    {
        // Arrange
        var articles = new List<Article>
        {
            new(1, "Architecture Patterns", "Clean decoupled boundaries"),
            new(2, "Database Indexing", "B-tree and GIN structures"),
            new(3, "Cloud Deployment", "Docker containers in Azure")
        };

        Expression<Func<Article, string>> titleSel = x => x.Title;
        Expression<Func<Article, string?>> contentSel = x => x.Content;
        var selectors = new LambdaExpression[] { titleSel, contentSel };

        // Act 1: Matches in Content ("decoupled")
        var pred1 = KeysetExpressionBuilder.ApplySearch<Article>(selectors, "decoupled");
        var res1 = articles.AsQueryable().Where(pred1!).ToList();

        // Act 2: Matches in Title ("Database")
        var pred2 = KeysetExpressionBuilder.ApplySearch<Article>(selectors, "database");
        var res2 = articles.AsQueryable().Where(pred2!).ToList();

        // Act 3: Matches both or either
        var pred3 = KeysetExpressionBuilder.ApplySearch<Article>(selectors, "azure");
        var res3 = articles.AsQueryable().Where(pred3!).ToList();

        // Assert
        res1.Select(x => x.Id).Should().Equal(1);
        res2.Select(x => x.Id).Should().Equal(2);
        res3.Select(x => x.Id).Should().Equal(3);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ApplySearch_NullOrWhitespaceSearch_ReturnsNull(string? searchTerm)
    {
        Expression<Func<Article, string>> titleSel = x => x.Title;
        var selectors = new LambdaExpression[] { titleSel };

        var pred = KeysetExpressionBuilder.ApplySearch<Article>(selectors, searchTerm);

        pred.Should().BeNull();
    }
}

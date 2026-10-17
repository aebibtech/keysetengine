using System.Linq.Expressions;
using FluentAssertions;
using KeysetEngine.Core.Configuration;
using KeysetEngine.PostgreSql;
using KeysetEngine.SqlServer;
using Xunit;

namespace KeysetEngine.Tests.Unit;

public class DatabaseSearchStrategyTests
{
    private sealed record Product(int Id, string Title);

    [Fact]
    public void PostgreSqlSearchStrategy_EmitsILikeCallExpression()
    {
        // Arrange
        var strategy = PostgreSqlSearchStrategy.Instance;
        ParameterExpression param = Expression.Parameter(typeof(Product), "x");
        MemberExpression propAccess = Expression.Property(param, nameof(Product.Title));

        // Act
        Expression predicate = strategy.BuildPredicate(propAccess, "Headphones");

        // Assert: EF.Functions.ILike(x.Title, "%Headphones%")
        predicate.Should().BeAssignableTo<MethodCallExpression>();
        var call = (MethodCallExpression)predicate;
        call.Method.Name.Should().Be("ILike");
        call.Arguments.Should().HaveCount(3);
        call.Arguments[1].Should().BeSameAs(propAccess);
        call.Arguments[2].Should().BeOfType<ConstantExpression>()
            .Which.Value.Should().Be("%Headphones%");
    }

    [Fact]
    public void PostgreSqlTsVectorSearchStrategy_EmitsTsVectorExpressions()
    {
        // Arrange
        var strategy = new PostgreSqlTsVectorSearchStrategy("english");
        ParameterExpression param = Expression.Parameter(typeof(Product), "x");
        MemberExpression propAccess = Expression.Property(param, nameof(Product.Title));

        // Act
        Expression predicate = strategy.BuildPredicate(propAccess, "keyboard");

        // Assert: Matches(ToTsVector("english", x.Title), PlainToTsQuery("english", "keyboard"))
        predicate.Should().BeAssignableTo<MethodCallExpression>();
        var call = (MethodCallExpression)predicate;
        call.Method.Name.Should().Be("Matches");
    }

    [Fact]
    public void SqlServerFullTextSearchStrategy_EmitsFreeTextCall()
    {
        // Arrange
        var strategy = new SqlServerFullTextSearchStrategy(useContains: false);
        ParameterExpression param = Expression.Parameter(typeof(Product), "x");
        MemberExpression propAccess = Expression.Property(param, nameof(Product.Title));

        // Act
        Expression predicate = strategy.BuildPredicate(propAccess, "wireless mouse");

        // Assert
        predicate.Should().BeAssignableTo<MethodCallExpression>();
        var call = (MethodCallExpression)predicate;
        call.Method.Name.Should().Be("FreeText");
        call.Arguments[2].Should().BeOfType<ConstantExpression>()
            .Which.Value.Should().Be("wireless mouse");
    }

    [Fact]
    public void SqlServerFullTextSearchStrategy_WithContains_EmitsContainsCall()
    {
        // Arrange
        var strategy = new SqlServerFullTextSearchStrategy(useContains: true);
        ParameterExpression param = Expression.Parameter(typeof(Product), "x");
        MemberExpression propAccess = Expression.Property(param, nameof(Product.Title));

        // Act
        Expression predicate = strategy.BuildPredicate(propAccess, "FORMSOF(INFLECTIONAL, run)");

        // Assert
        predicate.Should().BeAssignableTo<MethodCallExpression>();
        var call = (MethodCallExpression)predicate;
        call.Method.Name.Should().Be("Contains");
    }

    [Fact]
    public void SqlServerCollationSearchStrategy_EmitsCaseInsensitiveContains_WithoutToLower()
    {
        // Arrange
        var strategy = SqlServerCollationSearchStrategy.Instance;
        ParameterExpression param = Expression.Parameter(typeof(Product), "x");
        MemberExpression propAccess = Expression.Property(param, nameof(Product.Title));

        // Act
        Expression predicate = strategy.BuildPredicate(propAccess, "Monitor");

        // Assert: (x.Title != null) AND x.Title.Contains("Monitor")
        predicate.Should().BeAssignableTo<BinaryExpression>();
        var andAlso = (BinaryExpression)predicate;
        andAlso.NodeType.Should().Be(ExpressionType.AndAlso);

        andAlso.Right.Should().BeAssignableTo<MethodCallExpression>();
        var call = (MethodCallExpression)andAlso.Right;
        call.Method.Name.Should().Be("Contains");
        call.Object.Should().BeSameAs(propAccess); // directly invoked on propAccess without ToLower()
    }

    [Fact]
    public void KeysetEngineOptionsExtensions_ConfigureDialectsCorrectly()
    {
        var optionsPg = new KeysetEngineOptions();
        optionsPg.UsePostgreSqlSearch();
        optionsPg.DefaultSearchStrategy.Should().BeOfType<PostgreSqlSearchStrategy>();

        var optionsSql = new KeysetEngineOptions();
        optionsSql.UseSqlServerFullTextSearch();
        optionsSql.DefaultSearchStrategy.Should().BeOfType<SqlServerFullTextSearchStrategy>();

        var optionsCollation = new KeysetEngineOptions();
        optionsCollation.UseSqlServerCollationSearch();
        optionsCollation.DefaultSearchStrategy.Should().BeOfType<SqlServerCollationSearchStrategy>();
    }
}

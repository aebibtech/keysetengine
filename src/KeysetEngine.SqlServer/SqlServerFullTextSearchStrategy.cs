using System.Linq.Expressions;
using System.Reflection;
using KeysetEngine.Core.Search;
using Microsoft.EntityFrameworkCore;

namespace KeysetEngine.SqlServer;

/// <summary>
/// SQL Server full-text search strategy utilizing SQL Server Full-Text Catalogs via
/// <c>EF.Functions.FreeText</c> or <c>EF.Functions.Contains</c>.
/// </summary>
public sealed class SqlServerFullTextSearchStrategy : ISearchStrategy
{
    private static readonly MethodInfo FreeTextMethod = typeof(SqlServerDbFunctionsExtensions)
        .GetMethod(nameof(SqlServerDbFunctionsExtensions.FreeText), [typeof(DbFunctions), typeof(string), typeof(string)])
        ?? throw new InvalidOperationException("SqlServerDbFunctionsExtensions.FreeText method could not be located.");

    private static readonly MethodInfo ContainsMethod = typeof(SqlServerDbFunctionsExtensions)
        .GetMethod(nameof(SqlServerDbFunctionsExtensions.Contains), [typeof(DbFunctions), typeof(string), typeof(string)])
        ?? throw new InvalidOperationException("SqlServerDbFunctionsExtensions.Contains method could not be located.");

    private readonly bool _useContains;

    /// <summary>
    /// Initializes a new instance of <see cref="SqlServerFullTextSearchStrategy"/>.
    /// </summary>
    /// <param name="useContains">If true, emits CONTAINS(); if false (default), emits FREETEXT().</param>
    public SqlServerFullTextSearchStrategy(bool useContains = false)
    {
        _useContains = useContains;
    }

    /// <inheritdoc />
    public Expression BuildPredicate(Expression propertyAccess, string searchTerm)
    {
        ArgumentNullException.ThrowIfNull(propertyAccess);
        ArgumentNullException.ThrowIfNull(searchTerm);

        MemberExpression efFunctions = Expression.Property(null, typeof(EF), nameof(EF.Functions));
        ConstantExpression queryConst = Expression.Constant(searchTerm.Trim(), typeof(string));

        MethodInfo method = _useContains ? ContainsMethod : FreeTextMethod;

        return Expression.Call(method, efFunctions, propertyAccess, queryConst);
    }
}

using System.Linq.Expressions;
using System.Reflection;
using KeysetEngine.Core.Search;

namespace KeysetEngine.SqlServer;

/// <summary>
/// SQL Server search strategy relying on SQL Server's default case-insensitive collation (CI_AS).
/// Emits standard string.Contains() without ToLower(), preserving SARGability and index seek capabilities.
/// </summary>
public sealed class SqlServerCollationSearchStrategy : ISearchStrategy
{
    private static readonly MethodInfo StringContainsMethod = typeof(string)
        .GetMethod(nameof(string.Contains), [typeof(string)])
        ?? throw new InvalidOperationException("string.Contains(string) could not be located.");

    /// <summary>
    /// Singleton instance of <see cref="SqlServerCollationSearchStrategy"/>.
    /// </summary>
    public static SqlServerCollationSearchStrategy Instance { get; } = new();

    /// <inheritdoc />
    public Expression BuildPredicate(Expression propertyAccess, string searchTerm)
    {
        ArgumentNullException.ThrowIfNull(propertyAccess);
        ArgumentNullException.ThrowIfNull(searchTerm);

        // property != null && property.Contains(searchTerm)
        BinaryExpression notNull = Expression.NotEqual(propertyAccess, Expression.Constant(null, typeof(string)));
        ConstantExpression termConst = Expression.Constant(searchTerm.Trim(), typeof(string));
        MethodCallExpression containsCall = Expression.Call(propertyAccess, StringContainsMethod, termConst);

        return Expression.AndAlso(notNull, containsCall);
    }
}

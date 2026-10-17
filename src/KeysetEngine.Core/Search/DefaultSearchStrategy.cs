using System.Linq.Expressions;
using System.Reflection;

namespace KeysetEngine.Core.Search;

/// <summary>
/// Universal text search strategy that performs case-insensitive substring matching using LOWER() and Contains().
/// Translates cleanly across SQLite, PostgreSQL, SQL Server, and in-memory LINQ queries.
/// </summary>
public sealed class DefaultSearchStrategy : ISearchStrategy
{
    private static readonly MethodInfo ToLowerMethod = typeof(string)
        .GetMethod(nameof(string.ToLower), Type.EmptyTypes)!;

    private static readonly MethodInfo ContainsMethod = typeof(string)
        .GetMethod(nameof(string.Contains), [typeof(string)])!;

    /// <summary>
    /// Singleton instance of <see cref="DefaultSearchStrategy"/>.
    /// </summary>
    public static DefaultSearchStrategy Instance { get; } = new();

    /// <inheritdoc />
    public Expression BuildPredicate(Expression propertyAccess, string searchTerm)
    {
        ArgumentNullException.ThrowIfNull(propertyAccess);
        ArgumentNullException.ThrowIfNull(searchTerm);

        // Ensure propertyAccess is not null before invoking ToLower() to guard in-memory evaluation:
        // (property != null) AND (property.ToLower().Contains(searchTerm.ToLower()))
        BinaryExpression notNull = Expression.NotEqual(propertyAccess, Expression.Constant(null, typeof(string)));

        MethodCallExpression propToLower = Expression.Call(propertyAccess, ToLowerMethod);
        ConstantExpression termConst = Expression.Constant(searchTerm.Trim().ToLowerInvariant(), typeof(string));
        MethodCallExpression containsCall = Expression.Call(propToLower, ContainsMethod, termConst);

        return Expression.AndAlso(notNull, containsCall);
    }
}

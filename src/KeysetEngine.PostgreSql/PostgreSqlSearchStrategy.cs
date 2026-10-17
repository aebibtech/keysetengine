using System.Linq.Expressions;
using System.Reflection;
using KeysetEngine.Core.Search;
using Microsoft.EntityFrameworkCore;

namespace KeysetEngine.PostgreSql;

/// <summary>
/// PostgreSQL search strategy emitting native ILIKE pattern matching via <c>EF.Functions.ILike</c>.
/// Provides case-insensitive pattern matching that takes advantage of PostgreSQL pg_trgm GIN indexes.
/// </summary>
public sealed class PostgreSqlSearchStrategy : ISearchStrategy
{
    private static readonly MethodInfo ILikeMethod = typeof(NpgsqlDbFunctionsExtensions)
        .GetMethod(nameof(NpgsqlDbFunctionsExtensions.ILike), [typeof(DbFunctions), typeof(string), typeof(string)])
        ?? throw new InvalidOperationException("NpgsqlDbFunctionsExtensions.ILike method could not be located.");

    /// <summary>
    /// Singleton instance of <see cref="PostgreSqlSearchStrategy"/>.
    /// </summary>
    public static PostgreSqlSearchStrategy Instance { get; } = new();

    /// <inheritdoc />
    public Expression BuildPredicate(Expression propertyAccess, string searchTerm)
    {
        ArgumentNullException.ThrowIfNull(propertyAccess);
        ArgumentNullException.ThrowIfNull(searchTerm);

        // EF.Functions
        MemberExpression efFunctions = Expression.Property(null, typeof(EF), nameof(EF.Functions));

        // "%searchTerm%"
        ConstantExpression pattern = Expression.Constant($"%{searchTerm.Trim()}%", typeof(string));

        // EF.Functions.ILike(propertyAccess, "%searchTerm%")
        return Expression.Call(ILikeMethod, efFunctions, propertyAccess, pattern);
    }
}

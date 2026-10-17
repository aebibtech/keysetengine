using System.Linq.Expressions;
using System.Reflection;

namespace KeysetEngine.Core.Expressions;

/// <summary>
/// Provides dynamic LINQ ordering helpers that dynamically emit OrderBy and ThenBy calls
/// with automatic bidirectional order flipping for keyset pagination.
/// </summary>
public static class DynamicOrderHelper
{
    private static readonly MethodInfo OrderByMethod = typeof(Queryable)
        .GetMethods()
        .Single(m => m.Name == nameof(Queryable.OrderBy)
                     && m.GetParameters().Length == 2
                     && m.GetGenericArguments().Length == 2);

    private static readonly MethodInfo OrderByDescendingMethod = typeof(Queryable)
        .GetMethods()
        .Single(m => m.Name == nameof(Queryable.OrderByDescending)
                     && m.GetParameters().Length == 2
                     && m.GetGenericArguments().Length == 2);

    private static readonly MethodInfo ThenByMethod = typeof(Queryable)
        .GetMethods()
        .Single(m => m.Name == nameof(Queryable.ThenBy)
                     && m.GetParameters().Length == 2
                     && m.GetGenericArguments().Length == 2);

    private static readonly MethodInfo ThenByDescendingMethod = typeof(Queryable)
        .GetMethods()
        .Single(m => m.Name == nameof(Queryable.ThenByDescending)
                     && m.GetParameters().Length == 2
                     && m.GetGenericArguments().Length == 2);

    /// <summary>
    /// Applies composite ordering (OrderBy on sort property, ThenBy on tie-breaker property)
    /// to the queryable, automatically inverting SQL ordering when navigating backward.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="source">The source queryable.</param>
    /// <param name="sortSelector">Lambda expression selecting the primary sort property.</param>
    /// <param name="tieBreakerSelector">Lambda expression selecting the unique tie-breaker property.</param>
    /// <param name="isAscending">The configured forward sort direction for the sort property.</param>
    /// <param name="direction">The navigation direction (Forward or Backward).</param>
    /// <returns>An ordered <see cref="IQueryable{T}"/>.</returns>
    public static IQueryable<T> ApplyOrdering<T>(
        IQueryable<T> source,
        LambdaExpression sortSelector,
        LambdaExpression tieBreakerSelector,
        bool isAscending,
        PageDirection direction)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(sortSelector);
        ArgumentNullException.ThrowIfNull(tieBreakerSelector);

        // When navigating Backward, invert the SQL ORDER BY direction
        bool effectiveSortAscending = direction == PageDirection.Forward ? isAscending : !isAscending;
        bool effectiveTieAscending = direction == PageDirection.Forward ? isAscending : !isAscending;

        IQueryable<T> orderedQuery = ApplyOrderCall(source, sortSelector, effectiveSortAscending, isInitialOrder: true);
        return ApplyOrderCall(orderedQuery, tieBreakerSelector, effectiveTieAscending, isInitialOrder: false);
    }

    private static IQueryable<T> ApplyOrderCall<T>(
        IQueryable<T> source,
        LambdaExpression keySelector,
        bool ascending,
        bool isInitialOrder)
    {
        Type entityType = typeof(T);
        Type keyType = keySelector.ReturnType;

        MethodInfo methodDefinition;
        if (isInitialOrder)
        {
            methodDefinition = ascending ? OrderByMethod : OrderByDescendingMethod;
        }
        else
        {
            methodDefinition = ascending ? ThenByMethod : ThenByDescendingMethod;
        }

        MethodInfo genericMethod = methodDefinition.MakeGenericMethod(entityType, keyType);

        MethodCallExpression callExpression = Expression.Call(
            null,
            genericMethod,
            source.Expression,
            Expression.Quote(keySelector));

        return source.Provider.CreateQuery<T>(callExpression);
    }
}

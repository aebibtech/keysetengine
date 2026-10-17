using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using KeysetEngine.Core.Search;

namespace KeysetEngine.Core.Expressions;

/// <summary>
/// Generates universal nested boolean disjunctions for keyset boundary evaluation
/// without requiring SQL row-value / tuple comparison syntax.
/// Compatible across SQL Server, PostgreSQL, SQLite, and in-memory LINQ providers.
/// </summary>
public static class KeysetExpressionBuilder
{
    private static readonly MethodInfo StringCompareMethod = typeof(string)
        .GetMethod(nameof(string.Compare), [typeof(string), typeof(string)])!;

    private static readonly MethodInfo GuidCompareToMethod = typeof(Guid)
        .GetMethod(nameof(Guid.CompareTo), [typeof(Guid)])!;

    /// <summary>
    /// Builds a strongly-typed keyset boundary predicate expression.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <typeparam name="TSort">The sort property type.</typeparam>
    /// <typeparam name="TTie">The tie-breaker property type.</typeparam>
    /// <param name="sortSelector">Lambda expression for the sort property.</param>
    /// <param name="tieBreakerSelector">Lambda expression for the tie-breaker property.</param>
    /// <param name="sortValue">The boundary value of the sort property.</param>
    /// <param name="tieBreakerValue">The boundary value of the tie-breaker property.</param>
    /// <param name="isAscending">True if sorted in ascending order; false if descending.</param>
    /// <param name="direction">The navigation direction (Forward or Backward).</param>
    /// <returns>A binary lambda expression representing the keyset boundary filter.</returns>
    public static Expression<Func<T, bool>> BuildPredicate<T, TSort, TTie>(
        Expression<Func<T, TSort>> sortSelector,
        Expression<Func<T, TTie>> tieBreakerSelector,
        TSort sortValue,
        TTie tieBreakerValue,
        bool isAscending,
        PageDirection direction)
    {
        ArgumentNullException.ThrowIfNull(sortSelector);
        ArgumentNullException.ThrowIfNull(tieBreakerSelector);
        ArgumentNullException.ThrowIfNull(sortValue);
        ArgumentNullException.ThrowIfNull(tieBreakerValue);

        return BuildPredicate<T>(
            sortSelector,
            tieBreakerSelector,
            (object)sortValue,
            (object)tieBreakerValue,
            isAscending,
            direction);
    }

    /// <summary>
    /// Builds a keyset boundary predicate expression using dynamic lambda expressions.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="sortSelector">Lambda expression for the sort property.</param>
    /// <param name="tieBreakerSelector">Lambda expression for the tie-breaker property.</param>
    /// <param name="sortValue">The boundary value of the sort property.</param>
    /// <param name="tieBreakerValue">The boundary value of the tie-breaker property.</param>
    /// <param name="isAscending">True if sorted in ascending order; false if descending.</param>
    /// <param name="direction">The navigation direction (Forward or Backward).</param>
    /// <returns>A binary lambda expression representing the keyset boundary filter.</returns>
    public static Expression<Func<T, bool>> BuildPredicate<T>(
        LambdaExpression sortSelector,
        LambdaExpression tieBreakerSelector,
        object sortValue,
        object tieBreakerValue,
        bool isAscending,
        PageDirection direction)
    {
        ArgumentNullException.ThrowIfNull(sortSelector);
        ArgumentNullException.ThrowIfNull(tieBreakerSelector);
        ArgumentNullException.ThrowIfNull(sortValue);
        ArgumentNullException.ThrowIfNull(tieBreakerValue);

        ParameterExpression parameter = Expression.Parameter(typeof(T), "x");

        // Unify parameter references across both selector bodies
        Expression sortBody = ParameterReplacementVisitor.Replace(sortSelector.Parameters[0], parameter, sortSelector.Body);
        Expression tieBody = ParameterReplacementVisitor.Replace(tieBreakerSelector.Parameters[0], parameter, tieBreakerSelector.Body);

        ConstantExpression sortConst = Expression.Constant(sortValue, sortSelector.ReturnType);
        ConstantExpression tieConst = Expression.Constant(tieBreakerValue, tieBreakerSelector.ReturnType);

        // Determine effective inequality direction:
        // Forward + Ascending  -> GreaterThan (>)
        // Forward + Descending -> LessThan (<)
        // Backward + Ascending  -> LessThan (<)
        // Backward + Descending -> GreaterThan (>)
        bool effectiveGreater = (isAscending && direction == PageDirection.Forward)
                                || (!isAscending && direction == PageDirection.Backward);

        // 1. First inequality: Key_A > val_A (or Key_A < val_A)
        Expression firstComparison = BuildComparison(sortBody, sortConst, effectiveGreater);

        // 2. First equality: Key_A == val_A
        Expression firstEquality = Expression.Equal(sortBody, sortConst);

        // 3. Second inequality: Key_B > val_B (or Key_B < val_B)
        Expression secondComparison = BuildComparison(tieBody, tieConst, effectiveGreater);

        // 4. Conjunction: (Key_A == val_A) AND (Key_B > val_B)
        BinaryExpression andAlso = Expression.AndAlso(firstEquality, secondComparison);

        // 5. Disjunction: (Key_A > val_A) OR ((Key_A == val_A) AND (Key_B > val_B))
        BinaryExpression fullPredicate = Expression.OrElse(firstComparison, andAlso);

        return Expression.Lambda<Func<T, bool>>(fullPredicate, parameter);
    }

    /// <summary>
    /// Builds a keyset boundary predicate expression by deserializing coordinates from a <see cref="KeysetCursorToken"/>.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="sortSelector">Lambda expression for the sort property.</param>
    /// <param name="tieBreakerSelector">Lambda expression for the tie-breaker property.</param>
    /// <param name="token">The cursor token containing JSON-serialized boundary coordinates.</param>
    /// <param name="direction">The navigation direction (Forward or Backward).</param>
    /// <returns>A binary lambda expression representing the keyset boundary filter.</returns>
    public static Expression<Func<T, bool>> BuildPredicate<T>(
        LambdaExpression sortSelector,
        LambdaExpression tieBreakerSelector,
        KeysetCursorToken token,
        PageDirection direction)
    {
        ArgumentNullException.ThrowIfNull(sortSelector);
        ArgumentNullException.ThrowIfNull(tieBreakerSelector);
        ArgumentNullException.ThrowIfNull(token);

        object? sortVal = JsonSerializer.Deserialize(token.SortValueJson, sortSelector.ReturnType);
        object? tieVal = JsonSerializer.Deserialize(token.TieBreakerValueJson, tieBreakerSelector.ReturnType);

        if (sortVal is null || tieVal is null)
        {
            throw new InvalidOperationException("Keyset cursor token contained null coordinates.");
        }

        return BuildPredicate<T>(
            sortSelector,
            tieBreakerSelector,
            sortVal,
            tieVal,
            token.IsAscending,
            direction);
    }

    /// <summary>
    /// Combines multiple property search selectors into a single disjunctive lambda (x.Col1.Contains(...) || x.Col2.Contains(...)).
    /// Returns null if searchTerm is null/whitespace or if no search selectors are provided.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="searchSelectors">Collection of lambda expressions selecting string properties.</param>
    /// <param name="searchTerm">The text string to search for.</param>
    /// <param name="searchStrategy">Optional search strategy (defaults to Universal DefaultSearchStrategy).</param>
    /// <returns>A combined lambda expression, or null.</returns>
    public static Expression<Func<T, bool>>? ApplySearch<T>(
        IReadOnlyList<LambdaExpression>? searchSelectors,
        string? searchTerm,
        ISearchStrategy? searchStrategy = null)
    {
        if (string.IsNullOrWhiteSpace(searchTerm) || searchSelectors is null || searchSelectors.Count == 0)
        {
            return null;
        }

        ISearchStrategy strategy = searchStrategy ?? DefaultSearchStrategy.Instance;
        ParameterExpression parameter = Expression.Parameter(typeof(T), "x");

        Expression? combinedBody = null;

        for (int i = 0; i < searchSelectors.Count; i++)
        {
            LambdaExpression selector = searchSelectors[i];
            Expression propAccess = ParameterReplacementVisitor.Replace(selector.Parameters[0], parameter, selector.Body);
            Expression predicate = strategy.BuildPredicate(propAccess, searchTerm);

            combinedBody = combinedBody is null
                ? predicate
                : Expression.OrElse(combinedBody, predicate);
        }

        return combinedBody is null ? null : Expression.Lambda<Func<T, bool>>(combinedBody, parameter);
    }

    /// <summary>
    /// Combines multiple target properties into a single disjunctive search lambda.
    /// Returns null if searchTerm is null/whitespace or if no target properties are provided.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="searchProperties">Collection of string properties on the entity.</param>
    /// <param name="searchTerm">The text string to search for.</param>
    /// <param name="searchStrategy">Optional search strategy (defaults to Universal DefaultSearchStrategy).</param>
    /// <returns>A combined lambda expression, or null.</returns>
    public static Expression<Func<T, bool>>? ApplySearch<T>(
        IReadOnlyList<PropertyInfo>? searchProperties,
        string? searchTerm,
        ISearchStrategy? searchStrategy = null)
    {
        if (string.IsNullOrWhiteSpace(searchTerm) || searchProperties is null || searchProperties.Count == 0)
        {
            return null;
        }

        ISearchStrategy strategy = searchStrategy ?? DefaultSearchStrategy.Instance;
        ParameterExpression parameter = Expression.Parameter(typeof(T), "x");

        Expression? combinedBody = null;

        for (int i = 0; i < searchProperties.Count; i++)
        {
            MemberExpression propAccess = Expression.Property(parameter, searchProperties[i]);
            Expression predicate = strategy.BuildPredicate(propAccess, searchTerm);

            combinedBody = combinedBody is null
                ? predicate
                : Expression.OrElse(combinedBody, predicate);
        }

        return combinedBody is null ? null : Expression.Lambda<Func<T, bool>>(combinedBody, parameter);
    }


    private static BinaryExpression BuildComparison(Expression left, Expression right, bool greaterThan)
    {
        Type type = left.Type;
        Type underlyingType = Nullable.GetUnderlyingType(type) ?? type;

        if (underlyingType == typeof(string))
        {
            MethodCallExpression compareCall = Expression.Call(StringCompareMethod, left, right);
            ConstantExpression zero = Expression.Constant(0);
            return greaterThan
                ? Expression.GreaterThan(compareCall, zero)
                : Expression.LessThan(compareCall, zero);
        }

        if (underlyingType == typeof(Guid))
        {
            MethodCallExpression compareCall = Expression.Call(left, GuidCompareToMethod, right);
            ConstantExpression zero = Expression.Constant(0);
            return greaterThan
                ? Expression.GreaterThan(compareCall, zero)
                : Expression.LessThan(compareCall, zero);
        }

        return greaterThan
            ? Expression.GreaterThan(left, right)
            : Expression.LessThan(left, right);
    }

    private sealed class ParameterReplacementVisitor : ExpressionVisitor
    {
        private readonly ParameterExpression _source;
        private readonly ParameterExpression _target;

        private ParameterReplacementVisitor(ParameterExpression source, ParameterExpression target)
        {
            _source = source;
            _target = target;
        }

        public static Expression Replace(ParameterExpression source, ParameterExpression target, Expression body)
        {
            return new ParameterReplacementVisitor(source, target).Visit(body);
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            return node == _source ? _target : base.VisitParameter(node);
        }
    }
}

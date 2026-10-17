using System.Linq.Expressions;
using KeysetEngine.Core.Search;

namespace KeysetEngine.Core.Configuration;

/// <summary>
/// Immutable configuration defining keyset pagination boundaries, ordering, search fields, and token scopes.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
public sealed class KeysetConfig<T>
{
    private readonly Func<T, object> _sortGetter;
    private readonly Func<T, object> _tieBreakerGetter;

    /// <summary>
    /// Gets the lambda expression selecting the primary sort property.
    /// </summary>
    public LambdaExpression SortSelector { get; }

    /// <summary>
    /// Gets the lambda expression selecting the unique tie-breaker property (typically primary key).
    /// </summary>
    public LambdaExpression TieBreakerSelector { get; }

    /// <summary>
    /// Gets a value indicating whether the default sort direction is ascending.
    /// </summary>
    public bool IsAscending { get; }

    /// <summary>
    /// Gets the list of lambda expressions selecting string properties to include in universal search.
    /// </summary>
    public IReadOnlyList<LambdaExpression> SearchSelectors { get; }

    /// <summary>
    /// Gets the search strategy responsible for emitting database-compatible text search predicates.
    /// </summary>
    public ISearchStrategy SearchStrategy { get; }

    /// <summary>
    /// Gets the entity purpose string used for cryptographic cursor isolation.
    /// Defaults to the entity type name if not explicitly specified.
    /// </summary>
    public string EntityPurpose { get; }

    /// <summary>
    /// Gets the cached metadata definition, if constructed via builder.
    /// </summary>
    public KeysetDefinition? Definition { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="KeysetConfig{T}"/>.
    /// </summary>
    public KeysetConfig(
        LambdaExpression sortSelector,
        LambdaExpression tieBreakerSelector,
        bool isAscending = true,
        IEnumerable<LambdaExpression>? searchSelectors = null,
        ISearchStrategy? searchStrategy = null,
        string? entityPurpose = null,
        KeysetDefinition? definition = null)
    {
        ArgumentNullException.ThrowIfNull(sortSelector);
        ArgumentNullException.ThrowIfNull(tieBreakerSelector);

        SortSelector = sortSelector;
        TieBreakerSelector = tieBreakerSelector;
        IsAscending = isAscending;
        SearchSelectors = searchSelectors?.ToList().AsReadOnly() ?? (IReadOnlyList<LambdaExpression>)Array.Empty<LambdaExpression>();
        SearchStrategy = searchStrategy ?? DefaultSearchStrategy.Instance;
        EntityPurpose = string.IsNullOrWhiteSpace(entityPurpose) ? typeof(T).Name : entityPurpose;
        Definition = definition;

        _sortGetter = CompileGetter(sortSelector);
        _tieBreakerGetter = CompileGetter(tieBreakerSelector);
    }

    /// <summary>
    /// Extracts the sort property value from an entity instance in O(1) time without reflection overhead.
    /// </summary>
    public object GetSortValue(T entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return _sortGetter(entity);
    }

    /// <summary>
    /// Extracts the tie-breaker property value from an entity instance in O(1) time without reflection overhead.
    /// </summary>
    public object GetTieBreakerValue(T entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return _tieBreakerGetter(entity);
    }

    private static Func<T, object> CompileGetter(LambdaExpression selector)
    {
        ParameterExpression parameter = Expression.Parameter(typeof(T), "x");
        Expression body = selector.Body;

        if (selector.Parameters.Count > 0 && selector.Parameters[0] != parameter)
        {
            body = new ParameterReplacer(selector.Parameters[0], parameter).Visit(body);
        }

        UnaryExpression boxed = Expression.Convert(body, typeof(object));
        return Expression.Lambda<Func<T, object>>(boxed, parameter).Compile();
    }

    private sealed class ParameterReplacer : ExpressionVisitor
    {
        private readonly ParameterExpression _source;
        private readonly ParameterExpression _target;

        public ParameterReplacer(ParameterExpression source, ParameterExpression target)
        {
            _source = source;
            _target = target;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            return node == _source ? _target : base.VisitParameter(node);
        }
    }
}

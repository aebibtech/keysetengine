using System.Linq.Expressions;
using System.Reflection;
using KeysetEngine.Core.Search;

namespace KeysetEngine.Core.Configuration;

/// <summary>
/// Fluent builder for constructing and validating a <see cref="KeysetConfig{T}"/>.
/// Enforces non-nullable sort and tie-breaker invariants at configuration time.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
public sealed class KeysetConfigBuilder<T>
{
    private LambdaExpression? _sortSelector;
    private PropertyInfo? _sortProperty;
    private bool _isAscending = true;

    private LambdaExpression? _tieBreakerSelector;
    private PropertyInfo? _tieBreakerProperty;

    private readonly List<LambdaExpression> _searchSelectors = [];
    private readonly List<PropertyInfo> _searchProperties = [];

    private ISearchStrategy? _searchStrategy;
    private string? _entityPurpose;

    /// <summary>
    /// Configures the primary sort property and direction.
    /// The sort property must be non-nullable.
    /// </summary>
    /// <typeparam name="TSort">The sort property type.</typeparam>
    /// <param name="sortSelector">Lambda expression selecting the property to sort by.</param>
    /// <param name="ascending">True for ascending order (default); false for descending.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public KeysetConfigBuilder<T> SortBy<TSort>(Expression<Func<T, TSort>> sortSelector, bool ascending = true)
    {
        ArgumentNullException.ThrowIfNull(sortSelector);

        PropertyInfo prop = ExtractPropertyInfo(sortSelector, nameof(sortSelector));
        ValidateNonNullable(prop, "Sort");

        _sortSelector = sortSelector;
        _sortProperty = prop;
        _isAscending = ascending;
        return this;
    }

    /// <summary>
    /// Configures the unique tie-breaker property (typically primary key).
    /// The tie-breaker property must be non-nullable and unique.
    /// </summary>
    /// <typeparam name="TTie">The tie-breaker property type.</typeparam>
    /// <param name="tieBreakerSelector">Lambda expression selecting the unique tie-breaker property.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public KeysetConfigBuilder<T> TieBreakBy<TTie>(Expression<Func<T, TTie>> tieBreakerSelector)
    {
        ArgumentNullException.ThrowIfNull(tieBreakerSelector);

        PropertyInfo prop = ExtractPropertyInfo(tieBreakerSelector, nameof(tieBreakerSelector));
        ValidateNonNullable(prop, "Tie-breaker");

        _tieBreakerSelector = tieBreakerSelector;
        _tieBreakerProperty = prop;
        return this;
    }

    /// <summary>
    /// Adds one or more string properties to be evaluated during full-text search.
    /// </summary>
    /// <param name="searchSelectors">Lambda expressions selecting string properties.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public KeysetConfigBuilder<T> SearchIn(params Expression<Func<T, string?>>[] searchSelectors)
    {
        ArgumentNullException.ThrowIfNull(searchSelectors);

        foreach (var selector in searchSelectors)
        {
            if (selector is null) continue;
            PropertyInfo prop = ExtractPropertyInfo(selector, nameof(searchSelectors));
            _searchSelectors.Add(selector);
            _searchProperties.Add(prop);
        }

        return this;
    }

    /// <summary>
    /// Overrides the default text search strategy.
    /// </summary>
    /// <param name="searchStrategy">The search strategy implementation.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public KeysetConfigBuilder<T> UseSearchStrategy(ISearchStrategy searchStrategy)
    {
        ArgumentNullException.ThrowIfNull(searchStrategy);
        _searchStrategy = searchStrategy;
        return this;
    }

    /// <summary>
    /// Sets an explicit entity purpose string used for cryptographic cursor token isolation.
    /// </summary>
    /// <param name="entityPurpose">The purpose string.</param>
    /// <returns>The builder instance for fluent chaining.</returns>
    public KeysetConfigBuilder<T> WithEntityPurpose(string entityPurpose)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityPurpose);
        _entityPurpose = entityPurpose;
        return this;
    }

    /// <summary>
    /// Validates configuration invariants, creates the <see cref="KeysetDefinition"/> and <see cref="KeysetConfig{T}"/>,
    /// and caches the definition in the global registry.
    /// </summary>
    /// <returns>The immutable <see cref="KeysetConfig{T}"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown if SortBy or TieBreakBy were not configured.</exception>
    public KeysetConfig<T> Build()
    {
        if (_sortSelector is null || _sortProperty is null)
        {
            throw new InvalidOperationException($"Keyset configuration for '{typeof(T).Name}' must specify a Sort property via SortBy().");
        }

        if (_tieBreakerSelector is null || _tieBreakerProperty is null)
        {
            throw new InvalidOperationException($"Keyset configuration for '{typeof(T).Name}' must specify a Tie-breaker property via TieBreakBy().");
        }

        string purpose = _entityPurpose ?? typeof(T).Name;
        ISearchStrategy strategy = _searchStrategy ?? DefaultSearchStrategy.Instance;

        var definition = new KeysetDefinition(
            entityType: typeof(T),
            sortProperty: _sortProperty,
            tieBreakerProperty: _tieBreakerProperty,
            isAscending: _isAscending,
            searchProperties: _searchProperties,
            searchStrategy: strategy,
            entityPurpose: purpose);

        var config = new KeysetConfig<T>(
            sortSelector: _sortSelector,
            tieBreakerSelector: _tieBreakerSelector,
            isAscending: _isAscending,
            searchSelectors: _searchSelectors,
            searchStrategy: strategy,
            entityPurpose: purpose,
            definition: definition);

        KeysetConfigRegistry.Register(config);

        return config;
    }

    private static PropertyInfo ExtractPropertyInfo<TProp>(Expression<Func<T, TProp>> selector, string paramName)
    {
        Expression body = selector.Body;
        if (body is UnaryExpression { NodeType: ExpressionType.Convert } unary)
        {
            body = unary.Operand;
        }

        if (body is not MemberExpression { Member: PropertyInfo propInfo })
        {
            throw new ArgumentException($"Expression '{selector}' must be a member property access expression on '{typeof(T).Name}'.", paramName);
        }

        return propInfo;
    }

    private static void ValidateNonNullable(PropertyInfo property, string role)
    {
        Type propType = property.PropertyType;

        // Check Nullable<T> value types (e.g., int?, DateTime?)
        if (propType.IsValueType && Nullable.GetUnderlyingType(propType) is not null)
        {
            throw new InvalidOperationException($"{role} property '{property.Name}' on type '{property.DeclaringType?.Name}' must be non-nullable.");
        }

        // Check reference type nullability annotations if available
        var nullabilityContext = new NullabilityInfoContext();
        var nullabilityInfo = nullabilityContext.Create(property);
        if (nullabilityInfo.ReadState == NullabilityState.Nullable)
        {
            throw new InvalidOperationException($"{role} property '{property.Name}' on type '{property.DeclaringType?.Name}' must be non-nullable.");
        }
    }
}

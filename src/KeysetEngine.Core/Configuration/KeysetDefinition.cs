using System.Reflection;
using KeysetEngine.Core.Search;

namespace KeysetEngine.Core.Configuration;

/// <summary>
/// Cached reflection and metadata definition for an entity's keyset configuration.
/// Stored in thread-safe static cache to avoid repeated reflection overhead across application lifetime.
/// </summary>
public sealed class KeysetDefinition
{
    /// <summary>
    /// Gets the entity CLR type.
    /// </summary>
    public Type EntityType { get; }

    /// <summary>
    /// Gets the reflected <see cref="PropertyInfo"/> for the primary sort property.
    /// </summary>
    public PropertyInfo SortProperty { get; }

    /// <summary>
    /// Gets the reflected <see cref="PropertyInfo"/> for the tie-breaker property.
    /// </summary>
    public PropertyInfo TieBreakerProperty { get; }

    /// <summary>
    /// Gets a value indicating whether the default sort direction is ascending.
    /// </summary>
    public bool IsAscending { get; }

    /// <summary>
    /// Gets the collection of reflected <see cref="PropertyInfo"/> instances used for full-text search.
    /// </summary>
    public IReadOnlyList<PropertyInfo> SearchProperties { get; }

    /// <summary>
    /// Gets the search strategy instance.
    /// </summary>
    public ISearchStrategy SearchStrategy { get; }

    /// <summary>
    /// Gets the entity purpose string used for cryptographic cursor isolation.
    /// </summary>
    public string EntityPurpose { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="KeysetDefinition"/>.
    /// </summary>
    public KeysetDefinition(
        Type entityType,
        PropertyInfo sortProperty,
        PropertyInfo tieBreakerProperty,
        bool isAscending,
        IEnumerable<PropertyInfo>? searchProperties,
        ISearchStrategy searchStrategy,
        string entityPurpose)
    {
        ArgumentNullException.ThrowIfNull(entityType);
        ArgumentNullException.ThrowIfNull(sortProperty);
        ArgumentNullException.ThrowIfNull(tieBreakerProperty);
        ArgumentNullException.ThrowIfNull(searchStrategy);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityPurpose);

        EntityType = entityType;
        SortProperty = sortProperty;
        TieBreakerProperty = tieBreakerProperty;
        IsAscending = isAscending;
        SearchProperties = searchProperties?.ToList().AsReadOnly() ?? (IReadOnlyList<PropertyInfo>)Array.Empty<PropertyInfo>();
        SearchStrategy = searchStrategy;
        EntityPurpose = entityPurpose;
    }
}

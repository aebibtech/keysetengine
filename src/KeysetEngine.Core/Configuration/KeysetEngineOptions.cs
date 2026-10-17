using KeysetEngine.Core.Search;

namespace KeysetEngine.Core.Configuration;

/// <summary>
/// Options for configuring KeysetEngine pagination, search strategies, and entity mappings.
/// </summary>
public sealed class KeysetEngineOptions
{
    private readonly List<Action> _registrationActions = [];

    /// <summary>
    /// Gets or sets the default search strategy used when an entity does not explicitly specify one.
    /// </summary>
    public ISearchStrategy DefaultSearchStrategy { get; set; } = Search.DefaultSearchStrategy.Instance;

    /// <summary>
    /// Configures keyset pagination boundaries and search fields for an entity type.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="configure">The builder configuration action.</param>
    /// <returns>The options instance for fluent chaining.</returns>
    public KeysetEngineOptions Configure<T>(Action<KeysetConfigBuilder<T>> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        _registrationActions.Add(() =>
        {
            var builder = new KeysetConfigBuilder<T>();
            builder.UseSearchStrategy(DefaultSearchStrategy);
            configure(builder);
            builder.Build();
        });

        return this;
    }

    internal void ApplyRegistrations()
    {
        foreach (Action action in _registrationActions)
        {
            action();
        }
    }
}

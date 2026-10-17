using System.Collections.Concurrent;

namespace KeysetEngine.Core.Configuration;

/// <summary>
/// Static thread-safe registry caching compiled keyset configurations and metadata definitions.
/// Eliminates runtime reflection and expression compilation costs after first registration.
/// </summary>
public static class KeysetConfigRegistry
{
    private static readonly ConcurrentDictionary<Type, object> ConfigCache = new();
    private static readonly ConcurrentDictionary<Type, KeysetDefinition> DefinitionCache = new();

    /// <summary>
    /// Registers a strongly-typed <see cref="KeysetConfig{T}"/> and its <see cref="KeysetDefinition"/> into the cache.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="config">The keyset configuration.</param>
    public static void Register<T>(KeysetConfig<T> config)
    {
        ArgumentNullException.ThrowIfNull(config);

        ConfigCache[typeof(T)] = config;

        if (config.Definition is not null)
        {
            DefinitionCache[typeof(T)] = config.Definition;
        }
    }

    /// <summary>
    /// Retrieves the registered <see cref="KeysetConfig{T}"/> for the entity type, or null if not registered.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <returns>The registered <see cref="KeysetConfig{T}"/>, or null.</returns>
    public static KeysetConfig<T>? Get<T>()
    {
        if (ConfigCache.TryGetValue(typeof(T), out object? configObj) && configObj is KeysetConfig<T> typed)
        {
            return typed;
        }

        return null;
    }

    /// <summary>
    /// Retrieves or creates a <see cref="KeysetConfig{T}"/> using a configuration delegate.
    /// </summary>
    /// <typeparam name="T">The entity type.</typeparam>
    /// <param name="configure">The builder action.</param>
    /// <returns>The cached or newly created <see cref="KeysetConfig{T}"/>.</returns>
    public static KeysetConfig<T> GetOrCreate<T>(Func<KeysetConfigBuilder<T>, KeysetConfig<T>> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var existing = Get<T>();
        if (existing is not null)
        {
            return existing;
        }

        var builder = new KeysetConfigBuilder<T>();
        return configure(builder);
    }

    /// <summary>
    /// Retrieves the cached <see cref="KeysetDefinition"/> metadata for an entity type.
    /// </summary>
    /// <param name="entityType">The entity CLR type.</param>
    /// <returns>The cached <see cref="KeysetDefinition"/>, or null.</returns>
    public static KeysetDefinition? GetDefinition(Type entityType)
    {
        ArgumentNullException.ThrowIfNull(entityType);

        DefinitionCache.TryGetValue(entityType, out KeysetDefinition? definition);
        return definition;
    }

    /// <summary>
    /// Clears all cached configurations and definitions.
    /// </summary>
    public static void Clear()
    {
        ConfigCache.Clear();
        DefinitionCache.Clear();
    }
}

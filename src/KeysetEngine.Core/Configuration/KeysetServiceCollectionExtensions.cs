using Microsoft.Extensions.DependencyInjection;

namespace KeysetEngine.Core.Configuration;

/// <summary>
/// Service collection extensions for registering KeysetEngine pagination services and entity configurations.
/// </summary>
public static class KeysetServiceCollectionExtensions
{
    /// <summary>
    /// Adds KeysetEngine pagination configurations and registers entities in the configuration cache.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional options builder to configure entity mappings.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddKeysetPagination(
        this IServiceCollection services,
        Action<KeysetEngineOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new KeysetEngineOptions();
        configure?.Invoke(options);
        options.ApplyRegistrations();

        return services;
    }
}

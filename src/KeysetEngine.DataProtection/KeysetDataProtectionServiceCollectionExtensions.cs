using KeysetEngine.Core;
using KeysetEngine.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace KeysetEngine.DataProtection;

/// <summary>
/// Extension methods for setting up KeysetEngine Data Protection in an <see cref="IServiceCollection"/>.
/// </summary>
public static class KeysetDataProtectionServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="DataProtectionCursorProtector"/> as the <see cref="ICursorProtector"/> implementation.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddKeysetDataProtection(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<ICursorProtector, DataProtectionCursorProtector>();
        return services;
    }
}

using KeysetEngine.Core.Configuration;

namespace KeysetEngine.PostgreSql;

/// <summary>
/// Extension methods for configuring PostgreSQL-native search strategies in KeysetEngine.
/// </summary>
public static class KeysetEnginePostgreSqlExtensions
{
    /// <summary>
    /// Configures KeysetEngine to use <see cref="PostgreSqlSearchStrategy"/> (EF.Functions.ILike) as the default search strategy.
    /// </summary>
    /// <param name="options">The KeysetEngine options.</param>
    /// <returns>The options instance for fluent chaining.</returns>
    public static KeysetEngineOptions UsePostgreSqlSearch(this KeysetEngineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.DefaultSearchStrategy = PostgreSqlSearchStrategy.Instance;
        return options;
    }

    /// <summary>
    /// Configures KeysetEngine to use <see cref="PostgreSqlTsVectorSearchStrategy"/> (tsvector / tsquery) as the default search strategy.
    /// </summary>
    /// <param name="options">The KeysetEngine options.</param>
    /// <param name="regConfig">The PostgreSQL text search configuration language (default: "english").</param>
    /// <returns>The options instance for fluent chaining.</returns>
    public static KeysetEngineOptions UsePostgreSqlTsVectorSearch(this KeysetEngineOptions options, string regConfig = "english")
    {
        ArgumentNullException.ThrowIfNull(options);
        options.DefaultSearchStrategy = new PostgreSqlTsVectorSearchStrategy(regConfig);
        return options;
    }
}

using KeysetEngine.Core.Configuration;

namespace KeysetEngine.SqlServer;

/// <summary>
/// Extension methods for configuring SQL Server-native search strategies in KeysetEngine.
/// </summary>
public static class KeysetEngineSqlServerExtensions
{
    /// <summary>
    /// Configures KeysetEngine to use <see cref="SqlServerFullTextSearchStrategy"/> (FREETEXT or CONTAINS) as the default search strategy.
    /// </summary>
    /// <param name="options">The KeysetEngine options.</param>
    /// <param name="useContains">If true, uses CONTAINS(); if false (default), uses FREETEXT().</param>
    /// <returns>The options instance for fluent chaining.</returns>
    public static KeysetEngineOptions UseSqlServerFullTextSearch(this KeysetEngineOptions options, bool useContains = false)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.DefaultSearchStrategy = new SqlServerFullTextSearchStrategy(useContains);
        return options;
    }

    /// <summary>
    /// Configures KeysetEngine to use <see cref="SqlServerCollationSearchStrategy"/> (case-insensitive collation LIKE) as the default search strategy.
    /// </summary>
    /// <param name="options">The KeysetEngine options.</param>
    /// <returns>The options instance for fluent chaining.</returns>
    public static KeysetEngineOptions UseSqlServerCollationSearch(this KeysetEngineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.DefaultSearchStrategy = SqlServerCollationSearchStrategy.Instance;
        return options;
    }
}

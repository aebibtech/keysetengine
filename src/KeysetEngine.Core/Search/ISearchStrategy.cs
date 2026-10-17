using System.Linq.Expressions;

namespace KeysetEngine.Core.Search;

/// <summary>
/// Strategy contract for generating dynamic text search predicates on entity properties.
/// </summary>
public interface ISearchStrategy
{
    /// <summary>
    /// Builds a boolean expression evaluating whether the specified property access matches the search term.
    /// </summary>
    /// <param name="propertyAccess">The expression representing member access to the target text property.</param>
    /// <param name="searchTerm">The search term to match against.</param>
    /// <returns>A boolean expression representing the search predicate.</returns>
    Expression BuildPredicate(Expression propertyAccess, string searchTerm);
}

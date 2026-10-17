using System.Linq.Expressions;
using System.Reflection;
using KeysetEngine.Core.Search;
using Microsoft.EntityFrameworkCore;

namespace KeysetEngine.PostgreSql;

/// <summary>
/// PostgreSQL full-text search strategy utilizing tsvector and tsquery for lexical search
/// against PostgreSQL GIN tsvector indexes.
/// </summary>
public sealed class PostgreSqlTsVectorSearchStrategy : ISearchStrategy
{
    private static readonly MethodInfo ToTsVectorMethod = Helper.FindMethod("ToTsVector")
        ?? throw new InvalidOperationException("Npgsql ToTsVector method could not be located.");

    private static readonly MethodInfo PlainToTsQueryMethod = Helper.FindMethod("PlainToTsQuery")
        ?? throw new InvalidOperationException("Npgsql PlainToTsQuery method could not be located.");

    private static readonly MethodInfo MatchesMethod = Helper.FindMethod("Matches")
        ?? throw new InvalidOperationException("Npgsql Matches method could not be located.");

    private readonly string _regConfig;

    /// <summary>
    /// Initializes a new instance of <see cref="PostgreSqlTsVectorSearchStrategy"/>.
    /// </summary>
    /// <param name="regConfig">The PostgreSQL text search configuration language (defaults to "english").</param>
    public PostgreSqlTsVectorSearchStrategy(string regConfig = "english")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(regConfig);
        _regConfig = regConfig;
    }

    /// <inheritdoc />
    public Expression BuildPredicate(Expression propertyAccess, string searchTerm)
    {
        ArgumentNullException.ThrowIfNull(propertyAccess);
        ArgumentNullException.ThrowIfNull(searchTerm);

        MemberExpression efFunctions = Expression.Property(null, typeof(EF), nameof(EF.Functions));
        ConstantExpression configConst = Expression.Constant(_regConfig, typeof(string));
        ConstantExpression queryConst = Expression.Constant(searchTerm.Trim(), typeof(string));

        // EF.Functions.ToTsVector(regconfig?, property)
        MethodCallExpression toTsVectorCall = ToTsVectorMethod.GetParameters().Length == 3
            ? Expression.Call(ToTsVectorMethod, efFunctions, configConst, propertyAccess)
            : Expression.Call(ToTsVectorMethod, efFunctions, propertyAccess);

        // EF.Functions.PlainToTsQuery(regconfig?, searchTerm)
        MethodCallExpression plainToTsQueryCall = PlainToTsQueryMethod.GetParameters().Length == 3
            ? Expression.Call(PlainToTsQueryMethod, efFunctions, configConst, queryConst)
            : Expression.Call(PlainToTsQueryMethod, efFunctions, queryConst);

        Expression queryArg = MatchesMethod.GetParameters()[1].ParameterType == typeof(string)
            ? queryConst
            : plainToTsQueryCall;

        // Matches(vector, query)
        return MatchesMethod.IsStatic
            ? Expression.Call(MatchesMethod, toTsVectorCall, queryArg)
            : Expression.Call(toTsVectorCall, MatchesMethod, queryArg);
    }
}

using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace KeysetEngine.PostgreSql;

internal static class Helper
{
    public static MethodInfo? FindMethod(string name)
    {
        return typeof(NpgsqlDbFunctionsExtensions).Assembly.GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .FirstOrDefault(m => m.Name == name);
    }
}

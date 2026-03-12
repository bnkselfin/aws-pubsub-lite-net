namespace AwsPubSubLite.Utils;

internal static class VariableUtil
{
    internal static string Resolve(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        if (value.StartsWith("${") && value.EndsWith("}"))
        {
            var varName = value[2..^1];
            return Environment.GetEnvironmentVariable(varName) ?? value;
        }

        return value;
    }

    internal static string? ResolveNullable(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        return Resolve(value);
    }
}

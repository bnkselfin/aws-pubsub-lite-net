namespace AwsPubSubLite.Errors;

public enum ResourceNameErrorKind
{
    Empty,
    TooLong,
    InvalidPattern
}

public sealed class ResourceNameException : PubSubException
{
    public ResourceNameErrorKind NameErrorKind { get; }

    private ResourceNameException(ResourceNameErrorKind kind, string message) : base(message)
    {
        NameErrorKind = kind;
    }

    public ResourceNameException(string message) : base(message)
    {
        NameErrorKind = ResourceNameErrorKind.InvalidPattern;
    }

    public static ResourceNameException Empty() =>
        new(ResourceNameErrorKind.Empty, "Resource name is empty");

    public static ResourceNameException TooLong(string name, int max) =>
        new(ResourceNameErrorKind.TooLong, $"Resource name '{name}' exceeds {max} characters");

    public static ResourceNameException InvalidPattern(string name) =>
        new(ResourceNameErrorKind.InvalidPattern, $"Resource name '{name}' contains invalid characters (must match [a-zA-Z0-9_-]+)");
}

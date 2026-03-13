namespace AwsPubSubLite.Errors;

public sealed class ResourceException : PubSubException
{
    public string ResourceName { get; }
    public string ResourceType { get; }

    public ResourceException(string resourceName, string resourceType, string message)
        : base(PubSubErrorKind.Resource, message)
    {
        ResourceName = resourceName;
        ResourceType = resourceType;
    }

    public ResourceException(string resourceName, string resourceType, string message, Exception inner)
        : base(PubSubErrorKind.Resource, message, inner)
    {
        ResourceName = resourceName;
        ResourceType = resourceType;
    }
}

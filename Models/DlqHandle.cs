namespace AwsPubSubLite.Models;

public sealed record DlqHandle
{
    public ResourceName Name { get; }
    public string Url { get; }
    public string Arn { get; }

    internal DlqHandle(ResourceName name, string url, string arn)
    {
        name.ValidateResourceType(ResourceType.Queue);
        Name = name;
        Url = url;
        Arn = arn;
    }
}

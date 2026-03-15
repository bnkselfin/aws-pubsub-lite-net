namespace AwsPubSubLite.Models;

public sealed record TopicResource
{
    public ResourceName Name { get; }
    public string Arn { get; }

    internal TopicResource(ResourceName name, string arn)
    {
        name.ValidateResourceType(ResourceType.Topic);
        Name = name;
        Arn = arn;
    }
}

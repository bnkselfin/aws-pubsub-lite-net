namespace AwsPubSubLite.Models;

public sealed class QueueResource
{
    public ResourceName Name { get; }
    public string Arn { get; }
    public string Url { get; }
    public string SnsArn { get; }

    private int _subscribed;

    internal QueueResource(ResourceName name, string arn, string url, string snsArn)
    {
        name.ValidateResourceType(ResourceType.Queue);
        Name = name;
        Arn = arn;
        Url = url;
        SnsArn = snsArn;
    }

    public bool IsSubscribed => Interlocked.CompareExchange(ref _subscribed, 0, 0) == 1;

    internal bool TrySubscribe() =>
        Interlocked.CompareExchange(ref _subscribed, 1, 0) == 0;

    internal void Unsubscribe() =>
        Interlocked.Exchange(ref _subscribed, 0);
}

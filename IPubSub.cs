using Amazon.SimpleNotificationService.Model;
using AwsPubSubLite.Models;

namespace AwsPubSubLite;

public interface IPubSub : IDisposable, IAsyncDisposable
{
    IReadOnlyDictionary<string, TopicResource> Topics { get; }
    IReadOnlyDictionary<string, QueueResource> Queues { get; }

    Task<TopicResource> AddTopicAsync(ResourceName name, CancellationToken ct = default);
    Task<QueueResource> AddQueueAsync(ResourceName name, string snsArn, CancellationToken ct = default);

    Task<PublishResponse> PublishAsync(string topicName, string message, CancellationToken ct = default);
    Task<PublishResponse> PublishAsync<T>(string topicName, T message, CancellationToken ct = default);

    Task SubscribeAsync(
        string queueName,
        IReadOnlyList<IMessageHandler> handlers,
        HandlerExecutionMode executionMode,
        MessageDeleteMode deleteMode,
        CancellationToken ct = default);
}

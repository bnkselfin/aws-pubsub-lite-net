using Amazon.SimpleNotificationService.Model;
using AwsPubSubLite.Models;

namespace AwsPubSubLite;

public interface IPubSub : IDisposable, IAsyncDisposable
{
    IReadOnlyDictionary<string, TopicResource> Topics { get; }
    IReadOnlyDictionary<string, QueueResource> Queues { get; }
    IReadOnlyDictionary<string, DlqHandle> Dlqs { get; }

    Task<TopicResource> AddTopicAsync(ResourceName name, CancellationToken ct = default);
    Task<QueueResource> AddQueueAsync(ResourceName name, string snsArn, QueueDlq? dlq = null, CancellationToken ct = default);
    Task<DlqHandle> AddDlqAsync(ResourceName name, CancellationToken ct = default);

    Task<PublishResponse> PublishAsync(string topicName, string message, CancellationToken ct = default);
    Task<PublishResponse> PublishAsync<T>(string topicName, T message, CancellationToken ct = default);

    Task SubscribeAsync(
        string queueName,
        IReadOnlyList<IMessageHandler> handlers,
        HandlerExecutionMode executionMode,
        MessageDeleteMode deleteMode,
        CancellationToken ct = default);

    Task<IReadOnlyList<IncomingMessage>> PeekDlqAsync(
        string dlqUrl, int maxMessages, int visibilityTimeoutSecs,
        CancellationToken ct = default);

    Task<int> DrainDlqAsync(
        string dlqUrl, string targetUrl, int maxMessages,
        CancellationToken ct = default);

    Task DeleteDlqMessageAsync(
        string dlqUrl, string receiptHandle,
        CancellationToken ct = default);
}

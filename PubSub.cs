using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS;
using AwsPubSubLite.Internal;
using AwsPubSubLite.Models;
using AwsPubSubLite.Settings;
using AwsPubSubLite.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AwsPubSubLite;

public sealed class PubSub : IPubSub
{
    private readonly IAmazonSQS _sqsClient;
    private readonly IAmazonSimpleNotificationService _snsClient;
    private readonly ResourceRegistry _registry = new();
    private readonly TopicManager _topicManager;
    private readonly QueueManager _queueManager;
    private readonly DlqManager _dlqManager;
    private readonly MessagePublisher _publisher;
    private readonly MessageSubscriber _subscriber;

    public IReadOnlyDictionary<string, TopicResource> Topics => _registry.Topics;
    public IReadOnlyDictionary<string, QueueResource> Queues => _registry.Queues;
    public IReadOnlyDictionary<string, DlqHandle> Dlqs => _registry.Dlqs;

    public PubSub(IOptions<PubSubSettings> options, ILogger<PubSub> logger)
        : this(ResolveAwsSettings(options.Value), logger, null, null)
    {
    }

    internal PubSub(
        PubSubSettings settings,
        ILogger<PubSub> logger,
        IAmazonSQS? sqsClient,
        IAmazonSimpleNotificationService? snsClient)
    {
        _sqsClient = sqsClient ?? AwsClientFactory.CreateSqsClient(settings.Aws);
        _snsClient = snsClient ?? AwsClientFactory.CreateSnsClient(settings.Aws);

        _topicManager = new TopicManager(_snsClient, _registry, settings.Topic, logger);
        _queueManager = new QueueManager(_sqsClient, _registry, settings.Queue, logger);
        _dlqManager = new DlqManager(_sqsClient, _registry, settings.Queue, logger);
        _publisher = new MessagePublisher(_snsClient, _registry, settings.JsonOptions, logger);
        _subscriber = new MessageSubscriber(_sqsClient, _snsClient, _registry, settings.Queue, logger);

        logger.LogInformation("PubSub initialized");
    }

    public Task<TopicResource> AddTopicAsync(ResourceName name, CancellationToken ct = default)
        => _topicManager.AddTopicAsync(name, ct);

    public Task<QueueResource> AddQueueAsync(
        ResourceName name, string snsArn, QueueDlq? dlq = null, CancellationToken ct = default)
        => _queueManager.AddQueueAsync(name, snsArn, dlq, ct);

    public Task<DlqHandle> AddDlqAsync(ResourceName name, CancellationToken ct = default)
        => _dlqManager.AddDlqAsync(name, ct);

    public Task<PublishResponse> PublishAsync(string topicName, string message, CancellationToken ct = default)
        => _publisher.PublishAsync(topicName, message, ct);

    public Task<PublishResponse> PublishAsync<T>(string topicName, T message, CancellationToken ct = default)
        => _publisher.PublishAsync(topicName, message, ct);

    public Task SubscribeAsync(
        string queueName,
        IReadOnlyList<IMessageHandler> handlers,
        HandlerExecutionMode executionMode,
        MessageDeleteMode deleteMode,
        CancellationToken ct = default)
        => _subscriber.SubscribeAsync(queueName, handlers, executionMode, deleteMode, ct);

    public Task<IReadOnlyList<IncomingMessage>> PeekDlqAsync(
        string dlqUrl, int maxMessages, int visibilityTimeoutSecs, CancellationToken ct = default)
        => _dlqManager.PeekDlqAsync(dlqUrl, maxMessages, visibilityTimeoutSecs, ct);

    public Task<int> DrainDlqAsync(
        string dlqUrl, string targetUrl, int maxMessages, CancellationToken ct = default)
        => _dlqManager.DrainDlqAsync(dlqUrl, targetUrl, maxMessages, ct);

    public Task DeleteDlqMessageAsync(string dlqUrl, string receiptHandle, CancellationToken ct = default)
        => _dlqManager.DeleteDlqMessageAsync(dlqUrl, receiptHandle, ct);

    public void Dispose()
    {
        _sqsClient.Dispose();
        _snsClient.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_sqsClient is IAsyncDisposable sqsAsync)
            await sqsAsync.DisposeAsync();
        else
            _sqsClient.Dispose();

        if (_snsClient is IAsyncDisposable snsAsync)
            await snsAsync.DisposeAsync();
        else
            _snsClient.Dispose();
    }

    private static PubSubSettings ResolveAwsSettings(PubSubSettings settings)
    {
        var aws = settings.Aws;
        aws.Region = VariableUtil.Resolve(aws.Region);
        aws.AccessKey = VariableUtil.ResolveNullable(aws.AccessKey);
        aws.SecretKey = VariableUtil.ResolveNullable(aws.SecretKey);
        aws.ServiceUrl = VariableUtil.ResolveNullable(aws.ServiceUrl);
        return settings;
    }
}

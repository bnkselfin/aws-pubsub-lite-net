using System.Text.Json;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using AwsPubSubLite.Errors;
using Microsoft.Extensions.Logging;

namespace AwsPubSubLite.Internal;

internal sealed class MessagePublisher
{
    private readonly IAmazonSimpleNotificationService _snsClient;
    private readonly ResourceRegistry _registry;
    private readonly JsonSerializerOptions? _jsonOptions;
    private readonly ILogger _logger;

    internal MessagePublisher(
        IAmazonSimpleNotificationService snsClient,
        ResourceRegistry registry,
        JsonSerializerOptions? jsonOptions,
        ILogger logger)
    {
        _snsClient = snsClient;
        _registry = registry;
        _jsonOptions = jsonOptions;
        _logger = logger;
    }

    internal Task<PublishResponse> PublishAsync<T>(
        string topicName, T message, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(message, _jsonOptions);
        return PublishAsync(topicName, json, ct);
    }

    internal async Task<PublishResponse> PublishAsync(
        string topicName, string message, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(message))
            throw PubSubException.EmptyMessage();

        if (!_registry.TryGetTopic(topicName, out var topic))
            throw PubSubException.TopicNotExists(topicName);

        PublishResponse response;
        try
        {
            response = await _snsClient.PublishAsync(new PublishRequest
            {
                TopicArn = topic.Arn,
                Message = message
            }, ct);
        }
        catch (Exception ex) when (ex is not PubSubException && ex is not OperationCanceledException)
        {
            throw PubSubException.PublishMessage(topicName, message.Length, ex);
        }

        _logger.LogInformation(
            "Published message (len={MessageLength}) to topic '{TopicName}': {MessageId}",
            message.Length, topicName, response.MessageId);

        return response;
    }
}

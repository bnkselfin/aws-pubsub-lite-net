using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using AwsPubSubLite.Errors;
using AwsPubSubLite.Models;
using AwsPubSubLite.Settings;
using Microsoft.Extensions.Logging;

namespace AwsPubSubLite.Internal;

internal sealed class TopicManager
{
    private readonly IAmazonSimpleNotificationService _snsClient;
    private readonly ResourceRegistry _registry;
    private readonly TopicSettings? _topicSettings;
    private readonly ILogger _logger;

    internal TopicManager(
        IAmazonSimpleNotificationService snsClient,
        ResourceRegistry registry,
        TopicSettings? topicSettings,
        ILogger logger)
    {
        _snsClient = snsClient;
        _registry = registry;
        _topicSettings = topicSettings;
        _logger = logger;
    }

    internal async Task<TopicResource> AddTopicAsync(ResourceName name, CancellationToken ct)
    {
        var topicName = name.ToString();

        if (_registry.TryGetTopic(topicName, out var existing))
            return existing;

        var snsArn = await CreateSnsTopicAsync(topicName, ct);
        var topic = new TopicResource(name, snsArn);

        return _registry.GetOrAddTopic(topicName, topic);
    }

    private async Task<string> CreateSnsTopicAsync(string topicName, CancellationToken ct)
    {
        _logger.LogInformation("Creating SNS topic: {TopicName}", topicName);

        CreateTopicResponse response;
        try
        {
            response = await _snsClient.CreateTopicAsync(
                new CreateTopicRequest { Name = topicName }, ct);
        }
        catch (Exception ex) when (ex is not PubSubException && ex is not OperationCanceledException)
        {
            throw PubSubException.SnsTopicCreation(topicName, ex);
        }

        var snsArn = response.TopicArn
            ?? throw PubSubException.GettingSnsTopicArn(topicName);

        _logger.LogInformation("SNS topic created: {TopicName} -> {Arn}", topicName, snsArn);

        if (_topicSettings is not null)
        {
            try
            {
                await _snsClient.SetTopicAttributesAsync(new SetTopicAttributesRequest
                {
                    TopicArn = snsArn,
                    AttributeName = SnsTopicAttribute.DeliveryPolicy.ToAwsString(),
                    AttributeValue = _topicSettings.ToDeliveryPolicyJson()
                }, ct);
            }
            catch (Exception ex) when (ex is not PubSubException && ex is not OperationCanceledException)
            {
                throw PubSubException.SettingTopicAttributes(
                    snsArn, SnsTopicAttribute.DeliveryPolicy.ToAwsString(), ex);
            }

            _logger.LogInformation("Applied DeliveryPolicy to SNS topic: {Arn}", snsArn);
        }

        return snsArn;
    }
}

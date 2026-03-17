using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using AwsPubSubLite.Errors;
using AwsPubSubLite.Models;
using AwsPubSubLite.Settings;
using Microsoft.Extensions.Logging;

namespace AwsPubSubLite.Internal;

internal sealed class QueueManager
{
    private readonly IAmazonSQS _sqsClient;
    private readonly ResourceRegistry _registry;
    private readonly QueueSettings _queueSettings;
    private readonly ILogger _logger;

    internal QueueManager(
        IAmazonSQS sqsClient,
        ResourceRegistry registry,
        QueueSettings queueSettings,
        ILogger logger)
    {
        _sqsClient = sqsClient;
        _registry = registry;
        _queueSettings = queueSettings;
        _logger = logger;
    }

    internal async Task<QueueResource> AddQueueAsync(
        ResourceName name, string snsArn, QueueDlq? dlq, CancellationToken ct)
    {
        var queueName = name.ToString();

        if (_registry.TryGetQueue(queueName, out var existing))
            return existing;

        var (sqsArn, sqsUrl) = await CreateSqsQueueAsync(queueName, snsArn, ct);
        await ApplyQueueAttributesAsync(sqsArn, sqsUrl, snsArn, dlq, ct);

        var queue = new QueueResource(name, sqsArn, sqsUrl, snsArn);
        return _registry.GetOrAddQueue(queueName, queue);
    }

    private async Task<(string Arn, string Url)> CreateSqsQueueAsync(
        string queueName, string snsArn, CancellationToken ct)
    {
        _logger.LogInformation("Creating SQS queue: {QueueName}", queueName);

        CreateQueueResponse createResponse;
        try
        {
            createResponse = await _sqsClient.CreateQueueAsync(
                new CreateQueueRequest { QueueName = queueName }, ct);
        }
        catch (Exception ex) when (ex is not PubSubException && ex is not OperationCanceledException)
        {
            throw PubSubException.SqsQueueCreation(queueName, snsArn, ex);
        }

        var sqsUrl = createResponse.QueueUrl
            ?? throw PubSubException.GettingSqsQueueUrl(queueName);

        GetQueueAttributesResponse attrResponse;
        try
        {
            attrResponse = await _sqsClient.GetQueueAttributesAsync(
                new GetQueueAttributesRequest
                {
                    QueueUrl = sqsUrl,
                    AttributeNames = ["QueueArn"]
                }, ct);
        }
        catch (Exception ex) when (ex is not PubSubException && ex is not OperationCanceledException)
        {
            throw PubSubException.GettingQueueAttributes(queueName, "QueueArn", ex);
        }

        var sqsArn = attrResponse.QueueARN
            ?? throw PubSubException.QueueArnNotFound(queueName);

        _logger.LogInformation("SQS queue ready: {QueueName} -> {Url}", queueName, sqsUrl);
        return (sqsArn, sqsUrl);
    }

    private async Task ApplyQueueAttributesAsync(
        string sqsArn, string sqsUrl, string snsArn, QueueDlq? dlq, CancellationToken ct)
    {
        var retentionSecs = TimeSpan.FromMilliseconds(_queueSettings.MessageRetentionMs).TotalSeconds;

        var attributes = new Dictionary<string, string>
        {
            ["MessageRetentionPeriod"] = ((int)retentionSecs).ToString(),
            ["VisibilityTimeout"] = _queueSettings.VisibilityTimeoutSecs.ToString(),
            ["Policy"] = BuildSqsPolicy(snsArn, sqsArn)
        };

        if (dlq is not null)
            attributes["RedrivePolicy"] = dlq.ToRedrivePolicyJson();

        try
        {
            await _sqsClient.SetQueueAttributesAsync(new SetQueueAttributesRequest
            {
                QueueUrl = sqsUrl,
                Attributes = attributes
            }, ct);
        }
        catch (Exception ex) when (ex is not PubSubException && ex is not OperationCanceledException)
        {
            throw PubSubException.SettingQueueAttributes(sqsArn, "Policy", ex);
        }

        _logger.LogInformation("Queue attributes set for: {QueueArn}", sqsArn);
    }

    internal static string BuildSqsPolicy(string topicArn, string queueArn) =>
        JsonSerializer.Serialize(new
        {
            Version = "2012-10-17",
            Statement = new[]
            {
                new
                {
                    Sid = "AllowSNS",
                    Effect = "Allow",
                    Principal = "*",
                    Action = "SQS:SendMessage",
                    Resource = queueArn,
                    Condition = new
                    {
                        ArnEquals = new Dictionary<string, string>
                        {
                            ["aws:SourceArn"] = topicArn
                        }
                    }
                }
            }
        });
}

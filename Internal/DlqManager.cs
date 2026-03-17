using Amazon.SQS;
using Amazon.SQS.Model;
using AwsPubSubLite.Errors;
using AwsPubSubLite.Models;
using AwsPubSubLite.Settings;
using Microsoft.Extensions.Logging;

namespace AwsPubSubLite.Internal;

internal sealed class DlqManager
{
    private readonly IAmazonSQS _sqsClient;
    private readonly ResourceRegistry _registry;
    private readonly QueueSettings _queueSettings;
    private readonly ILogger _logger;

    internal DlqManager(
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

    internal async Task<DlqHandle> AddDlqAsync(ResourceName name, CancellationToken ct)
    {
        var dlqName = name.ToString();

        if (_registry.Dlqs.TryGetValue(dlqName, out var existing))
            return existing;

        _logger.LogInformation("Creating DLQ: {DlqName}", dlqName);

        CreateQueueResponse createResponse;
        try
        {
            createResponse = await _sqsClient.CreateQueueAsync(
                new CreateQueueRequest { QueueName = dlqName }, ct);
        }
        catch (Exception ex) when (ex is not PubSubException && ex is not OperationCanceledException)
        {
            throw PubSubException.DlqQueueCreation(dlqName, ex);
        }

        var url = createResponse.QueueUrl
            ?? throw PubSubException.GettingSqsQueueUrl(dlqName);

        GetQueueAttributesResponse attrResponse;
        try
        {
            attrResponse = await _sqsClient.GetQueueAttributesAsync(
                new GetQueueAttributesRequest
                {
                    QueueUrl = url,
                    AttributeNames = ["QueueArn"]
                }, ct);
        }
        catch (Exception ex) when (ex is not PubSubException && ex is not OperationCanceledException)
        {
            throw PubSubException.GettingQueueAttributes(dlqName, "QueueArn", ex);
        }

        var arn = attrResponse.QueueARN
            ?? throw PubSubException.QueueArnNotFound(dlqName);

        var retentionSecs = TimeSpan.FromMilliseconds(_queueSettings.DlqMessageRetentionMs).TotalSeconds;

        try
        {
            await _sqsClient.SetQueueAttributesAsync(new SetQueueAttributesRequest
            {
                QueueUrl = url,
                Attributes = new Dictionary<string, string>
                {
                    ["MessageRetentionPeriod"] = ((int)retentionSecs).ToString()
                }
            }, ct);
        }
        catch (Exception ex) when (ex is not PubSubException && ex is not OperationCanceledException)
        {
            throw PubSubException.SettingQueueAttributes(arn, "MessageRetentionPeriod", ex);
        }

        var handle = new DlqHandle(name, url, arn);
        _logger.LogInformation("DLQ created: {DlqName} -> {Url}", dlqName, url);

        return _registry.GetOrAddDlq(dlqName, handle);
    }

    internal async Task<IReadOnlyList<IncomingMessage>> PeekDlqAsync(
        string dlqUrl, int maxMessages, int visibilityTimeoutSecs, CancellationToken ct)
    {
        ReceiveMessageResponse response;
        try
        {
            response = await _sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
            {
                QueueUrl = dlqUrl,
                MaxNumberOfMessages = maxMessages,
                VisibilityTimeout = visibilityTimeoutSecs
            }, ct);
        }
        catch (Exception ex) when (ex is not PubSubException && ex is not OperationCanceledException)
        {
            throw PubSubException.ReceivingMessage(dlqUrl, ex);
        }

        return (response.Messages ?? [])
            .Where(m => m.Body is not null && m.ReceiptHandle is not null)
            .Select(m => new IncomingMessage(m.Body, m.ReceiptHandle))
            .ToList();
    }

    internal async Task<int> DrainDlqAsync(
        string dlqUrl, string targetUrl, int maxMessages, CancellationToken ct)
    {
        const int batchSize = 10;
        const int drainVisibilityTimeoutSecs = 30;

        var drained = 0;
        while (drained < maxMessages)
        {
            ct.ThrowIfCancellationRequested();

            var remaining = maxMessages - drained;
            var currentBatch = Math.Min(batchSize, remaining);

            ReceiveMessageResponse response;
            try
            {
                response = await _sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
                {
                    QueueUrl = dlqUrl,
                    MaxNumberOfMessages = currentBatch,
                    VisibilityTimeout = drainVisibilityTimeoutSecs
                }, ct);
            }
            catch (Exception ex) when (ex is not PubSubException && ex is not OperationCanceledException)
            {
                throw PubSubException.ReceivingMessage(dlqUrl, ex);
            }

            var messages = (response.Messages ?? [])
                .Where(m => m.Body is not null && m.ReceiptHandle is not null)
                .ToList();

            if (messages.Count == 0)
                break;

            var sendEntries = messages
                .Select((m, i) => new SendMessageBatchRequestEntry
                {
                    Id = i.ToString(),
                    MessageBody = m.Body
                })
                .ToList();

            SendMessageBatchResponse sendResponse;
            try
            {
                sendResponse = await _sqsClient.SendMessageBatchAsync(new SendMessageBatchRequest
                {
                    QueueUrl = targetUrl,
                    Entries = sendEntries
                }, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Batch send to target {TargetUrl} failed entirely; leaving messages in DLQ",
                    targetUrl);
                continue;
            }

            if (sendResponse.Failed.Count > 0)
                _logger.LogWarning("{FailedCount} message(s) failed to send to {TargetUrl}",
                    sendResponse.Failed.Count, targetUrl);

            var sentIds = sendResponse.Successful.Select(s => s.Id).ToHashSet();
            var deleteEntries = messages
                .Select((m, i) => (m, i))
                .Where(x => sentIds.Contains(x.i.ToString()))
                .Select(x => new DeleteMessageBatchRequestEntry
                {
                    Id = x.i.ToString(),
                    ReceiptHandle = x.m.ReceiptHandle
                })
                .ToList();

            if (deleteEntries.Count > 0)
            {
                try
                {
                    var deleteResponse = await _sqsClient.DeleteMessageBatchAsync(
                        new DeleteMessageBatchRequest
                        {
                            QueueUrl = dlqUrl,
                            Entries = deleteEntries
                        }, ct);

                    drained += deleteResponse.Successful.Count;

                    if (deleteResponse.Failed.Count > 0)
                        _logger.LogWarning(
                            "{FailedCount} message(s) sent to target but failed to delete from DLQ {DlqUrl}; duplicates possible",
                            deleteResponse.Failed.Count, dlqUrl);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Batch delete from DLQ {DlqUrl} failed; sent messages may be re-drained (duplicates possible)",
                        dlqUrl);
                }
            }
        }

        _logger.LogInformation(
            "Drain complete: {Drained} messages from {DlqUrl} to {TargetUrl}",
            drained, dlqUrl, targetUrl);

        return drained;
    }

    internal async Task DeleteDlqMessageAsync(
        string dlqUrl, string receiptHandle, CancellationToken ct)
    {
        try
        {
            await _sqsClient.DeleteMessageAsync(new DeleteMessageRequest
            {
                QueueUrl = dlqUrl,
                ReceiptHandle = receiptHandle
            }, ct);
        }
        catch (Exception ex) when (ex is not PubSubException && ex is not OperationCanceledException)
        {
            throw PubSubException.DeleteDlqMessage(dlqUrl, ex);
        }
    }
}

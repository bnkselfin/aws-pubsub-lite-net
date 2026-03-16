using System.Runtime.CompilerServices;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.SQS;
using Amazon.SQS.Model;
using AwsPubSubLite.Errors;
using AwsPubSubLite.Models;
using AwsPubSubLite.Settings;
using Microsoft.Extensions.Logging;

namespace AwsPubSubLite.Internal;

internal sealed class MessageSubscriber
{
    private const long MaxDelayMs = 4_294_967_294L;

    private readonly IAmazonSQS _sqsClient;
    private readonly IAmazonSimpleNotificationService _snsClient;
    private readonly ResourceRegistry _registry;
    private readonly QueueSettings _queueSettings;
    private readonly ILogger _logger;

    internal MessageSubscriber(
        IAmazonSQS sqsClient,
        IAmazonSimpleNotificationService snsClient,
        ResourceRegistry registry,
        QueueSettings queueSettings,
        ILogger logger)
    {
        _sqsClient = sqsClient;
        _snsClient = snsClient;
        _registry = registry;
        _queueSettings = queueSettings;
        _logger = logger;
    }

    internal async Task SubscribeAsync(
        string queueName,
        IReadOnlyList<IMessageHandler> handlers,
        HandlerExecutionMode executionMode,
        MessageDeleteMode deleteMode,
        CancellationToken ct)
    {
        if (!_registry.TryGetQueue(queueName, out var queue))
            throw PubSubException.QueueNotExists(queueName);

        if (!queue.TrySubscribe())
        {
            _logger.LogError("Queue '{QueueName}' already subscribed", queueName);
            throw PubSubException.QueueAlreadySubscribed(queueName);
        }

        try
        {
            await EnsureSubscriptionAsync(queue.SnsArn, queue.Arn, ct);

            _logger.LogInformation(
                "Starting subscription loop for queue '{QueueName}' with {HandlerCount} handler(s) [{Mode}]",
                queueName, handlers.Count, executionMode);

            await foreach (var msg in PollSqsStream(queue.Url, ct))
            {
                _logger.LogInformation("Got new message from queue '{QueueName}'", queueName);

                bool allHandled;
                try
                {
                    allHandled = executionMode == HandlerExecutionMode.Parallel
                        ? await RunHandlersParallelAsync(msg, handlers, ct)
                        : await RunHandlersSequentialAsync(msg, handlers, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    _logger.LogInformation("Cancelled during handler execution; skipping delete");
                    break;
                }

                var shouldDelete =
                    deleteMode == MessageDeleteMode.DeleteAllCalled
                    || (deleteMode == MessageDeleteMode.DeleteAllHandled && allHandled);

                if (shouldDelete)
                {
                    try
                    {
                        await DeleteMessageAsync(queue.Url, msg.ReceiptHandle, msg.Body.Length, ct);
                        _logger.LogInformation("Message deleted (len={MessageLength})", msg.Body.Length);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "Failed to delete message from queue '{QueueUrl}'; will redeliver after visibility timeout",
                            queue.Url);
                    }
                }
            }
        }
        finally
        {
            queue.Unsubscribe();
        }
    }

    private async Task EnsureSubscriptionAsync(
        string topicArn, string queueArn, CancellationToken ct)
    {
        string? nextToken = null;
        do
        {
            var request = new ListSubscriptionsByTopicRequest { TopicArn = topicArn };
            if (nextToken is not null)
                request.NextToken = nextToken;

            ListSubscriptionsByTopicResponse response;
            try
            {
                response = await _snsClient.ListSubscriptionsByTopicAsync(request, ct);
            }
            catch (Exception ex) when (ex is not PubSubException && ex is not OperationCanceledException)
            {
                throw PubSubException.ListingSubscriptions(topicArn, ex);
            }

            foreach (var sub in response.Subscriptions ?? [])
            {
                if (sub.Protocol == "sqs" && sub.Endpoint == queueArn)
                {
                    _logger.LogInformation(
                        "Subscription already exists for topic {TopicArn} -> queue {QueueArn}, skipping Subscribe",
                        topicArn, queueArn);
                    return;
                }
            }

            nextToken = response.NextToken;
        } while (nextToken is not null);

        try
        {
            await _snsClient.SubscribeAsync(new SubscribeRequest
            {
                TopicArn = topicArn,
                Protocol = "sqs",
                Endpoint = queueArn
            }, ct);
        }
        catch (Exception ex) when (ex is not PubSubException && ex is not OperationCanceledException)
        {
            throw PubSubException.SubscribingQueue(topicArn, queueArn, ex);
        }

        _logger.LogInformation(
            "Created new SNS subscription: topic {TopicArn} -> queue {QueueArn}",
            topicArn, queueArn);
    }

    private async IAsyncEnumerable<IncomingMessage> PollSqsStream(
        string sqsUrl,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            ReceiveMessageResponse response;
            try
            {
                response = await ReceiveWithRetryAsync(sqsUrl, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                yield break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Polling queue '{QueueUrl}' retries exhausted; will retry on next poll",
                    sqsUrl);
                continue;
            }

            var messages = response.Messages ?? [];
            if (messages.Count > 0)
                _logger.LogDebug("{Count} message(s) pulled from queue", messages.Count);

            foreach (var msg in messages)
            {
                if (msg.Body is not null && msg.ReceiptHandle is not null)
                    yield return new IncomingMessage(msg.Body, msg.ReceiptHandle);
            }
        }
    }

    private async Task<ReceiveMessageResponse> ReceiveWithRetryAsync(
        string sqsUrl, CancellationToken ct)
    {
        var attempt = 0;
        var delayMs = _queueSettings.RetryIntervalMs;
        while (true)
        {
            try
            {
                return await _sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
                {
                    QueueUrl = sqsUrl,
                    MaxNumberOfMessages = _queueSettings.MaxMessageCount,
                    WaitTimeSeconds = _queueSettings.WaitTimeSeconds
                }, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (attempt >= _queueSettings.RetryCount)
                    throw PubSubException.PollingQueue(sqsUrl, ex);

                attempt++;
                _logger.LogWarning(
                    "ReceiveMessage attempt {Attempt}/{Max} failed; retrying in {DelayMs}ms",
                    attempt, _queueSettings.RetryCount, delayMs);
                var sleep = TimeSpan.FromMilliseconds(Math.Min(delayMs, MaxDelayMs));
                await Task.Delay(sleep, ct);
                delayMs = SaturatingMul(delayMs, _queueSettings.RetryIntervalMs);
            }
        }
    }

    private static long SaturatingMul(long a, long b)
    {
        try
        {
            return checked(a * b);
        }
        catch (OverflowException)
        {
            return long.MaxValue;
        }
    }

    private async Task DeleteMessageAsync(
        string queueUrl, string receiptHandle, int messageLength, CancellationToken ct)
    {
        try
        {
            await _sqsClient.DeleteMessageAsync(new DeleteMessageRequest
            {
                QueueUrl = queueUrl,
                ReceiptHandle = receiptHandle
            }, ct);
        }
        catch (Exception ex) when (ex is not PubSubException && ex is not OperationCanceledException)
        {
            throw PubSubException.DeleteMessage(messageLength, queueUrl, ex);
        }
    }

    private async Task<bool> RunHandlersParallelAsync(
        IncomingMessage msg,
        IReadOnlyList<IMessageHandler> handlers,
        CancellationToken ct)
    {
        var tasks = handlers.Select(h => RunSingleHandlerAsync(h, msg, ct)).ToArray();
        var results = await Task.WhenAll(tasks);
        return results.All(r => r);
    }

    private async Task<bool> RunHandlersSequentialAsync(
        IncomingMessage msg,
        IReadOnlyList<IMessageHandler> handlers,
        CancellationToken ct)
    {
        var allHandled = true;
        foreach (var handler in handlers)
        {
            ct.ThrowIfCancellationRequested();
            if (!await RunSingleHandlerAsync(handler, msg, ct))
                allHandled = false;
        }
        return allHandled;
    }

    private async Task<bool> RunSingleHandlerAsync(
        IMessageHandler handler, IncomingMessage msg, CancellationToken ct)
    {
        try
        {
            await handler.HandleAsync(msg, ct);
            return true;
        }
        catch (MessageAlreadyProcessedException)
        {
            _logger.LogWarning(
                "Handler '{HandlerName}' skipped message (len={MessageLength}): already processed",
                handler.HandlerName, msg.Body.Length);
            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Handler '{HandlerName}' failed to process message (len={MessageLength})",
                handler.HandlerName, msg.Body.Length);
            return false;
        }
    }
}

namespace AwsPubSubLite.Errors;

public enum PubSubErrorKind
{
    Generic,
    TopicNotExists,
    QueueNotExists,
    PollingQueue,
    SnsTopicCreation,
    GettingSnsTopicArn,
    SqsQueueCreation,
    GettingSqsQueueUrl,
    GettingQueueAttributes,
    SettingQueueAttributes,
    NoQueueAttributes,
    QueueArnNotFound,
    EmptyMessage,
    PublishMessage,
    QueueAlreadySubscribed,
    SubscribingQueue,
    ListingSubscriptions,
    DeleteMessage,
    SettingTopicAttributes,
    Resource
}

public class PubSubException : Exception
{
    public PubSubErrorKind Kind { get; }

    public PubSubException(string message) : base(message) => Kind = PubSubErrorKind.Generic;

    public PubSubException(string message, Exception inner) : base(message, inner) => Kind = PubSubErrorKind.Generic;

    public PubSubException(PubSubErrorKind kind, string message, Exception? inner = null) : base(message, inner) => Kind = kind;

    public static PubSubException TopicNotExists(string topic) =>
        new(PubSubErrorKind.TopicNotExists, $"Topic '{topic}' not exists");

    public static PubSubException QueueNotExists(string queue) =>
        new(PubSubErrorKind.QueueNotExists, $"Queue '{queue}' not exists");

    public static PubSubException PollingQueue(string queue, Exception inner) =>
        new(PubSubErrorKind.PollingQueue, $"Error polling queue '{queue}': {inner.Message}", inner);

    public static PubSubException SnsTopicCreation(string topic, Exception inner) =>
        new(PubSubErrorKind.SnsTopicCreation, $"Error creating sns topic '{topic}': {inner.Message}", inner);

    public static PubSubException GettingSnsTopicArn(string topic) =>
        new(PubSubErrorKind.GettingSnsTopicArn, $"Error getting SNS topic '{topic}' arn");

    public static PubSubException SqsQueueCreation(string queue, string snsArn, Exception inner) =>
        new(PubSubErrorKind.SqsQueueCreation, $"Error creating sqs queue '{queue}' for sns topic(arn) '{snsArn}': {inner.Message}", inner);

    public static PubSubException GettingSqsQueueUrl(string queue) =>
        new(PubSubErrorKind.GettingSqsQueueUrl, $"Error getting SQS queue '{queue}' url");

    public static PubSubException GettingQueueAttributes(string queue, string attribute, Exception inner) =>
        new(PubSubErrorKind.GettingQueueAttributes, $"Error getting attribute '{attribute}' of SQS queue '{queue}': {inner.Message}", inner);

    public static PubSubException SettingQueueAttributes(string queueArn, string attribute, Exception inner) =>
        new(PubSubErrorKind.SettingQueueAttributes, $"Error setting attribute '{attribute}' of SQS queue '{queueArn}': {inner.Message}", inner);

    public static PubSubException NoQueueAttributes(string queue) =>
        new(PubSubErrorKind.NoQueueAttributes, $"No attributes found for SQS queue '{queue}'");

    public static PubSubException QueueArnNotFound(string queue) =>
        new(PubSubErrorKind.QueueArnNotFound, $"Arn was not found for SQS queue '{queue}'");

    public static PubSubException EmptyMessage() =>
        new(PubSubErrorKind.EmptyMessage, "Empty message");

    public static PubSubException PublishMessage(string topic, int messageLength, Exception inner) =>
        new(PubSubErrorKind.PublishMessage, $"Error pushing message (len={messageLength}) to topic '{topic}': {inner.Message}", inner);

    public static PubSubException QueueAlreadySubscribed(string queue) =>
        new(PubSubErrorKind.QueueAlreadySubscribed, $"Queue '{queue}' already subscribed");

    public static PubSubException SubscribingQueue(string topic, string queueArn, Exception inner) =>
        new(PubSubErrorKind.SubscribingQueue, $"Error subscribing queue(arn) '{queueArn}' to topic '{topic}': {inner.Message}", inner);

    public static PubSubException ListingSubscriptions(string topic, Exception inner) =>
        new(PubSubErrorKind.ListingSubscriptions, $"Error listing subscriptions for SNS topic '{topic}': {inner.Message}", inner);

    public static PubSubException DeleteMessage(int messageLength, string queueUrl, Exception inner) =>
        new(PubSubErrorKind.DeleteMessage, $"Error deleting message (len={messageLength}) in queue(url) '{queueUrl}'", inner);

    public static PubSubException SettingTopicAttributes(string topicArn, string attribute, Exception inner) =>
        new(PubSubErrorKind.SettingTopicAttributes, $"Error setting attribute '{attribute}' of SNS topic '{topicArn}': {inner.Message}", inner);
}

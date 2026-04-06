namespace AwsPubSubLite.Settings;

public sealed class QueueSettings
{
    private int _maxMessageCount = 10;
    private int _retryCount = 3;
    private long _retryIntervalMs = 1000;
    private long _messageRetentionMs = 345_600_000;
    private int _waitTimeSeconds = 20;
    private int _visibilityTimeoutSecs = 30;
    private long _dlqMessageRetentionMs = 1_209_600_000;

    public int MaxMessageCount
    {
        get => _maxMessageCount;
        set => _maxMessageCount = value is >= 1 and <= 10
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value, "Must be between 1 and 10.");
    }

    public int RetryCount
    {
        get => _retryCount;
        set => _retryCount = value >= 0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value, "Must be non-negative.");
    }

    public long RetryIntervalMs
    {
        get => _retryIntervalMs;
        set => _retryIntervalMs = value > 0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value, "Must be positive.");
    }

    public long MessageRetentionMs
    {
        get => _messageRetentionMs;
        set => _messageRetentionMs = value > 0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value, "Must be positive.");
    }

    public int WaitTimeSeconds
    {
        get => _waitTimeSeconds;
        set => _waitTimeSeconds = value is >= 0 and <= 20
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value, "Must be between 0 and 20.");
    }

    public int VisibilityTimeoutSecs
    {
        get => _visibilityTimeoutSecs;
        set => _visibilityTimeoutSecs = value is >= 0 and <= 43200
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value, "Must be between 0 and 43200.");
    }

    public long DlqMessageRetentionMs
    {
        get => _dlqMessageRetentionMs;
        set => _dlqMessageRetentionMs = value > 0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value, "Must be positive.");
    }
}

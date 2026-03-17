namespace AwsPubSubLite.Settings;

public sealed class QueueSettings
{
    public int MaxMessageCount { get; set; } = 10;
    public int RetryCount { get; set; } = 3;
    public long RetryIntervalMs { get; set; } = 1000;
    public long MessageRetentionMs { get; set; } = 345_600_000;
    public int WaitTimeSeconds { get; set; } = 20;
    public int VisibilityTimeoutSecs { get; set; } = 30;
    public long DlqMessageRetentionMs { get; set; } = 1_209_600_000;
}

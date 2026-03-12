namespace AwsPubSubLite.Settings;

public sealed class AwsSettings
{
    public string Region { get; set; } = "us-east-1";
    public string? AccessKey { get; set; }
    public string? SecretKey { get; set; }
    public string? ServiceUrl { get; set; }

    public override string ToString() =>
        $"AwsSettings {{ Region = {Region}, AccessKey = [REDACTED], SecretKey = [REDACTED] }}";
}

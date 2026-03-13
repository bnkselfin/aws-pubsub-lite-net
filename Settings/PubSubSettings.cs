using System.Text.Json;

namespace AwsPubSubLite.Settings;

public sealed class PubSubSettings
{
    public AwsSettings Aws { get; set; } = new();
    public QueueSettings Queue { get; set; } = new();
    public TopicSettings? Topic { get; set; }
    public JsonSerializerOptions? JsonOptions { get; set; }
}

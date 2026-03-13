using System.Text.Json;
using AwsPubSubLite.Models;

namespace AwsPubSubLite.Settings;

public sealed class TopicSettings
{
    public int MinDelayTargetSecs { get; set; }
    public int MaxDelayTargetSecs { get; set; }
    public int NumRetries { get; set; }
    public int NumMaxDelayRetries { get; set; }
    public int NumNoDelayRetries { get; set; }
    public int NumMinDelayRetries { get; set; }
    public BackoffFunction BackoffFunction { get; set; } = BackoffFunction.Linear;

    public string ToDeliveryPolicyJson() => JsonSerializer.Serialize(new
    {
        http = new
        {
            defaultHealthyRetryPolicy = new
            {
                minDelayTarget = MinDelayTargetSecs,
                maxDelayTarget = MaxDelayTargetSecs,
                numRetries = NumRetries,
                numMaxDelayRetries = NumMaxDelayRetries,
                numMinDelayRetries = NumMinDelayRetries,
                numNoDelayRetries = NumNoDelayRetries,
                backoffFunction = BackoffFunction.ToAwsString()
            },
            disableSubscriptionOverrides = false
        }
    });
}

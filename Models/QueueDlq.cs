using System.Text.Json;

namespace AwsPubSubLite.Models;

public sealed record QueueDlq(string Arn, uint MaxReceiveCount)
{
    public string ToRedrivePolicyJson() => JsonSerializer.Serialize(new
    {
        deadLetterTargetArn = Arn,
        maxReceiveCount = MaxReceiveCount.ToString()
    });
}

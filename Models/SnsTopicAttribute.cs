namespace AwsPubSubLite.Models;

public enum SnsTopicAttribute
{
    DeliveryPolicy
}

public static class SnsTopicAttributeExtensions
{
    public static string ToAwsString(this SnsTopicAttribute attr) => attr switch
    {
        SnsTopicAttribute.DeliveryPolicy => "DeliveryPolicy",
        _ => throw new ArgumentOutOfRangeException(nameof(attr))
    };
}

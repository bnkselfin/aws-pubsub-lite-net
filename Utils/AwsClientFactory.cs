using Amazon;
using Amazon.Runtime;
using Amazon.SimpleNotificationService;
using Amazon.SQS;
using AwsPubSubLite.Settings;

namespace AwsPubSubLite.Utils;

internal static class AwsClientFactory
{
    internal static IAmazonSQS CreateSqsClient(AwsSettings settings)
    {
        var config = new AmazonSQSConfig
        {
            RegionEndpoint = RegionEndpoint.GetBySystemName(settings.Region)
        };

        if (!string.IsNullOrWhiteSpace(settings.ServiceUrl))
            config.ServiceURL = settings.ServiceUrl;

        var credentials = ResolveCredentials(settings);
        return credentials is not null
            ? new AmazonSQSClient(credentials, config)
            : new AmazonSQSClient(config);
    }

    internal static IAmazonSimpleNotificationService CreateSnsClient(AwsSettings settings)
    {
        var config = new AmazonSimpleNotificationServiceConfig
        {
            RegionEndpoint = RegionEndpoint.GetBySystemName(settings.Region)
        };

        if (!string.IsNullOrWhiteSpace(settings.ServiceUrl))
            config.ServiceURL = settings.ServiceUrl;

        var credentials = ResolveCredentials(settings);
        return credentials is not null
            ? new AmazonSimpleNotificationServiceClient(credentials, config)
            : new AmazonSimpleNotificationServiceClient(config);
    }

    private static AWSCredentials? ResolveCredentials(AwsSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.AccessKey) &&
            !string.IsNullOrWhiteSpace(settings.SecretKey))
            return new BasicAWSCredentials(settings.AccessKey, settings.SecretKey);

        return null;
    }
}

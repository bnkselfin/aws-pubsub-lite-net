using AwsPubSubLite.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace AwsPubSubLite;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAwsPubSubLite(
        this IServiceCollection services,
        Action<PubSubSettings> configure)
    {
        services.Configure(configure);
        services.AddSingleton<IPubSub, PubSub>();
        return services;
    }
}

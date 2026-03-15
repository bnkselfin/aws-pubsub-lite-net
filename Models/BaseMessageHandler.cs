namespace AwsPubSubLite.Models;

public abstract class BaseMessageHandler : IMessageHandler
{
    public abstract string HandlerName { get; }

    public abstract Task HandleAsync(IncomingMessage message, CancellationToken ct = default);
}

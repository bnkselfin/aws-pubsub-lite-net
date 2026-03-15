namespace AwsPubSubLite.Models;

public interface IMessageHandler
{
    string HandlerName { get; }

    Task HandleAsync(IncomingMessage message, CancellationToken ct = default);
}

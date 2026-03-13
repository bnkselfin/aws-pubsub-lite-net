namespace AwsPubSubLite.Errors;

public sealed class MessageAlreadyProcessedException : PubSubException
{
    public string HandlerName { get; }
    public int MessageLength { get; }

    public MessageAlreadyProcessedException(string handlerName, int messageLength)
        : base($"Message (len={messageLength}) has already been processed by handler '{handlerName}'")
    {
        HandlerName = handlerName;
        MessageLength = messageLength;
    }
}

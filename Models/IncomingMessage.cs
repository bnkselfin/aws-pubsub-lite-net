using System.Text.Json;

namespace AwsPubSubLite.Models;

public sealed record IncomingMessage(string Body, string ReceiptHandle)
{
    public T? Deserialize<T>(JsonSerializerOptions? options = null)
    {
        if (TryUnwrapSnsEnvelope(out var inner))
            return JsonSerializer.Deserialize<T>(inner, options);

        return JsonSerializer.Deserialize<T>(Body, options);
    }

    private bool TryUnwrapSnsEnvelope(out string innerBody)
    {
        innerBody = Body;
        try
        {
            using var doc = JsonDocument.Parse(Body);
            if (doc.RootElement.TryGetProperty("Message", out var messageProp))
            {
                innerBody = messageProp.GetString() ?? Body;
                return true;
            }
        }
        catch (JsonException) { }
        return false;
    }
}

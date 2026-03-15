namespace AwsPubSubLite.Models;

public sealed record IncomingMessage(string Body, string ReceiptHandle);

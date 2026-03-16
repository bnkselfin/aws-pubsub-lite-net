using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using AwsPubSubLite.Models;

namespace AwsPubSubLite.Internal;

internal sealed class ResourceRegistry
{
    private readonly ConcurrentDictionary<string, TopicResource> _topics = new();
    private readonly ConcurrentDictionary<string, QueueResource> _queues = new();

    public IReadOnlyDictionary<string, TopicResource> Topics => _topics;
    public IReadOnlyDictionary<string, QueueResource> Queues => _queues;

    public TopicResource GetOrAddTopic(string name, TopicResource topic) =>
        _topics.GetOrAdd(name, topic);

    public QueueResource GetOrAddQueue(string name, QueueResource queue) =>
        _queues.GetOrAdd(name, queue);

    public bool TryGetTopic(string name, [MaybeNullWhen(false)] out TopicResource topic) =>
        _topics.TryGetValue(name, out topic);

    public bool TryGetQueue(string name, [MaybeNullWhen(false)] out QueueResource queue) =>
        _queues.TryGetValue(name, out queue);
}

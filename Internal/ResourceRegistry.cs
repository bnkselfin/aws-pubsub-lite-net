using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using AwsPubSubLite.Models;

namespace AwsPubSubLite.Internal;

internal sealed class ResourceRegistry
{
    private readonly ConcurrentDictionary<string, TopicResource> _topics = new();
    private readonly ConcurrentDictionary<string, QueueResource> _queues = new();
    private readonly ConcurrentDictionary<string, DlqHandle> _dlqs = new();

    public IReadOnlyDictionary<string, TopicResource> Topics => _topics;
    public IReadOnlyDictionary<string, QueueResource> Queues => _queues;
    public IReadOnlyDictionary<string, DlqHandle> Dlqs => _dlqs;

    public TopicResource GetOrAddTopic(string name, TopicResource topic) =>
        _topics.GetOrAdd(name, topic);

    public QueueResource GetOrAddQueue(string name, QueueResource queue) =>
        _queues.GetOrAdd(name, queue);

    public DlqHandle GetOrAddDlq(string name, DlqHandle dlq) =>
        _dlqs.GetOrAdd(name, dlq);

    public bool TryGetTopic(string name, [MaybeNullWhen(false)] out TopicResource topic) =>
        _topics.TryGetValue(name, out topic);

    public bool TryGetQueue(string name, [MaybeNullWhen(false)] out QueueResource queue) =>
        _queues.TryGetValue(name, out queue);
}

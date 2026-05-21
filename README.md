# AwsPubSubLite

A lightweight .NET library that wraps AWS SNS (topics) and SQS (queues) into a single `PubSub` abstraction. It owns topic/queue/DLQ provisioning, redrive wiring, publishing, polling, and handler dispatch.

## Features

- **Single orchestrator** — `PubSub` owns topics, queues, and DLQs.
- **Idempotent provisioning** — every cold start reconciles to the desired AWS state.
- **Built-in DLQ** — provision via `AddDlqAsync`, attach via `QueueDlq?`, recover via `PeekDlqAsync` / `DrainDlqAsync` / `DeleteDlqMessageAsync`.
- **Concurrent-safe** — `ConcurrentDictionary` registries, lock-free `Interlocked` subscription state, no shared-mutable footguns.
- **Long polling by default** — 20s SQS wait, no client-side throttling.
- **Handler isolation** — exceptions are caught and logged; the worker keeps running.
- **Parallel or sequential handlers** — chosen per `SubscribeAsync` call.
- **Graceful shutdown** — `CancellationToken`-driven; cooperative handler cancellation.
- **Typed errors** — `PubSubException` carries a `PubSubErrorKind` with SDK exceptions preserved as `InnerException`; message bodies scrubbed to `len=N`.

## Quick start

`.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="AwsPubSubLite" Version="1.0.0" />
  <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="8.*" />
  <PackageReference Include="Microsoft.Extensions.Logging.Console" Version="8.*" />
</ItemGroup>
```

Minimal usage:

```csharp
using AwsPubSubLite;
using AwsPubSubLite.Models;
using AwsPubSubLite.Settings;
using Microsoft.Extensions.DependencyInjection;

sealed class MyHandler : BaseMessageHandler
{
    public override string HandlerName => "MyHandler";

    public override Task HandleAsync(IncomingMessage message, CancellationToken ct = default)
    {
        Console.WriteLine($"got: {message.Body}");
        return Task.CompletedTask;
    }
}

var services = new ServiceCollection();
services.AddLogging(b => b.AddConsole());
services.AddAwsPubSubLite(options =>
{
    options.Aws.Region    = Environment.GetEnvironmentVariable("AWS_REGION")!;
    options.Aws.AccessKey = Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID");
    options.Aws.SecretKey = Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY");

    options.Queue = new QueueSettings
    {
        MaxMessageCount       = 1,
        RetryCount            = 15,
        RetryIntervalMs       = 2000,
        MessageRetentionMs    = 600_000,
        WaitTimeSeconds       = 20,
        VisibilityTimeoutSecs = 60,
        DlqMessageRetentionMs = 1_209_600_000
    };
});

await using var provider = services.BuildServiceProvider();
var pubsub = provider.GetRequiredService<IPubSub>();

var naming = new ResourceNamingOptions
{
    Prefix          = "v1",
    Suffix          = "topic",
    PrefixSeparator = SeparatorSymbol.Hyphen,
    SuffixSeparator = SeparatorSymbol.Underscore
};

var topic = await pubsub.AddTopicAsync(
    ResourceName.Create("orders", ResourceType.Topic, naming));
var dlq = await pubsub.AddDlqAsync(
    ResourceName.Create("orders-dlq", ResourceType.Queue, naming));
var queue = await pubsub.AddQueueAsync(
    ResourceName.Create("orders", ResourceType.Queue, naming),
    topic.Arn,
    new QueueDlq(dlq.Arn, 5));

await pubsub.SubscribeAsync(
    queue.Name.ToString(),
    new IMessageHandler[] { new MyHandler() },
    HandlerExecutionMode.Sequential,
    MessageDeleteMode.DeleteAllHandled,
    CancellationToken.None);
```

## Configuration

Configuration is supplied through `AddAwsPubSubLite(...)` (bind it to environment variables, `appsettings.json`, or any `IConfiguration` source):

```jsonc
{
  "Aws":   { "Region": "eu-north-1", "AccessKey": "...", "SecretKey": "..." },
  "Queue": {
    "MaxMessageCount":       1,
    "RetryCount":            15,
    "RetryIntervalMs":       2000,
    "MessageRetentionMs":    600000,
    "WaitTimeSeconds":       20,
    "VisibilityTimeoutSecs": 60,
    "DlqMessageRetentionMs": 1209600000
  },
  "Topic": {
    "MinDelayTargetSecs": 5,
    "MaxDelayTargetSecs": 60,
    "NumRetries":         3,
    "NumMaxDelayRetries": 0,
    "NumNoDelayRetries":  0,
    "NumMinDelayRetries": 0,
    "BackoffFunction":    "Linear"
  }
}
```

> Never hard-code AWS credentials — use environment variables, AWS profiles, or IAM roles. `AwsSettings.ToString()` redacts both keys.

**DLQ tuning:** `(MessageRetentionMs / 1000) / VisibilityTimeoutSecs >= maxReceiveCount` must hold, or messages get purged before AWS routes them to the DLQ. The values above give `600 / 60 = 10`, so `maxReceiveCount` up to 10 is safe.

## How it works

- **Provisioning** — `AddTopicAsync` / `AddQueueAsync` / `AddDlqAsync` are idempotent on AWS and in-process: registry pre-lookup, AWS work, race-safe `GetOrAdd`. Manual queue-attribute edits are reverted on the next start (the app is the source of truth, IaC-style).
- **Subscribe** — looks up the queue, atomic `Interlocked` CAS on the subscribed flag (fails fast with `QueueAlreadySubscribed`), subscribes to SNS only if no matching subscription exists, then drives the polling stream in an `await foreach` loop. Per message, handlers run sequentially or in parallel and delete is decided by `deleteMode` + `allHandled`.
- **DLQ routing** — purely AWS-driven. On failure the library just doesn't delete; after `maxReceiveCount` deliveries AWS moves the message to the DLQ via the `RedrivePolicy` written at `AddQueueAsync`.
- **DLQ recovery** — operator-triggered only: `PeekDlqAsync` (inspect), `DrainDlqAsync` (replay to a target queue, send-then-delete), `DeleteDlqMessageAsync` (drop one). Nothing runs automatically.

## Idempotency contract

Every `IMessageHandler` **must** be idempotent and throw `MessageAlreadyProcessedException` when it detects a duplicate. The library uses that signal to safely delete duplicates under `DeleteAllHandled` (from `DrainDlqAsync` replays or concurrent retries).

```csharp
public override async Task HandleAsync(IncomingMessage message, CancellationToken ct = default)
{
    var id = ParseId(message.Body);
    if (await _dedupStore.ContainsAsync(id, ct))
        throw new MessageAlreadyProcessedException(HandlerName, message.Body.Length);

    await _dedupStore.AddAsync(id, ct);
}
```

## Build

```bash
dotnet restore
dotnet build
dotnet test
```

The project targets **net10.0** — a matching .NET 10 SDK is required.

## Caveats

- Subscribe is one-shot per process — no dynamic add/remove of handlers.
- Cold-start re-applies queue attributes; manual console edits get reverted (use settings for per-environment overrides).
- Handler exceptions are caught but still flip `allHandled = false` (the message redelivers under `DeleteAllHandled`).
- Parallel handlers cannot be force-aborted on cancellation — handlers must observe the `CancellationToken` to stop promptly.
- No tests yet — pure logic is testable; AWS-touching code needs LocalStack or similar.

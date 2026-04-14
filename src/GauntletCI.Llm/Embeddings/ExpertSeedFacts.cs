// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Llm.Embeddings;

/// <summary>
/// Ten hand-curated expert facts about .NET resource lifecycle and concurrency,
/// sourced from high-signal discussions in dotnet/runtime, dotnet/roslyn, and aws/aws-sdk-net.
/// </summary>
public static class ExpertSeedFacts
{
    public static readonly IReadOnlyList<SeedFact> All =
    [
        new(
            Id:      "dotnet/runtime#issues/84530",
            Content: "HttpClient is thread-safe and designed to be reused across requests; creating a new instance per request causes socket exhaustion via TIME_WAIT port accumulation.",
            Source:  "https://github.com/dotnet/runtime/issues/84530"),

        new(
            Id:      "dotnet/runtime#issues/31848",
            Content: "MemoryPool<T>.Shared rentals must be returned in a finally block; abandoned rentals silently accumulate as untracked memory pressure with no GC reclamation.",
            Source:  "https://github.com/dotnet/runtime/issues/31848"),

        new(
            Id:      "dotnet/runtime#issues/23878",
            Content: "CancellationTokenSource.Cancel() is synchronous and runs all registered callbacks on the calling thread, risking deadlock if any callback blocks.",
            Source:  "https://github.com/dotnet/runtime/issues/23878"),

        new(
            Id:      "dotnet/runtime#issues/13391",
            Content: "ValueTask must not be awaited more than once; re-awaiting a completed ValueTask is undefined behavior and can silently corrupt state.",
            Source:  "https://github.com/dotnet/runtime/issues/13391"),

        new(
            Id:      "dotnet/runtime#issues/22144",
            Content: "SemaphoreSlim.WaitAsync() is preferred over lock() in async code paths because lock() can cause ThreadPool starvation when combined with await inside the lock body.",
            Source:  "https://github.com/dotnet/runtime/issues/22144"),

        new(
            Id:      "dotnet/runtime#issues/55974",
            Content: "IAsyncEnumerable<T> enumerators must be disposed via await foreach or explicit DisposeAsync(); failing to dispose leaks underlying connections or file handles.",
            Source:  "https://github.com/dotnet/runtime/issues/55974"),

        new(
            Id:      "dotnet/runtime#issues/14267",
            Content: "Task.WhenAll does not short-circuit on first failure; all tasks continue running and all exceptions are aggregated — callers must inspect AggregateException.InnerExceptions.",
            Source:  "https://github.com/dotnet/runtime/issues/14267"),

        new(
            Id:      "dotnet/runtime#issues/36060",
            Content: "StringBuilder is not thread-safe; concurrent Append calls from multiple threads produce silently corrupted output without throwing any exception.",
            Source:  "https://github.com/dotnet/runtime/issues/36060"),

        new(
            Id:      "dotnet/roslyn#issues/21165",
            Content: "ImmutableArray<T> has a dangerous default value with a null backing array; always initialize with ImmutableArray.Create() or ImmutableArray<T>.Empty, never with default.",
            Source:  "https://github.com/dotnet/roslyn/issues/21165"),

        new(
            Id:      "aws/aws-sdk-net#issues/1310",
            Content: "AmazonS3Client is thread-safe and must be shared as a singleton; instantiating it per-request causes connection pool exhaustion and significant latency spikes.",
            Source:  "https://github.com/aws/aws-sdk-net/issues/1310"),
    ];
}

public sealed record SeedFact(string Id, string Content, string Source);

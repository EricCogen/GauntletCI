// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Corpus.Labeling;

public sealed record LlmEndpoint(string Name, ILlmLabeler Labeler);

/// <summary>
/// Distributes LLM classification requests across multiple underlying labelers while
/// failing over when one endpoint returns no result. Repeatedly unhealthy endpoints are
/// temporarily removed from rotation and retried after a cooldown.
/// </summary>
public sealed class RoundRobinLlmLabeler : ILlmLabeler, IDisposable
{
    private sealed class EndpointState(LlmEndpoint endpoint)
    {
        public LlmEndpoint Endpoint { get; } = endpoint;
        public object SyncRoot { get; } = new();
        public int ConsecutiveFailures { get; set; }
        public DateTime DisabledUntilUtc { get; set; }
    }

    private readonly IReadOnlyList<EndpointState> _endpoints;
    private readonly IReadOnlyList<IDisposable> _disposables;
    private readonly TimeSpan _cooldown;
    private readonly int _failureThreshold;
    private int _nextIndex = -1;

    public RoundRobinLlmLabeler(
        IEnumerable<LlmEndpoint> endpoints,
        int failureThreshold = 3,
        TimeSpan? cooldown = null)
    {
        if (failureThreshold < 1)
            throw new ArgumentOutOfRangeException(nameof(failureThreshold), "Failure threshold must be at least 1.");

        _endpoints = (endpoints ?? throw new ArgumentNullException(nameof(endpoints)))
            .Select(endpoint => new EndpointState(endpoint))
            .ToArray();

        if (_endpoints.Count == 0)
            throw new ArgumentException("At least one labeler is required.", nameof(endpoints));

        _disposables = _endpoints
            .Select(state => state.Endpoint.Labeler)
            .OfType<IDisposable>()
            .ToArray();

        _failureThreshold = failureThreshold;
        _cooldown = cooldown ?? TimeSpan.FromMinutes(1);
    }

    public RoundRobinLlmLabeler(IEnumerable<ILlmLabeler> labelers)
        : this((labelers ?? throw new ArgumentNullException(nameof(labelers)))
            .Select((labeler, index) => new LlmEndpoint($"endpoint-{index + 1}", labeler)))
    {
    }

    public async Task<LlmLabelResult?> ClassifyAsync(
        string ruleId,
        string findingMessage,
        string evidence,
        string? filePath,
        IEnumerable<string> reviewCommentBodies,
        string diffSnippet,
        CancellationToken ct = default)
    {
        // Cast to uint before modulo to avoid OverflowException when _nextIndex wraps to int.MinValue.
        var startIndex = (int)((uint)Interlocked.Increment(ref _nextIndex) % (uint)_endpoints.Count);
        var attemptedHealthyEndpoint = false;

        for (var offset = 0; offset < _endpoints.Count; offset++)
        {
            var endpointIndex = (startIndex + offset) % _endpoints.Count;
            var state = _endpoints[endpointIndex];
            if (!IsAvailable(state, DateTime.UtcNow))
                continue;

            attemptedHealthyEndpoint = true;
            var result = await state.Endpoint.Labeler.ClassifyAsync(
                ruleId,
                findingMessage,
                evidence,
                filePath,
                reviewCommentBodies,
                diffSnippet,
                ct);

            if (result is not null)
            {
                RegisterSuccess(state);
                return result;
            }

            if (RegisterFailure(state))
            {
                Console.Error.WriteLine(
                    $"[llm] Endpoint {state.Endpoint.Name} disabled for {_cooldown.TotalSeconds:0}s after {_failureThreshold} consecutive failures.");
            }
        }

        if (!attemptedHealthyEndpoint)
        {
            var nextRetryAt = _endpoints
                .Select(state =>
                {
                    lock (state.SyncRoot)
                    {
                        return state.DisabledUntilUtc;
                    }
                })
                .Min();

            Console.Error.WriteLine(
                $"[llm] All endpoints are temporarily disabled. Next retry after {nextRetryAt:O}.");
        }

        return null;
    }

    private static bool IsAvailable(EndpointState state, DateTime nowUtc)
    {
        lock (state.SyncRoot)
        {
            return state.DisabledUntilUtc <= nowUtc;
        }
    }

    private bool RegisterFailure(EndpointState state)
    {
        lock (state.SyncRoot)
        {
            state.ConsecutiveFailures++;
            if (state.ConsecutiveFailures < _failureThreshold || state.DisabledUntilUtc > DateTime.UtcNow)
                return false;

            state.DisabledUntilUtc = DateTime.UtcNow.Add(_cooldown);
            // Reset so the endpoint gets a full failure window after cooldown expires.
            state.ConsecutiveFailures = 0;
            return true;
        }
    }

    private static void RegisterSuccess(EndpointState state)
    {
        lock (state.SyncRoot)
        {
            state.ConsecutiveFailures = 0;
            state.DisabledUntilUtc = DateTime.MinValue;
        }
    }

    public void Dispose()
    {
        foreach (var disposable in _disposables)
            disposable.Dispose();
    }
}

// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Corpus.Labeling;
using GauntletCI.Corpus.Interfaces;

namespace GauntletCI.Tests;

public sealed class RoundRobinLlmLabelerTests
{
    // Fake labeler that returns a fixed result or null, and tracks call order.
    private sealed class FakeLabeler(LlmLabelResult? returnValue = null) : ILlmLabeler
    {
        public int CallCount { get; private set; }
        public string Name { get; init; } = "fake";

        public Task<LlmLabelResult?> ClassifyAsync(
            string ruleId, string findingMessage, string evidence,
            string? filePath, IEnumerable<string> reviewCommentBodies,
            string diffSnippet, CancellationToken ct = default)
        {
            CallCount++;
            return Task.FromResult(returnValue);
        }
    }

    private static LlmLabelResult SomeResult() =>
        new(true, 0.9, "test", false);

    // ────────────────────────────────────────────────────────────────────────
    // Round-robin rotation
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Calls_endpoints_in_round_robin_order()
    {
        var a = new FakeLabeler(SomeResult()) { Name = "a" };
        var b = new FakeLabeler(SomeResult()) { Name = "b" };
        var c = new FakeLabeler(SomeResult()) { Name = "c" };

        var rr = new RoundRobinLlmLabeler(
        [
            new LlmEndpoint("a", a),
            new LlmEndpoint("b", b),
            new LlmEndpoint("c", c),
        ]);

        // Six calls → each endpoint gets exactly 2 turns.
        for (var i = 0; i < 6; i++)
            await rr.ClassifyAsync("GCI0001", "", "", null, [], "", default);

        Assert.Equal(2, a.CallCount);
        Assert.Equal(2, b.CallCount);
        Assert.Equal(2, c.CallCount);
    }

    // ────────────────────────────────────────────────────────────────────────
    // Disable-after-threshold
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Disables_endpoint_after_consecutive_failures()
    {
        var bad  = new FakeLabeler(null) { Name = "bad" };   // always fails
        var good = new FakeLabeler(SomeResult()) { Name = "good" };

        // threshold=2: bad is disabled after 2 consecutive failures.
        // With 2 endpoints, bad is tried on startIndex=0 calls (odd iterations).
        // 3 calls → bad is tried on calls 1 and 3 → 2 failures → disabled.
        var rr = new RoundRobinLlmLabeler(
        [
            new LlmEndpoint("bad",  bad),
            new LlmEndpoint("good", good),
        ],
        failureThreshold: 2,
        cooldown: TimeSpan.FromHours(1));

        for (var i = 0; i < 3; i++)
        {
            var result = await rr.ClassifyAsync("GCI0001", "", "", null, [], "", default);
            Assert.NotNull(result);  // good always rescues
        }

        // bad should now be disabled for 1 hour.
        var badCallsBefore = bad.CallCount;
        for (var i = 0; i < 10; i++)
            await rr.ClassifyAsync("GCI0001", "", "", null, [], "", default);

        Assert.Equal(badCallsBefore, bad.CallCount);  // bad received no further calls
        Assert.True(good.CallCount > badCallsBefore);
    }

    // ────────────────────────────────────────────────────────────────────────
    // ConsecutiveFailures reset on disable — endpoint gets full window on re-entry
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Resets_consecutive_failures_on_disable()
    {
        // Use a very short cooldown so we can test re-entry in-process.
        var bad  = new FakeLabeler(null) { Name = "bad" };
        var good = new FakeLabeler(SomeResult()) { Name = "good" };

        // threshold=2; drive bad to disable via 3 calls (bad hit on calls 1 and 3).
        var rr = new RoundRobinLlmLabeler(
        [
            new LlmEndpoint("bad",  bad),
            new LlmEndpoint("good", good),
        ],
        failureThreshold: 2,
        cooldown: TimeSpan.FromMilliseconds(50));

        for (var i = 0; i < 3; i++)
            await rr.ClassifyAsync("GCI0001", "", "", null, [], "", default);

        var badCallsAfterDisable = bad.CallCount;

        // Wait for cooldown to expire, then make one more call.
        // If ConsecutiveFailures was NOT reset on disable, bad would be disabled
        // immediately again on the first null result (counter still >= threshold).
        // With the fix, bad.CF was reset to 0 on disable, so it gets a full
        // threshold window before being disabled again.
        await Task.Delay(100);

        // Make 2 calls — the round-robin counter alternates between index 0 (bad)
        // and index 1 (good), so at least one of the two will route through bad.
        await rr.ClassifyAsync("GCI0001", "", "", null, [], "", default);
        await rr.ClassifyAsync("GCI0001", "", "", null, [], "", default);

        // bad should have been called at least once (it re-entered rotation).
        Assert.True(bad.CallCount > badCallsAfterDisable,
            "bad endpoint should have been called at least once after cooldown expired");
    }

    // ────────────────────────────────────────────────────────────────────────
    // All endpoints disabled → returns null
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Returns_null_when_all_endpoints_disabled()
    {
        var bad = new FakeLabeler(null) { Name = "bad" };

        var rr = new RoundRobinLlmLabeler(
        [
            new LlmEndpoint("bad", bad),
        ],
        failureThreshold: 1,
        cooldown: TimeSpan.FromHours(1));

        // First call disables the only endpoint; result is null (no healthy fallback).
        var first = await rr.ClassifyAsync("GCI0001", "", "", null, [], "", default);
        Assert.Null(first);

        // Second call — all disabled; should return null immediately.
        var second = await rr.ClassifyAsync("GCI0001", "", "", null, [], "", default);
        Assert.Null(second);
    }

    // ────────────────────────────────────────────────────────────────────────
    // No overflow on int.MinValue wrap-around
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Does_not_throw_when_internal_counter_wraps_around()
    {
        var labeler = new FakeLabeler(SomeResult());
        var rr = new RoundRobinLlmLabeler([ new LlmEndpoint("e", labeler) ]);

        // Force _nextIndex close to int.MaxValue by reflection so the very
        // next Increment wraps to int.MinValue, previously triggering Math.Abs overflow.
        var field = typeof(RoundRobinLlmLabeler)
            .GetField("_nextIndex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        field.SetValue(rr, int.MaxValue - 1);

        var exception = await Record.ExceptionAsync(
            () => rr.ClassifyAsync("GCI0001", "", "", null, [], "", default));

        Assert.Null(exception);
    }

    // ────────────────────────────────────────────────────────────────────────
    // IDisposable — disposes underlying labelers that implement IDisposable
    // ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Dispose_disposes_underlying_labelers()
    {
        var disposableLabeler = new DisposableFakeLabeler();
        var rr = new RoundRobinLlmLabeler([ new LlmEndpoint("e", disposableLabeler) ]);
        rr.Dispose();
        Assert.True(disposableLabeler.Disposed);
    }

    private sealed class DisposableFakeLabeler : ILlmLabeler, IDisposable
    {
        public bool Disposed { get; private set; }
        public void Dispose() => Disposed = true;
        public Task<LlmLabelResult?> ClassifyAsync(string ruleId, string findingMessage,
            string evidence, string? filePath, IEnumerable<string> reviewCommentBodies,
            string diffSnippet, CancellationToken ct = default)
            => Task.FromResult<LlmLabelResult?>(null);
    }
}

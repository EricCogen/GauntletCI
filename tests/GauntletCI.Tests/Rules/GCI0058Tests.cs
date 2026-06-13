// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Rules.Implementations;

namespace GauntletCI.Tests.Rules;

public sealed class GCI0058Tests
{
    private static readonly GCI0058_PairedImplementationConsistency Rule = new(new StubPatternProvider());

    [Fact]
    public async Task InvertedSiblingPredicate_ShouldFire()
    {
        var raw = """
            diff --git a/src/Subscription.cs b/src/Subscription.cs
            index abc..def 100644
            --- a/src/Subscription.cs
            +++ b/src/Subscription.cs
            @@ -10,20 +10,30 @@
             internal sealed class SingleNodeSubscription : Subscription
             {
                 protected override void RemoveDisconnectedEndpoints()
                 {
                     foreach (var endpoint in endpoints)
                     {
                         if (!IsSubscriberConnected(endpoint))
                         {
                             endpoints.Remove(endpoint);
                         }
                     }
                 }
             }
             
             internal sealed class MultiNodeSubscription : Subscription
             {
                 protected override void RemoveDisconnectedEndpoints()
                 {
                     foreach (var endpoint in endpoints)
                     {
            +            if (IsSubscriberConnected(endpoint))
            +            {
            +                endpoints.Remove(endpoint);
            +            }
                     }
                 }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        var finding = Assert.Single(findings);
        Assert.Equal("GCI0058", finding.RuleId);
        Assert.Contains("IsSubscriberConnected", finding.Evidence, StringComparison.Ordinal);
        Assert.Contains("MultiNodeSubscription", finding.Evidence, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MatchingSiblingPredicate_ShouldNotFire()
    {
        var raw = """
            diff --git a/src/Subscription.cs b/src/Subscription.cs
            index abc..def 100644
            --- a/src/Subscription.cs
            +++ b/src/Subscription.cs
            @@ -10,20 +10,30 @@
             internal sealed class SingleNodeSubscription : Subscription
             {
                 protected override void RemoveDisconnectedEndpoints()
                 {
                     foreach (var endpoint in endpoints)
                     {
                         if (!IsSubscriberConnected(endpoint))
                         {
                             endpoints.Remove(endpoint);
                         }
                     }
                 }
             }
             
             internal sealed class MultiNodeSubscription : Subscription
             {
                 protected override void RemoveDisconnectedEndpoints()
                 {
                     foreach (var endpoint in endpoints)
                     {
            +            if (!IsSubscriberConnected(endpoint))
            +            {
            +                endpoints.Remove(endpoint);
            +            }
                     }
                 }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task RedisStylePropertyPattern_ShouldFire()
    {
        var raw = """
            diff --git a/src/Subscription.cs b/src/Subscription.cs
            index abc..def 100644
            --- a/src/Subscription.cs
            +++ b/src/Subscription.cs
            @@ -1,1 +1,40 @@
            +    internal sealed class SingleNodeSubscription : Subscription
            +    {
            +        internal override void RemoveDisconnectedEndpoints()
            +        {
            +            var server = _currentServer;
            +            if (server is { IsSubscriberConnected: false })
            +            {
            +                _currentServer = null;
            +            }
            +        }
            +    }
            +
            +    internal sealed class MultiNodeSubscription : Subscription
            +    {
            +        internal override void RemoveDisconnectedEndpoints()
            +        {
            +            foreach (var server in _servers)
            +            {
            +                if (server.Value.IsSubscriberConnected)
            +                {
            +                    scratch[count++] = server.Key;
            +                }
            +            }
            +        }
            +    }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        var finding = Assert.Single(findings);
        Assert.Equal("GCI0058", finding.RuleId);
        Assert.Contains("IsSubscriberConnected", finding.Evidence, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WhileLoopSiblingPolarity_ShouldFire()
    {
        var raw = """
            diff --git a/src/Worker.cs b/src/Worker.cs
            index abc..def 100644
            --- a/src/Worker.cs
            +++ b/src/Worker.cs
            @@ -1,1 +1,24 @@
            +    sealed class FastWorker
            +    {
            +        internal void Drain()
            +        {
            +            while (HasPendingWork())
            +                Process();
            +        }
            +    }
            +
            +    sealed class SlowWorker
            +    {
            +        internal void Drain()
            +        {
            +            while (!HasPendingWork())
            +                Process();
            +        }
            +    }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        var finding = Assert.Single(findings);
        Assert.Equal("GCI0058", finding.RuleId);
        Assert.Contains("HasPendingWork", finding.Evidence, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TernarySiblingPolarity_ShouldFire()
    {
        var raw = """
            diff --git a/src/Gate.cs b/src/Gate.cs
            index abc..def 100644
            --- a/src/Gate.cs
            +++ b/src/Gate.cs
            @@ -1,1 +1,16 @@
            +    sealed class StrictGate
            +    {
            +        internal bool Allow(Request r)
            +        {
            +            return CanAccept(r) ? true : false;
            +        }
            +    }
            +
            +    sealed class LooseGate
            +    {
            +        internal bool Allow(Request r)
            +        {
            +            return !CanAccept(r) ? true : false;
            +        }
            +    }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        var finding = Assert.Single(findings);
        Assert.Equal("GCI0058", finding.RuleId);
        Assert.Contains("CanAccept", finding.Evidence, StringComparison.Ordinal);
    }
}

// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Rules.Implementations;

namespace GauntletCI.Tests.Rules;

public sealed class GCI0059Tests
{
    private static readonly GCI0059_GuardDeletionRemoteUse Rule = new(new StubPatternProvider());

    [Fact]
    public async Task RemovedNullGuardWithContinuedUse_ShouldFire()
    {
        var raw = """
            diff --git a/src/OrderService.cs b/src/OrderService.cs
            index abc..def 100644
            --- a/src/OrderService.cs
            +++ b/src/OrderService.cs
            @@ -10,12 +10,10 @@
             internal sealed class OrderService
             {
                 internal bool TryCharge(Order? order)
                 {
            -        if (order == null)
            -            return false;
                     return order.Total > 0;
                 }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        var finding = Assert.Single(findings);
        Assert.Equal("GCI0059", finding.RuleId);
        Assert.Contains("order", finding.Evidence, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GuardRemovedButReplaced_ShouldNotFire()
    {
        var raw = """
            diff --git a/src/OrderService.cs b/src/OrderService.cs
            index abc..def 100644
            --- a/src/OrderService.cs
            +++ b/src/OrderService.cs
            @@ -10,14 +10,14 @@
             internal sealed class OrderService
             {
                 internal bool TryCharge(Order? order)
                 {
            -        if (order == null)
            -            return false;
            +        if (order is null)
            +            return false;
                     return order.Total > 0;
                 }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task RemovedThrowIfNullWithAddedUse_ShouldFire()
    {
        var raw = """
            diff --git a/src/CacheReader.cs b/src/CacheReader.cs
            index abc..def 100644
            --- a/src/CacheReader.cs
            +++ b/src/CacheReader.cs
            @@ -20,10 +20,12 @@
             internal sealed class CacheReader
             {
                 internal string Read(string key)
                 {
            -        ArgumentNullException.ThrowIfNull(_cache);
            +        var hit = _cache.TryGetValue(key, out var value);
            +        return hit ? value : string.Empty;
                 }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        var finding = Assert.Single(findings);
        Assert.Equal("GCI0059", finding.RuleId);
        Assert.Contains("_cache", finding.Evidence, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GuardAndDereferenceBothRemoved_ShouldNotFire()
    {
        var raw = """
            diff --git a/src/OrderService.cs b/src/OrderService.cs
            index abc..def 100644
            --- a/src/OrderService.cs
            +++ b/src/OrderService.cs
            @@ -10,12 +10,8 @@
             internal sealed class OrderService
             {
                 internal bool TryCharge(Order? order)
                 {
            -        if (order == null)
            -            return false;
            -        return order.Total > 0;
            +        return true;
                 }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task SameMethodNameDifferentClasses_ShouldNotCrossMatch()
    {
        var raw = """
            diff --git a/src/Services.cs b/src/Services.cs
            index abc..def 100644
            --- a/src/Services.cs
            +++ b/src/Services.cs
            @@ -1,20 +1,18 @@
             internal sealed class OrderService
             {
                 internal bool Process(Order? order)
                 {
            -        if (order == null)
            -            return false;
                 }
             }
             internal sealed class InvoiceService
             {
                 internal bool Process(Invoice? invoice)
                 {
                     return invoice.Total > 0;
                 }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task ReplacementGuardAfterUnguardedUse_ShouldFire()
    {
        var raw = """
            diff --git a/src/OrderService.cs b/src/OrderService.cs
            index abc..def 100644
            --- a/src/OrderService.cs
            +++ b/src/OrderService.cs
            @@ -10,14 +10,14 @@
             internal sealed class OrderService
             {
                 internal bool TryCharge(Order? order)
                 {
            -        if (order == null)
            -            return false;
                     return order.Total > 0;
            +        if (order is null)
            +            return false;
                 }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Single(findings);
    }
}

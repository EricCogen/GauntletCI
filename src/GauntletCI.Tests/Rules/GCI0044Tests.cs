// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Rules.Implementations;

namespace GauntletCI.Tests.Rules;

public class GCI0044Tests
{
    private static readonly GCI0044_PerformanceHotpathRisk Rule = new();

    [Fact]
    public async Task EmptyDiff_ShouldNotFire()
    {
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,3 +1,4 @@
             public class Service {
            +    public string GetUser(int id) => _repo.Get(id)?.Name ?? "unknown";
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task ThreadSleep_InProductionCode_ShouldFire()
    {
        var raw = """
            diff --git a/src/PollingService.cs b/src/PollingService.cs
            index abc..def 100644
            --- a/src/PollingService.cs
            +++ b/src/PollingService.cs
            @@ -1,3 +1,6 @@
             public class PollingService {
            +    public void Poll() {
            +        Thread.Sleep(1000);
            +        DoWork();
            +    }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("Thread.Sleep"));
    }

    [Fact]
    public async Task LinqInsideLoop_ShouldFire()
    {
        var raw = """
            diff --git a/src/ReportService.cs b/src/ReportService.cs
            index abc..def 100644
            --- a/src/ReportService.cs
            +++ b/src/ReportService.cs
            @@ -1,3 +1,9 @@
             public class ReportService {
            +    public void Generate(List<Order> orders) {
            +        foreach (var order in orders) {
            +            var items = allItems.Where(i => i.OrderId == order.Id).ToList();
            +            Process(items);
            +        }
            +    }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("LINQ"));
    }

    [Fact]
    public async Task AddInsideWhileLoop_ShouldFire()
    {
        // Unbounded while loop with .Add — this is a genuine memory growth risk
        var raw = """
            diff --git a/src/AggregatorService.cs b/src/AggregatorService.cs
            index abc..def 100644
            --- a/src/AggregatorService.cs
            +++ b/src/AggregatorService.cs
            @@ -1,3 +1,9 @@
             public class AggregatorService {
            +    public List<Result> Poll() {
            +        var results = new List<Result>();
            +        while (reader.HasNext()) {
            +            results.Add(reader.ReadNext());
            +        }
            +        return results;
            +    }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains(".Add"));
    }

    [Fact]
    public async Task AddInsideForeachLoop_ShouldNotFire()
    {
        // foreach is bounded by its input — accumulating results is a standard safe pattern
        var raw = """
            diff --git a/src/AggregatorService.cs b/src/AggregatorService.cs
            index abc..def 100644
            --- a/src/AggregatorService.cs
            +++ b/src/AggregatorService.cs
            @@ -1,3 +1,9 @@
             public class AggregatorService {
            +    public List<Result> Aggregate(IEnumerable<Item> items) {
            +        var results = new List<Result>();
            +        foreach (var item in items) {
            +            results.Add(Transform(item));
            +        }
            +        return results;
            +    }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains(".Add"));
    }

    [Fact]
    public async Task ThreadSleep_InTestFile_ShouldNotFire()
    {
        var raw = """
            diff --git a/src/PollingServiceTests.cs b/src/PollingServiceTests.cs
            index abc..def 100644
            --- a/src/PollingServiceTests.cs
            +++ b/src/PollingServiceTests.cs
            @@ -1,3 +1,5 @@
             public class PollingServiceTests {
            +    [Fact]
            +    public void SlowTest() { Thread.Sleep(100); Assert.True(true); }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("Thread.Sleep"));
    }

    [Fact]
    public async Task CleanProductionCode_ShouldNotFire()
    {
        var raw = """
            diff --git a/src/OrderService.cs b/src/OrderService.cs
            index abc..def 100644
            --- a/src/OrderService.cs
            +++ b/src/OrderService.cs
            @@ -1,3 +1,8 @@
             public class OrderService {
            +    public async Task<List<Order>> GetOrdersAsync(int userId) {
            +        var orders = await _repo.GetByUserAsync(userId);
            +        return orders.Where(o => o.IsActive).ToList();
            +    }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Empty(findings);
    }
}

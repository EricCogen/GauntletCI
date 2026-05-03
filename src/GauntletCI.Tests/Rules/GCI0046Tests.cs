// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Rules;
using GauntletCI.Core.Rules.Implementations;

namespace GauntletCI.Tests.Rules;

public class GCI0046Tests
{
    private static readonly GCI0046_PatternConsistencyDeviation Rule = new(new StubPatternProvider());

    [Fact]
    public async Task ServiceLocatorCall_ShouldFire()
    {
        var raw = """
            diff --git a/src/UserController.cs b/src/UserController.cs
            index abc..def 100644
            --- a/src/UserController.cs
            +++ b/src/UserController.cs
            @@ -1,3 +1,5 @@
             public class UserController {
            +    public IActionResult Get(int id) {
            +        var svc = _provider.GetRequiredService<IUserService>();
            +        return Ok(svc.Get(id));
            +    }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("Service locator") || f.Summary.Contains("service locator"));
    }

    [Fact]
    public async Task ServiceLocatorCurrent_ShouldFire()
    {
        var raw = """
            diff --git a/src/LegacyHelper.cs b/src/LegacyHelper.cs
            index abc..def 100644
            --- a/src/LegacyHelper.cs
            +++ b/src/LegacyHelper.cs
            @@ -1,3 +1,4 @@
             public class LegacyHelper {
            +    var svc = ServiceLocator.Current.GetService<IFooService>();
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("Service locator") || f.Summary.Contains("service locator"));
    }

    [Fact]
    public async Task ConstructorInjection_ShouldNotFire()
    {
        var raw = """
            diff --git a/src/OrderController.cs b/src/OrderController.cs
            index abc..def 100644
            --- a/src/OrderController.cs
            +++ b/src/OrderController.cs
            @@ -1,3 +1,8 @@
             public class OrderController {
            +    private readonly IOrderService _svc;
            +    public OrderController(IOrderService svc) {
            +        _svc = svc;
            +    }
            +    public IActionResult Get(int id) => Ok(_svc.GetOrder(id));
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f =>
            f.Summary.Contains("Service locator") || f.Summary.Contains("service locator"));
    }

    [Fact]
    public async Task MixedSyncAsync_ShouldFire()
    {
        var raw = """
            diff --git a/src/DataService.cs b/src/DataService.cs
            index abc..def 100644
            --- a/src/DataService.cs
            +++ b/src/DataService.cs
            @@ -1,3 +1,7 @@
             public class DataService {
            +    public Task<Data> LoadDataAsync(int id) {
            +        return _repo.GetAsync(id);
            +    }
            +    public Data LoadData(int id) {
            +        return _repo.Get(id);
            +    }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("LoadData") || f.Summary.Contains("sync/async"));
    }

    [Fact]
    public async Task AsyncOnlyMethods_ShouldNotFireMixedCheck()
    {
        var raw = """
            diff --git a/src/ReportService.cs b/src/ReportService.cs
            index abc..def 100644
            --- a/src/ReportService.cs
            +++ b/src/ReportService.cs
            @@ -1,3 +1,7 @@
             public class ReportService {
            +    public async Task<Report> GenerateReportAsync(int id) {
            +        return await _repo.GetReportAsync(id);
            +    }
            +    public async Task<List<Report>> GetAllReportsAsync() {
            +        return await _repo.GetAllAsync();
            +    }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("sync/async") || f.Summary.Contains("Async"));
    }

    [Fact]
    public async Task PatternDefinitionStringLiteral_ShouldNotFire()
    {
        var raw = """
            diff --git a/src/MyDetector.cs b/src/MyDetector.cs
            index abc..def 100644
            --- a/src/MyDetector.cs
            +++ b/src/MyDetector.cs
            @@ -1,3 +1,8 @@
             public class MyDetector {
            +    private static readonly string[] Patterns =
            +        [".GetService<", ".GetRequiredService<", "ServiceLocator.Current"];
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f =>
            f.Summary.Contains("Service locator") || f.Summary.Contains("service locator"));
    }

    [Fact]
    public async Task AllowlistedSyncAsyncPair_ShouldNotFire()
    {
        var raw = """
            diff --git a/src/EventBus.cs b/src/EventBus.cs
            index abc..def 100644
            --- a/src/EventBus.cs
            +++ b/src/EventBus.cs
            @@ -1,3 +1,7 @@
             public class EventBus {
            +    public void Subscribe(string topic, Action handler) {
            +        _handlers[topic] = handler;
            +    }
            +    public async Task SubscribeAsync(string topic, Func<Task> handler) {
            +        await _store.RegisterAsync(topic, handler);
            +    }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var rule = new GCI0046_PatternConsistencyDeviation(new StubPatternProvider());
        rule.Configure(new GauntletCI.Core.Configuration.GauntletConfig
        {
            PatternConsistency = new GauntletCI.Core.Configuration.PatternConsistencyConfig
            {
                AllowedSyncAsyncPairs = ["Subscribe"]
            }
        });
        var findings = await rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("Subscribe"));
    }

    [Fact]
    public async Task AutofacResolvePattern_ShouldFire()
    {
        var raw = """
            diff --git a/src/Factory.cs b/src/Factory.cs
            index abc..def 100644
            --- a/src/Factory.cs
            +++ b/src/Factory.cs
            @@ -1,3 +1,4 @@
             public class Factory {
            +    var svc = container.Resolve<IMyService>();
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("Service locator"));
    }

    [Fact]
    public async Task GetInstancePattern_ShouldFire()
    {
        var raw = """
            diff --git a/src/Locator.cs b/src/Locator.cs
            index abc..def 100644
            --- a/src/Locator.cs
            +++ b/src/Locator.cs
            @@ -1,3 +1,4 @@
             public class Locator {
            +    var svc = _registry.GetInstance<IService>();
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("Service locator"));
    }

    [Fact]
    public async Task SyncAddedWithExistingAsync_ShouldFire()
    {
        var raw = """
            diff --git a/src/DataService.cs b/src/DataService.cs
            index abc..def 100644
            --- a/src/DataService.cs
            +++ b/src/DataService.cs
            @@ -1,5 +1,7 @@
             public class DataService {
                 public async Task<Data> LoadDataAsync(int id) {
                     return await _repo.GetAsync(id);
                 }
            +    public Data LoadData(int id) {
            +        return _repo.Get(id);
            +    }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("sync/async"));
    }

    [Fact]
    public async Task ServiceLocatorInString_ShouldNotFire()
    {
        var raw = """
            diff --git a/src/Logger.cs b/src/Logger.cs
            index abc..def 100644
            --- a/src/Logger.cs
            +++ b/src/Logger.cs
            @@ -1,3 +1,4 @@
             public class Logger {
            +    Log("Warning: use container.Resolve<T>() instead of ServiceLocator");
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("Service locator"));
    }
}

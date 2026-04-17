// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Rules.Implementations;

namespace GauntletCI.Tests.Rules;

public class GCI0046Tests
{
    private static readonly GCI0046_PatternConsistencyDeviation Rule = new();

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
}

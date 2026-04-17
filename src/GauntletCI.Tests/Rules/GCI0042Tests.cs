// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Rules.Implementations;

namespace GauntletCI.Tests.Rules;

public class GCI0042Tests
{
    private static readonly GCI0042_TodoStubDetection Rule = new();

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
    public async Task TodoComment_InProductionFile_ShouldFire()
    {
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,3 +1,5 @@
             public class Service {
            +    // TODO: implement this properly
            +    public void Process() { }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("TODO/stub"));
    }

    [Fact]
    public async Task MultipleMarkers_ShouldAggregateIntoOneFindings()
    {
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,3 +1,8 @@
             public class Service {
            +    // TODO: fix this
            +    // FIXME: broken
            +    // HACK: temporary workaround
            +    public void Process() { }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Single(findings);
        Assert.Contains("3", findings[0].Summary);
    }

    [Fact]
    public async Task NotImplementedException_ShouldFire()
    {
        var raw = """
            diff --git a/src/OrderService.cs b/src/OrderService.cs
            index abc..def 100644
            --- a/src/OrderService.cs
            +++ b/src/OrderService.cs
            @@ -1,3 +1,5 @@
             public class OrderService {
            +    public void ProcessOrder() {
            +        throw new NotImplementedException();
            +    }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("TODO/stub"));
    }

    [Fact]
    public async Task TodoInTestFile_ShouldNotFire()
    {
        var raw = """
            diff --git a/src/ServiceTests.cs b/src/ServiceTests.cs
            index abc..def 100644
            --- a/src/ServiceTests.cs
            +++ b/src/ServiceTests.cs
            @@ -1,3 +1,5 @@
             public class ServiceTests {
            +    // TODO: add more test cases
            +    throw new NotImplementedException("test helper");
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Empty(findings);
    }
}

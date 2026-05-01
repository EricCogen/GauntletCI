// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;
using GauntletCI.Core.Rules.Implementations;

namespace GauntletCI.Tests.Rules;

public class GCI0054Tests
{
    private static readonly GCI0054_AsyncVoidAbuse Rule = new();

    [Fact]
    public async Task PublicAsyncVoidMethod_ShouldFire()
    {
        var raw = """
            diff --git a/src/UserService.cs b/src/UserService.cs
            index abc..def 100644
            --- a/src/UserService.cs
            +++ b/src/UserService.cs
            @@ -1,5 +1,5 @@
             public class UserService {
            -    public Task UpdateUserAsync(int id, string name) => _repo.UpdateAsync(id, name);
            +    public async void UpdateUserAsync(int id, string name) { await _repo.UpdateAsync(id, name); }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f =>
            f.Summary.Contains("async void") &&
            f.Confidence == Confidence.High);
    }

    [Fact]
    public async Task AsyncVoidEventHandler_ShouldNotFire()
    {
        var raw = """
            diff --git a/src/Form.cs b/src/Form.cs
            index abc..def 100644
            --- a/src/Form.cs
            +++ b/src/Form.cs
            @@ -1,5 +1,5 @@
             public class Form {
            -    void OnSubmitButtonClicked() { }
            +    public async void OnSubmitButtonClicked() { await HandleSubmitAsync(); }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("async void"));
    }

    [Fact]
    public async Task AsyncVoidWithEventHandlerName_ShouldNotFire()
    {
        var raw = """
            diff --git a/src/Control.cs b/src/Control.cs
            index abc..def 100644
            --- a/src/Control.cs
            +++ b/src/Control.cs
            @@ -1,5 +1,5 @@
             public class Control {
            -    void OnClick() { }
            +    public async void OnClick() { await ProcessAsync(); }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("async void"));
    }

    [Fact]
    public async Task AsyncTaskMethod_ShouldNotFire()
    {
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,5 +1,5 @@
             public class Service {
            -    public void DoWork() { }
            +    public async Task DoWorkAsync() { await _dep.ProcessAsync(); }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task PrivateAsyncVoid_ShouldNotFire()
    {
        var raw = """
            diff --git a/src/Internal.cs b/src/Internal.cs
            index abc..def 100644
            --- a/src/Internal.cs
            +++ b/src/Internal.cs
            @@ -1,5 +1,5 @@
             public class Internal {
            -    void Helper() { }
            +    private async void Helper() { await ProcessAsync(); }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("async void"));
    }

    [Fact]
    public async Task AsyncVoidInTestFile_ShouldNotFire()
    {
        var raw = """
            diff --git a/tests/ServiceTests.cs b/tests/ServiceTests.cs
            index abc..def 100644
            --- a/tests/ServiceTests.cs
            +++ b/tests/ServiceTests.cs
            @@ -1,5 +1,5 @@
             public class ServiceTests {
            -    void Test() { }
            +    public async void TestMethod() { await _svc.ProcessAsync(); }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Empty(findings);
    }
}

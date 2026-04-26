// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Rules.Implementations;

namespace GauntletCI.Tests.Rules;

public class GCI0032Tests
{
    private static readonly GCI0032_UncaughtExceptionPath Rule = new();

    [Fact]
    public async Task ThrowNewWithoutTestEvidence_ShouldFlag()
    {
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,3 +1,5 @@
             public class Service {
            +    if (!state.IsReady) throw new InvalidOperationException("Service not ready");
            +    if (quota.Exceeded) throw new QuotaExceededException("Rate limit hit");
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("2") && f.Summary.Contains("throw new"));
    }

    [Fact]
    public async Task ThrowNewWithAssertThrowsEvidence_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,3 +1,4 @@
             public class Service {
            +    if (id == null) throw new ArgumentNullException(nameof(id));
             }
            diff --git a/src/ServiceTests.cs b/src/ServiceTests.cs
            index abc..def 100644
            --- a/src/ServiceTests.cs
            +++ b/src/ServiceTests.cs
            @@ -1,3 +1,4 @@
             public class ServiceTests {
            +    Assert.Throws<ArgumentNullException>(() => service.Get(null));
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("throw new"));
    }

    [Fact]
    public async Task NoThrowNew_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,3 +1,4 @@
             public class Service {
            +    return defaultValue;
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task ThrowNewInTestFile_ShouldNotFlag()
    {
        var raw = """
            diff --git a/src/ServiceTests.cs b/src/ServiceTests.cs
            index abc..def 100644
            --- a/src/ServiceTests.cs
            +++ b/src/ServiceTests.cs
            @@ -1,3 +1,4 @@
             public class ServiceTests {
            +    if (x) throw new InvalidOperationException("test helper");
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task OnlyNotImplementedException_GCI0042OwnsIt_ShouldNotFlag()
    {
        // GCI0042 (TODO/Stub Detection) is the authoritative reporter for NotImplementedException.
        // GCI0032 must not double-report the same throw.
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,3 +1,4 @@
             public class Service {
            +    public void DoWork() { throw new NotImplementedException(); }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task GuardClauseThrows_ShouldNotFlag()
    {
        // ArgumentNullException, ArgumentException, ArgumentOutOfRangeException, and
        // ObjectDisposedException are defensive guard clauses — they do not represent
        // untested business logic paths and must not trigger this rule.
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,3 +1,6 @@
             public class Service {
            +    if (id == null) throw new ArgumentNullException(nameof(id));
            +    if (id < 0) throw new ArgumentOutOfRangeException(nameof(id));
            +    if (!valid) throw new ArgumentException("Must be valid", nameof(id));
            +    if (_disposed) throw new ObjectDisposedException(nameof(Service));
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Empty(findings);
    }
}
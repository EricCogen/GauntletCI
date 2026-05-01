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
            +    if (state.HasError) throw new Exception("Service error detected");
            +    if (quota.Exceeded) throw new CustomException("Rate limit hit");
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
        // ObjectDisposedException are defensive guard clauses: they do not represent
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

    [Fact]
    public async Task SingleLineEmptyCatch_ShouldFlag()
    {
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,5 +1,8 @@
             public class Service {
            +    try {
            +        DoWork();
            +    } catch (Exception ex) { }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("empty") && f.Summary.Contains("catch"));
    }

    [Fact]
    public async Task MultiLineEmptyCatch_ShouldFlag()
    {
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,5 +1,10 @@
             public class Service {
            +    try {
            +        DoWork();
            +    } catch (IOException ex) {
            +        // intentionally ignored
            +    }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("empty") && f.Summary.Contains("catch"));
    }

    [Fact]
    public async Task CatchWithExecutableStatement_ShouldNotFlag()
    {
        // A catch block with actual code (e.g., logging or rethrow) is not empty.
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,5 +1,10 @@
             public class Service {
            +    try {
            +        DoWork();
            +    } catch (IOException ex) {
            +        _logger.LogError(ex, "DoWork failed");
            +        throw;
            +    }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("empty") && f.Summary.Contains("catch"));
    }

    [Fact]
    public async Task EmptyCatchInTestFile_ShouldNotFlag()
    {
        // Empty catch blocks in test files (e.g., expected-exception patterns) are not flagged.
        var raw = """
            diff --git a/src/ServiceTests.cs b/src/ServiceTests.cs
            index abc..def 100644
            --- a/src/ServiceTests.cs
            +++ b/src/ServiceTests.cs
            @@ -1,5 +1,8 @@
             public class ServiceTests {
            +    try { service.Run(); } catch { }
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("empty") && f.Summary.Contains("catch"));
    }

    [Fact]
    public async Task ThrowNewWithRemovedAssertionInTestFile_ShouldFlag()
    {
        // Removed (-) assertion lines must not suppress the finding.
        // If the only Assert.Throws evidence was deleted, the throw path is now untested.
        var raw = """
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,3 +1,4 @@
             public class Service {
            +    throw new CustomException("Processing failed");
             }
            diff --git a/src/ServiceTests.cs b/src/ServiceTests.cs
            index abc..def 100644
            --- a/src/ServiceTests.cs
            +++ b/src/ServiceTests.cs
            @@ -1,4 +1,3 @@
             public class ServiceTests {
            -    Assert.Throws<CustomException>(() => service.Do());
             }
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("throw new"));
    }
}
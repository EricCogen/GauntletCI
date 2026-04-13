// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Rules.Implementations;

namespace GauntletCI.Tests.Rules;

public class GCI0013Tests
{
    private static readonly GCI0013_ObservabilityDebugability Rule = new();

    [Fact]
    public async Task LargeAdditionWithNoLogging_ShouldFlag()
    {
        // 20+ added lines with no logging pattern
        var addedLines = string.Join("\n", Enumerable.Range(1, 22).Select(i => $"+    var x{i} = {i};"));
        var raw = $"""
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,1 +1,23 @@
             // service
            {addedLines}
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("lines added") && f.Summary.Contains("no logging"));
    }

    [Fact]
    public async Task PublicClassWithoutXmlDocs_ShouldNotFlag_OwnerIsGCI0026()
    {
        // Public API XML doc checking is owned by GCI0026 (Public API Documentation).
        // GCI0013 must not duplicate this check.
        var raw = """
            diff --git a/src/MyService.cs b/src/MyService.cs
            index abc..def 100644
            --- a/src/MyService.cs
            +++ b/src/MyService.cs
            @@ -1,1 +1,2 @@
             // existing
            +public class MyService
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("without XML documentation"));
    }

    [Fact]
    public async Task PublicClassWithXmlDocs_ShouldNotFlagDocs()
    {
        var raw = """
            diff --git a/src/MyService.cs b/src/MyService.cs
            index abc..def 100644
            --- a/src/MyService.cs
            +++ b/src/MyService.cs
            @@ -1,1 +1,3 @@
             // existing
            +/// <summary>My service.</summary>
            +public class MyService
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("without XML documentation"));
    }

    [Fact]
    public async Task XUnitTestFile_ShouldNotFlag()
    {
        // The [Fact] attribute appears as a context line (not added) — rule must
        // inspect all hunk lines, not just added lines, to detect test files.
        var addedLines = string.Join("\n", Enumerable.Range(1, 22).Select(i => $"+    var x{i} = {i};"));
        var raw = $"""
            diff --git a/src/ServiceTests.cs b/src/ServiceTests.cs
            index abc..def 100644
            --- a/src/ServiceTests.cs
            +++ b/src/ServiceTests.cs
            @@ -1,3 +1,25 @@
             using Xunit;
             public class ServiceTests {{
             [Fact] public async Task Foo() {{
            {addedLines}
             }}
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("lines added") && f.Summary.Contains("no logging"));
    }

    [Fact]
    public async Task NUnitTestFile_ShouldNotFlag()
    {
        var addedLines = string.Join("\n", Enumerable.Range(1, 22).Select(i => $"+    var x{i} = {i};"));
        var raw = $"""
            diff --git a/src/MySpec.cs b/src/MySpec.cs
            index abc..def 100644
            --- a/src/MySpec.cs
            +++ b/src/MySpec.cs
            @@ -1,2 +1,24 @@
             using NUnit.Framework;
             [TestFixture] public class MySpec {{
            {addedLines}
             }}
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("lines added") && f.Summary.Contains("no logging"));
    }

    [Fact]
    public async Task NonCsFile_ShouldNotBeChecked()
    {
        // GCI0013 only checks .cs files
        var addedLines = string.Join("\n", Enumerable.Range(1, 25).Select(i => $"+const x{i} = {i};"));
        var raw = $"""
            diff --git a/src/service.ts b/src/service.ts
            index abc..def 100644
            --- a/src/service.ts
            +++ b/src/service.ts
            @@ -1,1 +1,26 @@
             // service
            {addedLines}
            """;

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("lines added") && f.Summary.Contains("no logging"));
    }
}

// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Rules.Implementations;

namespace GauntletCI.Tests.Rules;

public class GCI0009Tests
{
    private static readonly GCI0009_ConsistencyWithPatterns Rule = new();

    private static DiffContext MakeDiff(params string[] addedLines)
    {
        var added = string.Join("\n", addedLines.Select(l => $"+{l}"));
        return DiffParser.Parse($"""
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,1 +1,{1 + addedLines.Length} @@
             // existing
            {added}
            """);
    }

    private static DiffContext MakeDiffWithContext(string contextLine, params string[] addedLines)
    {
        var added = string.Join("\n", addedLines.Select(l => $"+{l}"));
        return DiffParser.Parse($"""
            diff --git a/src/Service.cs b/src/Service.cs
            index abc..def 100644
            --- a/src/Service.cs
            +++ b/src/Service.cs
            @@ -1,1 +1,{1 + addedLines.Length} @@
             {contextLine}
            {added}
            """);
    }

    // --- *Async naming mismatch (high confidence) ---

    [Fact]
    public async Task SyncMethodNamedAsync_ShouldFlagFinding()
    {
        var diff = MakeDiff("    public string GetUserAsync(int id) { return null; }");
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("*Async"));
    }

    [Fact]
    public async Task ProperAsyncMethod_ShouldNotFlag()
    {
        var diff = MakeDiff("    public async Task<string> GetUserAsync(int id) { return null; }");
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("*Async"));
    }

    [Fact]
    public async Task TaskReturningMethodNamedAsync_ShouldNotFlag()
    {
        var diff = MakeDiff("    public Task<User> FetchUserAsync(int id);");
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("*Async"));
    }

    // --- Sync method in async codebase (low confidence) ---

    [Fact]
    public async Task SyncGetMethodInAsyncFile_ShouldFlagFinding()
    {
        var diff = MakeDiffWithContext(
            "    public async Task<string> LoadConfig() { }",
            "    public string GetUser(int id) { return null; }");

        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("GetUser"));
    }

    [Fact]
    public async Task SyncGetMethodInNonAsyncFile_ShouldNotFlag()
    {
        var diff = MakeDiff("    public string GetUser(int id) { return null; }");
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("GetUser"));
    }

    [Fact]
    public async Task AsyncGetMethodInAsyncFile_ShouldNotFlag()
    {
        var diff = MakeDiffWithContext(
            "    public async Task<string> LoadConfig() { }",
            "    public async Task<string> GetUser(int id) { return null; }");

        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("Synchronous method") && f.Summary.Contains("GetUser"));
    }

    // --- String comparison antipattern ---

    [Fact]
    public async Task ToLowerInEquality_ShouldFlagFinding()
    {
        var diff = MakeDiff("    if (name.ToLower() == \"admin\") { }");
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains(".ToLower()"));
    }

    [Fact]
    public async Task ToUpperInContains_ShouldFlagFinding()
    {
        var diff = MakeDiff("    var match = list.Where(x => x.ToUpper().Contains(\"TEST\")).ToList();");
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains(".ToUpper()"));
    }

    [Fact]
    public async Task ToLowerNotInComparison_ShouldNotFlag()
    {
        var diff = MakeDiff("    var lower = name.ToLower();");
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains(".ToLower()"));
    }

    [Fact]
    public async Task OrdinalIgnoreCase_ShouldNotFlag()
    {
        var diff = MakeDiff("    if (string.Equals(name, \"admin\", StringComparison.OrdinalIgnoreCase)) { }");
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Empty(findings);
    }
}

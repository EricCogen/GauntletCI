// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Rules.Implementations;

namespace GauntletCI.Tests.Rules;

public class GCI0025Tests
{
    private static readonly GCI0025_FeatureFlagReadiness Rule = new();

    [Fact]
    public async Task LargeChangeToAuthFileWithoutFlag_ShouldFlag()
    {
        var addedLines = string.Join("\n", Enumerable.Range(1, 55).Select(i => $"+    var x{i} = {i};"));
        var raw = "diff --git a/src/AuthService.cs b/src/AuthService.cs\n" +
            "index abc..def 100644\n" +
            "--- a/src/AuthService.cs\n" +
            "+++ b/src/AuthService.cs\n" +
            "@@ -1,1 +1,56 @@\n" +
            " public class AuthService {\n" +
            addedLines + "\n" +
            " }";

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.Summary.Contains("feature flag") || f.Summary.Contains("no feature flag"));
    }

    [Fact]
    public async Task LargeChangeToAuthFileWithFlag_ShouldNotFlag()
    {
        var addedLines = string.Join("\n", Enumerable.Range(1, 55).Select(i => $"+    var x{i} = {i};"));
        var raw = "diff --git a/src/AuthService.cs b/src/AuthService.cs\n" +
            "index abc..def 100644\n" +
            "--- a/src/AuthService.cs\n" +
            "+++ b/src/AuthService.cs\n" +
            "@@ -1,1 +1,57 @@\n" +
            " public class AuthService {\n" +
            "+    var enabled = await _featureManager.IsEnabledAsync(\"new-auth-flow\");\n" +
            addedLines + "\n" +
            " }";

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("feature flag"));
    }

    [Fact]
    public async Task SmallChangeToAuthFile_ShouldNotFlag()
    {
        // Fewer than 50 lines — should not trigger
        var addedLines = string.Join("\n", Enumerable.Range(1, 10).Select(i => $"+    var x{i} = {i};"));
        var raw = "diff --git a/src/AuthService.cs b/src/AuthService.cs\n" +
            "index abc..def 100644\n" +
            "--- a/src/AuthService.cs\n" +
            "+++ b/src/AuthService.cs\n" +
            "@@ -1,1 +1,11 @@\n" +
            " public class AuthService {\n" +
            addedLines + "\n" +
            " }";

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("feature flag"));
    }

    [Fact]
    public async Task LargeChangeToNonCriticalFile_ShouldNotFlag()
    {
        // Large change but to a non-critical-path file
        var addedLines = string.Join("\n", Enumerable.Range(1, 55).Select(i => $"+    var x{i} = {i};"));
        var raw = "diff --git a/src/Utilities.cs b/src/Utilities.cs\n" +
            "index abc..def 100644\n" +
            "--- a/src/Utilities.cs\n" +
            "+++ b/src/Utilities.cs\n" +
            "@@ -1,1 +1,56 @@\n" +
            " public class Utilities {\n" +
            addedLines + "\n" +
            " }";

        var diff = DiffParser.Parse(raw);
        var findings = await Rule.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.Summary.Contains("feature flag"));
    }
}

// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;
using GauntletCI.Core.Rules;
using GauntletCI.Core.Rules.Implementations;

namespace GauntletCI.Tests;

public class DependencyRuleTests
{
    private static readonly GCI0052_DependencyBotApiDrift Rule52 = new(new StubPatternProvider());
    private static readonly GCI0053_LockfileChangedWithoutSource Rule53 = new(new StubPatternProvider());

    private static DiffContext MakeDiff(params DiffFile[] files) =>
        new() { Files = [.. files] };

    private static DiffFile MakeFile(string path, params string[] addedLineContents)
    {
        var hunk = new DiffHunk
        {
            Lines = [.. addedLineContents.Select((content, i) => new DiffLine
            {
                Kind       = DiffLineKind.Added,
                LineNumber = i + 1,
                Content    = content,
            })]
        };
        return new DiffFile { NewPath = path, OldPath = path, Hunks = [hunk] };
    }

    // ── GCI0052 ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GCI0052_WhenDependabotActorAndPublicApiChange_Fires()
    {
        var prev = Environment.GetEnvironmentVariable("GITHUB_ACTOR");
        try
        {
            Environment.SetEnvironmentVariable("GITHUB_ACTOR", "dependabot[bot]");

            var diff = MakeDiff(
                MakeFile("packages.lock.json", "some lock content"),
                MakeFile("src/Foo.cs", "    public static string DoSomething(string input) {"));

            var findings = await Rule52.EvaluateAsync(diff, null);

            Assert.NotEmpty(findings);
            Assert.Contains(findings, f => f.Summary.Contains("public API change"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("GITHUB_ACTOR", prev);
        }
    }

    [Fact]
    public async Task GCI0052_WhenNoDependabotActor_DoesNotFire()
    {
        var prev = Environment.GetEnvironmentVariable("GITHUB_ACTOR");
        try
        {
            Environment.SetEnvironmentVariable("GITHUB_ACTOR", null);

            var diff = MakeDiff(
                MakeFile("packages.lock.json", "some lock content"),
                MakeFile("src/Foo.cs", "    public static string DoSomething(string input) {"));

            var findings = await Rule52.EvaluateAsync(diff, null);

            Assert.Empty(findings);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GITHUB_ACTOR", prev);
        }
    }

    [Fact]
    public async Task GCI0052_WhenDependabotActorButNoPublicApiChange_DoesNotFire()
    {
        var prev = Environment.GetEnvironmentVariable("GITHUB_ACTOR");
        try
        {
            Environment.SetEnvironmentVariable("GITHUB_ACTOR", "dependabot[bot]");

            var diff = MakeDiff(
                MakeFile("packages.lock.json", "some lock content"),
                MakeFile("src/Foo.cs", "    private string InternalHelper() {"));

            var findings = await Rule52.EvaluateAsync(diff, null);

            Assert.Empty(findings);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GITHUB_ACTOR", prev);
        }
    }

    [Fact]
    public async Task GCI0052_RenovateBotActorAlsoFires()
    {
        var prev = Environment.GetEnvironmentVariable("GITHUB_ACTOR");
        try
        {
            Environment.SetEnvironmentVariable("GITHUB_ACTOR", "renovate[bot]");

            var diff = MakeDiff(
                MakeFile("yarn.lock", "some lock content"),
                MakeFile("src/Bar.cs", "    public async Task<string> GetAsync(int id) {"));

            var findings = await Rule52.EvaluateAsync(diff, null);

            Assert.NotEmpty(findings);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GITHUB_ACTOR", prev);
        }
    }

    // ── GCI0053 ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GCI0053_WhenLockfileOnlyChange_Fires()
    {
        var diff = MakeDiff(MakeFile("yarn.lock", "some lock content"));

        var findings = await Rule53.EvaluateAsync(diff, null);

        Assert.NotEmpty(findings);
        Assert.Contains(findings, f => f.Summary.Contains("Lockfile modified"));
    }

    [Fact]
    public async Task GCI0053_WhenLockfileAndSourceChange_DoesNotFire()
    {
        var diff = MakeDiff(
            MakeFile("yarn.lock", "some lock content"),
            MakeFile("src/App.cs", "public class App {}"));

        var findings = await Rule53.EvaluateAsync(diff, null);

        Assert.Empty(findings);
    }

    [Fact]
    public async Task GCI0053_DotLockExtensionIsDetected()
    {
        var diff = MakeDiff(MakeFile("something.lock", "lock data"));

        var findings = await Rule53.EvaluateAsync(diff, null);

        Assert.NotEmpty(findings);
    }

    [Fact]
    public async Task GCI0053_NoLockfileChanges_DoesNotFire()
    {
        var diff = MakeDiff(MakeFile("src/Service.cs", "public class Service {}"));

        var findings = await Rule53.EvaluateAsync(diff, null);

        Assert.Empty(findings);
    }
}

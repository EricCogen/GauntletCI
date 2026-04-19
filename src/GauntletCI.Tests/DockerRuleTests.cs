// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;
using GauntletCI.Core.Rules.Implementations;

namespace GauntletCI.Tests;

public class DockerRuleTests
{
    private static readonly GCI0101_ExposedPortChanged  Rule101 = new();
    private static readonly GCI0102_BaseImageUpdated    Rule102 = new();
    private static readonly GCI0103_NewVolumeMount      Rule103 = new();
    private static readonly GCI0104_UserContextSwitched Rule104 = new();
    private static readonly GCI0105_HealthcheckAdded    Rule105 = new();

    // ── helpers ──────────────────────────────────────────────────────────────

    private static DiffContext MakeDiff(params DiffFile[] files) =>
        new() { Files = [.. files] };

    private static DiffFile MakeDockerfile(string path, string[] removed, string[] added, string[]? context = null)
    {
        var lines = new List<DiffLine>();
        int lineNum = 1;
        foreach (var r in removed)
            lines.Add(new DiffLine { Kind = DiffLineKind.Removed, Content = r, LineNumber = 0, OldLineNumber = lineNum++ });
        foreach (var a in added)
            lines.Add(new DiffLine { Kind = DiffLineKind.Added, Content = a, LineNumber = lineNum++, OldLineNumber = 0 });
        foreach (var c in context ?? [])
            lines.Add(new DiffLine { Kind = DiffLineKind.Context, Content = c, LineNumber = lineNum++, OldLineNumber = lineNum++ });

        var hunk = new DiffHunk { Lines = lines };
        return new DiffFile { NewPath = path, OldPath = path, Hunks = [hunk] };
    }

    private static DiffFile MakeDockerfileWithLines(string path, params DiffLine[] lines)
    {
        var hunk = new DiffHunk { Lines = [.. lines] };
        return new DiffFile { NewPath = path, OldPath = path, Hunks = [hunk] };
    }

    // ── GCI0101 ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GCI0101_PortChanged_Fires()
    {
        var diff = MakeDiff(MakeDockerfile("Dockerfile",
            removed: ["EXPOSE 8080"],
            added:   ["EXPOSE 9090"]));

        var findings = await Rule101.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.RuleId == "GCI0101" && f.Evidence.Contains("8080") && f.Evidence.Contains("9090"));
    }

    [Fact]
    public async Task GCI0101_SamePort_NoFire()
    {
        var diff = MakeDiff(MakeDockerfile("Dockerfile",
            removed: ["EXPOSE 8080"],
            added:   ["EXPOSE 8080"]));

        var findings = await Rule101.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.RuleId == "GCI0101");
    }

    [Fact]
    public async Task GCI0101_NonDockerfile_NoFire()
    {
        var diff = MakeDiff(MakeDockerfile("src/Program.cs",
            removed: ["EXPOSE 8080"],
            added:   ["EXPOSE 9090"]));

        var findings = await Rule101.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.RuleId == "GCI0101");
    }

    [Fact]
    public async Task GCI0101_DotDockerfileExtension_Fires()
    {
        var diff = MakeDiff(MakeDockerfile("build/app.dockerfile",
            removed: ["EXPOSE 3000"],
            added:   ["EXPOSE 3001"]));

        var findings = await Rule101.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.RuleId == "GCI0101");
    }

    // ── GCI0102 ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GCI0102_BaseImageChanged_Fires()
    {
        var diff = MakeDiff(MakeDockerfile("Dockerfile",
            removed: ["FROM node:18-alpine"],
            added:   ["FROM node:20-alpine"]));

        var findings = await Rule102.EvaluateAsync(diff, null);

        Assert.Contains(findings, f =>
            f.RuleId == "GCI0102" &&
            f.Evidence.Contains("node:18-alpine") &&
            f.Evidence.Contains("node:20-alpine"));
    }

    [Fact]
    public async Task GCI0102_NonDockerfile_NoFire()
    {
        var diff = MakeDiff(MakeDockerfile("src/build.sh",
            removed: ["FROM node:18-alpine"],
            added:   ["FROM node:20-alpine"]));

        var findings = await Rule102.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.RuleId == "GCI0102");
    }

    [Fact]
    public async Task GCI0102_OnlyAddedFrom_NoFire()
    {
        var diff = MakeDiff(MakeDockerfile("Dockerfile",
            removed: [],
            added:   ["FROM node:20-alpine"]));

        var findings = await Rule102.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.RuleId == "GCI0102");
    }

    // ── GCI0103 ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GCI0103_VolumeAdded_Fires()
    {
        var diff = MakeDiff(MakeDockerfile("Dockerfile",
            removed: [],
            added:   ["VOLUME /data"]));

        var findings = await Rule103.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.RuleId == "GCI0103" && f.Evidence.Contains("/data"));
    }

    [Fact]
    public async Task GCI0103_VolumeRemoved_NoFire()
    {
        var diff = MakeDiff(MakeDockerfile("Dockerfile",
            removed: ["VOLUME /data"],
            added:   []));

        var findings = await Rule103.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.RuleId == "GCI0103");
    }

    [Fact]
    public async Task GCI0103_NonDockerfile_NoFire()
    {
        var diff = MakeDiff(MakeDockerfile("config.yaml",
            removed: [],
            added:   ["VOLUME /data"]));

        var findings = await Rule103.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.RuleId == "GCI0103");
    }

    // ── GCI0104 ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GCI0104_UserChangedToRoot_FiresWithBlockSummary()
    {
        var diff = MakeDiff(MakeDockerfile("Dockerfile",
            removed: ["USER appuser"],
            added:   ["USER root"]));

        var findings = await Rule104.EvaluateAsync(diff, null);

        Assert.Contains(findings, f =>
            f.RuleId == "GCI0104" &&
            f.Summary.Contains("root"));
    }

    [Fact]
    public async Task GCI0104_UserChangedNonRoot_Fires()
    {
        var diff = MakeDiff(MakeDockerfile("Dockerfile",
            removed: ["USER appuser"],
            added:   ["USER serviceaccount"]));

        var findings = await Rule104.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.RuleId == "GCI0104");
    }

    [Fact]
    public async Task GCI0104_SameUser_NoFire()
    {
        var diff = MakeDiff(MakeDockerfile("Dockerfile",
            removed: ["USER appuser"],
            added:   ["USER appuser"]));

        var findings = await Rule104.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.RuleId == "GCI0104");
    }

    [Fact]
    public async Task GCI0104_NonDockerfile_NoFire()
    {
        var diff = MakeDiff(MakeDockerfile("src/entrypoint.sh",
            removed: ["USER appuser"],
            added:   ["USER root"]));

        var findings = await Rule104.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.RuleId == "GCI0104");
    }

    // ── GCI0105 ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GCI0105_HealthcheckAddedWithoutComment_Fires()
    {
        var diff = MakeDiff(MakeDockerfileWithLines("Dockerfile",
            new DiffLine { Kind = DiffLineKind.Added, Content = "RUN apt-get update", LineNumber = 1 },
            new DiffLine { Kind = DiffLineKind.Added, Content = "HEALTHCHECK CMD curl -f http://localhost/ || exit 1", LineNumber = 2 }));

        var findings = await Rule105.EvaluateAsync(diff, null);

        Assert.Contains(findings, f => f.RuleId == "GCI0105");
    }

    [Fact]
    public async Task GCI0105_HealthcheckWithCommentAbove_NoFire()
    {
        var diff = MakeDiff(MakeDockerfileWithLines("Dockerfile",
            new DiffLine { Kind = DiffLineKind.Added, Content = "# Check that the web server responds on port 80", LineNumber = 1 },
            new DiffLine { Kind = DiffLineKind.Added, Content = "HEALTHCHECK CMD curl -f http://localhost/ || exit 1", LineNumber = 2 }));

        var findings = await Rule105.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.RuleId == "GCI0105");
    }

    [Fact]
    public async Task GCI0105_NonDockerfile_NoFire()
    {
        var diff = MakeDiff(MakeDockerfileWithLines("src/init.sh",
            new DiffLine { Kind = DiffLineKind.Added, Content = "HEALTHCHECK CMD curl -f http://localhost/ || exit 1", LineNumber = 1 }));

        var findings = await Rule105.EvaluateAsync(diff, null);

        Assert.DoesNotContain(findings, f => f.RuleId == "GCI0105");
    }
}

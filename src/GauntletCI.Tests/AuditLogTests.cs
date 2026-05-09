// SPDX-License-Identifier: Elastic-2.0
using System.Text.Json;
using GauntletCI.Cli.Audit;

namespace GauntletCI.Tests;

public class AuditLogTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [Fact]
    public void AuditLogEntry_DefaultValues_AreCorrect()
    {
        var entry = new AuditLogEntry();

        Assert.True(Guid.TryParse(entry.ScanId, out _));
        var delta = DateTimeOffset.UtcNow - entry.Timestamp;
        Assert.True(delta.TotalSeconds < 5);
        Assert.Equal(string.Empty, entry.RepoPath);
        Assert.Equal(string.Empty, entry.CommitSha);
        Assert.Equal(string.Empty, entry.DiffSource);
        Assert.Equal(0, entry.FilesChanged);
        Assert.Equal(0, entry.FilesEligible);
        Assert.Equal(0, entry.RulesEvaluated);
        Assert.Equal(0, entry.FindingCount);
        Assert.NotNull(entry.Findings);
        Assert.Empty(entry.Findings);
    }

    [Fact]
    public void AuditFinding_DefaultValues_AreCorrect()
    {
        var finding = new AuditFinding();

        Assert.Equal(string.Empty, finding.RuleId);
        Assert.Equal(string.Empty, finding.RuleName);
        Assert.Equal(string.Empty, finding.Summary);
        Assert.Equal(string.Empty, finding.Confidence);
        Assert.Null(finding.FilePath);
        Assert.Null(finding.Line);
    }

    [Fact]
    public void AuditLogEntry_WithFindings_SerializesToJson()
    {
        var entry = new AuditLogEntry
        {
            ScanId = "test-scan-001",
            RepoPath = "/repo/test",
            CommitSha = "abc123",
            DiffSource = "staged",
            FilesChanged = 2,
            FindingCount = 2,
            Findings =
            [
                new AuditFinding { RuleId = "GCI0001", RuleName = "Diff Integrity", Summary = "Risk found", Confidence = "High" },
                new AuditFinding { RuleId = "GCI0002", RuleName = "Behavioral Change", Summary = "Guard added", Confidence = "Medium", FilePath = "src/Foo.cs", Line = 42 },
            ],
        };

        var json = JsonSerializer.Serialize(entry, JsonOpts);
        var deserialized = JsonSerializer.Deserialize<AuditLogEntry>(json, JsonOpts);

        Assert.NotNull(deserialized);
        Assert.Equal(entry.ScanId, deserialized.ScanId);
        Assert.Equal(entry.RepoPath, deserialized.RepoPath);
        Assert.Equal(entry.CommitSha, deserialized.CommitSha);
        Assert.Equal(entry.DiffSource, deserialized.DiffSource);
        Assert.Equal(entry.FilesChanged, deserialized.FilesChanged);
        Assert.Equal(entry.FindingCount, deserialized.FindingCount);
        Assert.Equal(2, deserialized.Findings.Count);
        Assert.Equal("GCI0001", deserialized.Findings[0].RuleId);
        Assert.Equal("GCI0002", deserialized.Findings[1].RuleId);
        Assert.Equal(42, deserialized.Findings[1].Line);
        Assert.Equal("src/Foo.cs", deserialized.Findings[1].FilePath);
    }

    [Fact]
    public async Task AuditLog_NdjsonFormat_CanBeReadManually()
    {
        var entry1 = new AuditLogEntry { ScanId = "scan-001", RepoPath = "/repo/a" };
        var entry2 = new AuditLogEntry { ScanId = "scan-002", RepoPath = "/repo/b" };

        var ndjson = JsonSerializer.Serialize(entry1, JsonOpts) + Environment.NewLine
                   + JsonSerializer.Serialize(entry2, JsonOpts) + Environment.NewLine;

        var tempFile = Path.Combine(Path.GetTempPath(), $"gauntletci-test-{Guid.NewGuid():N}.ndjson");
        try
        {
            await File.WriteAllTextAsync(tempFile, ndjson);

            var lines = await File.ReadAllLinesAsync(tempFile);
            var entries = lines
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(l => JsonSerializer.Deserialize<AuditLogEntry>(l, JsonOpts))
                .Where(e => e is not null)
                .ToList();

            Assert.Equal(2, entries.Count);
            Assert.Equal("scan-001", entries[0]!.ScanId);
            Assert.Equal("/repo/a", entries[0]!.RepoPath);
            Assert.Equal("scan-002", entries[1]!.ScanId);
            Assert.Equal("/repo/b", entries[1]!.RepoPath);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task AuditLog_NdjsonFormat_SkipsBlankLines()
    {
        var entry = new AuditLogEntry { ScanId = "scan-solo", RepoPath = "/repo/c" };

        // Embed blank lines to verify the reader skips them
        var ndjson = Environment.NewLine
                   + JsonSerializer.Serialize(entry, JsonOpts) + Environment.NewLine
                   + "   " + Environment.NewLine;

        var tempFile = Path.Combine(Path.GetTempPath(), $"gauntletci-test-{Guid.NewGuid():N}.ndjson");
        try
        {
            await File.WriteAllTextAsync(tempFile, ndjson);

            var lines = await File.ReadAllLinesAsync(tempFile);
            var entries = lines
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(l => JsonSerializer.Deserialize<AuditLogEntry>(l, JsonOpts))
                .Where(e => e is not null)
                .ToList();

            Assert.Single(entries);
            Assert.Equal("scan-solo", entries[0]!.ScanId);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}

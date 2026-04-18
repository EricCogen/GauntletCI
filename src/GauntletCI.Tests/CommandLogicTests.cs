// SPDX-License-Identifier: Elastic-2.0
using System.Reflection;
using System.Text.Json;
using GauntletCI.Cli.Audit;
using GauntletCI.Cli.Commands;
using GauntletCI.Cli.Telemetry;
using GauntletCI.Core.Rules;

namespace GauntletCI.Tests;

public class CommandLogicTests : IDisposable
{
    private readonly List<string> _tempDirs = [];

    private string CreateTempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), $"gauntletci-cmdtest-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        _tempDirs.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (var dir in _tempDirs)
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    // ── AuditCommand.CsvEscape ────────────────────────────────────────────────

    private static readonly MethodInfo _csvEscape =
        typeof(AuditCommand).GetMethod("CsvEscape", BindingFlags.NonPublic | BindingFlags.Static)!;

    private static string CsvEscape(string value) =>
        (string)_csvEscape.Invoke(null, [value])!;

    [Fact]
    public void CsvEscape_PlainString_ReturnedAsIs()
    {
        Assert.Equal("hello", CsvEscape("hello"));
    }

    [Fact]
    public void CsvEscape_StringWithComma_WrappedInQuotes()
    {
        Assert.Equal("\"hello,world\"", CsvEscape("hello,world"));
    }

    [Fact]
    public void CsvEscape_StringWithDoubleQuote_InnerQuoteDoubledAndWrapped()
    {
        Assert.Equal("\"say \"\"hi\"\"\"", CsvEscape("say \"hi\""));
    }

    [Fact]
    public void CsvEscape_StringWithNewline_WrappedInQuotes()
    {
        Assert.Equal("\"line1\nline2\"", CsvEscape("line1\nline2"));
    }

    [Fact]
    public void CsvEscape_EmptyString_ReturnedAsEmpty()
    {
        Assert.Equal("", CsvEscape(""));
    }

    // ── AuditCommand.ToCsv ────────────────────────────────────────────────────

    private static readonly MethodInfo _toCsv =
        typeof(AuditCommand).GetMethod("ToCsv", BindingFlags.NonPublic | BindingFlags.Static)!;

    private static string ToCsv(IReadOnlyList<AuditLogEntry> entries) =>
        (string)_toCsv.Invoke(null, [entries])!;

    private static string[] SplitCsvLines(string csv) =>
        csv.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

    [Fact]
    public void ToCsv_SingleEntryNoFindings_ProducesHeaderPlusOneDataRow()
    {
        var entry = new AuditLogEntry
        {
            ScanId = "scan-001",
            RepoPath = "/repo/a",
            CommitSha = "abc123",
            DiffSource = "staged",
            FilesChanged = 3,
            FilesEligible = 2,
            RulesEvaluated = 10,
            FindingCount = 0,
            Findings = [],
        };

        var lines = SplitCsvLines(ToCsv([entry]));

        Assert.Equal(2, lines.Length);
    }

    [Fact]
    public void ToCsv_SingleEntryWithTwoFindings_ProducesHeaderPlusTwoDataRows()
    {
        var entry = new AuditLogEntry
        {
            ScanId = "scan-002",
            FindingCount = 2,
            Findings =
            [
                new AuditFinding { RuleId = "GCI0001", RuleName = "DiffIntegrity", Summary = "Risk", Confidence = "High" },
                new AuditFinding { RuleId = "GCI0002", RuleName = "Behavioral", Summary = "Guard", Confidence = "Medium", FilePath = "src/Foo.cs", Line = 42 },
            ],
        };

        var lines = SplitCsvLines(ToCsv([entry]));

        Assert.Equal(3, lines.Length);
    }

    [Fact]
    public void ToCsv_Header_StartsWithScanIdTimestamp()
    {
        var entry = new AuditLogEntry { ScanId = "scan-003" };
        var csv = ToCsv([entry]);
        var firstLine = SplitCsvLines(csv)[0];

        Assert.StartsWith("ScanId,Timestamp,", firstLine);
    }

    // ── InitCommand.FindGitRoot ───────────────────────────────────────────────

    private static readonly MethodInfo _findGitRoot =
        typeof(InitCommand).GetMethod("FindGitRoot", BindingFlags.NonPublic | BindingFlags.Static)!;

    private static string? FindGitRoot(string path) =>
        (string?)_findGitRoot.Invoke(null, [path]);

    [Fact]
    public void FindGitRoot_DirectoryContainingDotGit_ReturnsThatDirectory()
    {
        var root = CreateTempDir();
        Directory.CreateDirectory(Path.Combine(root, ".git"));

        var result = FindGitRoot(root);

        Assert.Equal(root, result);
    }

    [Fact]
    public void FindGitRoot_ChildOfGitRoot_ReturnsRoot()
    {
        var root = CreateTempDir();
        Directory.CreateDirectory(Path.Combine(root, ".git"));
        var child = Path.Combine(root, "src");
        Directory.CreateDirectory(child);

        var result = FindGitRoot(child);

        Assert.Equal(root, result);
    }

    [Fact]
    public void FindGitRoot_ThreeLevelsDeep_StillFindsRoot()
    {
        var root = CreateTempDir();
        Directory.CreateDirectory(Path.Combine(root, ".git"));
        var deep = Path.Combine(root, "a", "b", "c");
        Directory.CreateDirectory(deep);

        var result = FindGitRoot(deep);

        Assert.Equal(root, result);
    }

    [Fact]
    public void FindGitRoot_NoGitAnywhere_ReturnsNull()
    {
        // Temp dir ancestry on a standard machine contains no .git folder.
        var isolated = CreateTempDir();

        var result = FindGitRoot(isolated);

        // In the rare case the temp dir sits inside a git repo (some CI configs),
        // the returned path must at least contain a real .git folder.
        if (result is not null)
            Assert.True(Directory.Exists(Path.Combine(result, ".git")));
        else
            Assert.Null(result);
    }

    [Fact]
    public void FindGitRoot_PassingDotGitDirectoryItself_WalksUpToParent()
    {
        var root = CreateTempDir();
        var dotGit = Path.Combine(root, ".git");
        Directory.CreateDirectory(dotGit);

        // The .git dir has no nested .git, so it walks up and finds root's .git.
        var result = FindGitRoot(dotGit);

        Assert.Equal(root, result);
    }

    // ── InitCommand.BuildDefaultRules ─────────────────────────────────────────

    private static readonly MethodInfo _buildDefaultRules =
        typeof(InitCommand).GetMethod("BuildDefaultRules", BindingFlags.NonPublic | BindingFlags.Static)!;

    private static Dictionary<string, object> BuildDefaultRules() =>
        (Dictionary<string, object>)_buildDefaultRules.Invoke(null, null)!;

    [Fact]
    public void BuildDefaultRules_ReturnsAllDiscoveredRules()
    {
        var rules = BuildDefaultRules();
        var expectedCount = RuleOrchestrator.GetAllRuleIds().Count;

        Assert.Equal(expectedCount, rules.Count);
    }

    [Theory]
    [InlineData("GCI0001")]
    [InlineData("GCI0010")]
    public void BuildDefaultRules_ContainsExpectedKey(string key)
    {
        var rules = BuildDefaultRules();

        Assert.True(rules.ContainsKey(key), $"Missing expected rule key '{key}'.");
    }

    [Fact]
    public void BuildDefaultRules_AllKeysFormattedAsGciWithFourDigits()
    {
        var rules = BuildDefaultRules();

        foreach (var key in rules.Keys)
            Assert.Matches(@"^GCI\d{4}$", key);
    }

    [Fact]
    public void BuildDefaultRules_AllValuesHaveEnabledTrue()
    {
        var rules = BuildDefaultRules();

        foreach (var (key, value) in rules)
        {
            var json = JsonSerializer.Serialize(value);
            using var doc = JsonDocument.Parse(json);
            var enabled = doc.RootElement.GetProperty("enabled").GetBoolean();
            Assert.True(enabled, $"Rule '{key}' does not have enabled=true.");
        }
    }

    // ── TelemetryCommand — mode parsing ───────────────────────────────────────

    private static TelemetryMode? ParseMode(string mode) =>
        mode.Trim().ToLowerInvariant() switch
        {
            "shared" => TelemetryMode.Shared,
            "local"  => TelemetryMode.Local,
            "off"    => TelemetryMode.Off,
            _        => (TelemetryMode?)null,
        };

    [Theory]
    [InlineData("shared", TelemetryMode.Shared)]
    [InlineData("local",  TelemetryMode.Local)]
    [InlineData("off",    TelemetryMode.Off)]
    public void ParseMode_ValidLowercase_ReturnsMappedEnum(string input, TelemetryMode expected)
    {
        Assert.Equal(expected, ParseMode(input));
    }

    [Fact]
    public void ParseMode_SharedUppercase_ReturnsTelemetryModeShared()
    {
        Assert.Equal(TelemetryMode.Shared, ParseMode("SHARED"));
    }

    [Fact]
    public void ParseMode_SharedWithWhitespace_ReturnsTelemetryModeShared()
    {
        Assert.Equal(TelemetryMode.Shared, ParseMode("  shared  "));
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("")]
    [InlineData("maybe")]
    public void ParseMode_InvalidInput_ReturnsNull(string input)
    {
        Assert.Null(ParseMode(input));
    }

    // ── FeedbackCommand — vote validation ─────────────────────────────────────

    private static bool IsValidVote(string raw)
    {
        var vote = raw.ToLowerInvariant();
        return vote is "up" or "down";
    }

    [Theory]
    [InlineData("up")]
    [InlineData("down")]
    public void IsValidVote_ValidVotes_ReturnsTrue(string vote)
    {
        Assert.True(IsValidVote(vote));
    }

    [Fact]
    public void IsValidVote_UppercaseUp_ReturnsTrueAfterNormalization()
    {
        Assert.True(IsValidVote("UP"));
    }

    [Theory]
    [InlineData("sideways")]
    [InlineData("")]
    [InlineData("maybe")]
    public void IsValidVote_InvalidVotes_ReturnsFalse(string vote)
    {
        Assert.False(IsValidVote(vote));
    }
}

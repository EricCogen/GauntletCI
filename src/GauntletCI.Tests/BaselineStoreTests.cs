// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Cli.Baseline;
using GauntletCI.Core.Model;

namespace GauntletCI.Tests;

public class BaselineStoreTests
{
    private static Finding MakeFinding(string ruleId = "GCI0001", string? filePath = "src/Foo.cs", string evidence = "Line 10: foo") =>
        new()
        {
            RuleId         = ruleId,
            RuleName       = "Test Rule",
            Summary        = "Test summary",
            Evidence       = evidence,
            WhyItMatters   = "Why",
            SuggestedAction = "Action",
            FilePath       = filePath,
        };

    [Fact]
    public void ComputeFingerprint_IsDeterministic()
    {
        var f = MakeFinding();
        Assert.Equal(BaselineStore.ComputeFingerprint(f), BaselineStore.ComputeFingerprint(f));
    }

    [Fact]
    public void ComputeFingerprint_DiffersForDifferentRuleId()
    {
        var f1 = MakeFinding(ruleId: "GCI0001");
        var f2 = MakeFinding(ruleId: "GCI0002");
        Assert.NotEqual(BaselineStore.ComputeFingerprint(f1), BaselineStore.ComputeFingerprint(f2));
    }

    [Fact]
    public void ComputeFingerprint_DiffersForDifferentEvidence()
    {
        var f1 = MakeFinding(evidence: "Line 10: foo");
        var f2 = MakeFinding(evidence: "Line 11: bar");
        Assert.NotEqual(BaselineStore.ComputeFingerprint(f1), BaselineStore.ComputeFingerprint(f2));
    }

    [Fact]
    public void ComputeFingerprint_NullFilePath_DoesNotThrow()
    {
        var f = MakeFinding(filePath: null);
        var fp = BaselineStore.ComputeFingerprint(f);
        Assert.False(string.IsNullOrEmpty(fp));
    }

    [Fact]
    public void SaveAndLoad_RoundTrip()
    {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        try
        {
            var fingerprints = new[] { "aabbcc", "ddeeff" };
            BaselineStore.Save(dir, fingerprints, commit: "abc123");

            var loaded = BaselineStore.Load(dir);

            Assert.NotNull(loaded);
            Assert.Equal(1, loaded.Version);
            Assert.Equal("abc123", loaded.Commit);
            Assert.Contains("aabbcc", loaded.Fingerprints);
            Assert.Contains("ddeeff", loaded.Fingerprints);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Load_ReturnsNull_WhenFileAbsent()
    {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        try
        {
            Assert.Null(BaselineStore.Load(dir));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Clear_DeletesFile_ReturnsTrue()
    {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        try
        {
            BaselineStore.Save(dir, ["aabbcc"]);
            Assert.True(BaselineStore.Clear(dir));
            Assert.Null(BaselineStore.Load(dir));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Clear_ReturnsFalse_WhenNoFile()
    {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        try
        {
            Assert.False(BaselineStore.Clear(dir));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}

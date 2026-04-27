// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Corpus.Labeling;
using Xunit;

namespace GauntletCI.Tests.Corpus;

public sealed class DiffEntropyEnricherTests
{
    // ── single file ───────────────────────────────────────────────────────────

    [Fact]
    public void ComputeMetrics_SingleFile_EntropyZeroNormalizedZero()
    {
        var diff = new[]
        {
            "diff --git a/src/Foo/Bar.cs b/src/Foo/Bar.cs",
            "@@ -1,3 +1,5 @@",
            "+    public void Foo() {}",
            "+    public void Bar() {}",
        };

        var m = DiffEntropyEnricher.ComputeMetrics(diff);

        Assert.Equal(1, m.FileCount);
        Assert.Equal(0.0, m.ChangeEntropy,      precision: 10);
        Assert.Equal(0.0, m.NormalizedEntropy,  precision: 10);
    }

    // ── two files equal changes -> entropy = 1 bit ───────────────────────────

    [Fact]
    public void ComputeMetrics_TwoFilesEqualChanges_Entropy1BitNormalized1()
    {
        var diff = new[]
        {
            "diff --git a/src/A.cs b/src/A.cs",
            "@@ -1,3 +1,4 @@",
            "+line",
            "diff --git a/src/B.cs b/src/B.cs",
            "@@ -1,3 +1,4 @@",
            "+line",
        };

        var m = DiffEntropyEnricher.ComputeMetrics(diff);

        Assert.Equal(2, m.FileCount);
        Assert.Equal(1.0, m.ChangeEntropy,      precision: 10);
        Assert.Equal(1.0, m.NormalizedEntropy,  precision: 10);
    }

    // ── two files, one file has all changes -> entropy = 0 ───────────────────

    [Fact]
    public void ComputeMetrics_TwoFilesOneHasAllChanges_EntropyZero()
    {
        var diff = new[]
        {
            "diff --git a/src/A.cs b/src/A.cs",
            "@@ -1,3 +1,5 @@",
            "+line1",
            "+line2",
            "+line3",
            "diff --git a/src/B.cs b/src/B.cs",
            // B has no added/removed lines - just file header
        };

        var m = DiffEntropyEnricher.ComputeMetrics(diff);

        // B has 0 lines changed, entropy contribution is 0
        Assert.Equal(0.0, m.ChangeEntropy,     precision: 10);
        Assert.Equal(0.0, m.NormalizedEntropy, precision: 10);
    }

    // ── four files equal changes -> entropy = 2 bits ─────────────────────────

    [Fact]
    public void ComputeMetrics_FourFilesEqualChanges_Entropy2BitsNormalized1()
    {
        var diff = new[]
        {
            "diff --git a/src/A.cs b/src/A.cs",
            "@@ -1,3 +1,4 @@",
            "+line",
            "diff --git a/src/B.cs b/src/B.cs",
            "@@ -1,3 +1,4 @@",
            "+line",
            "diff --git a/src/C.cs b/src/C.cs",
            "@@ -1,3 +1,4 @@",
            "+line",
            "diff --git a/src/D.cs b/src/D.cs",
            "@@ -1,3 +1,4 @@",
            "+line",
        };

        var m = DiffEntropyEnricher.ComputeMetrics(diff);

        Assert.Equal(4, m.FileCount);
        Assert.Equal(2.0, m.ChangeEntropy,     precision: 10);
        Assert.Equal(1.0, m.NormalizedEntropy, precision: 10);
    }

    // ── directory count ───────────────────────────────────────────────────────

    [Fact]
    public void CountDistinctDirectories_TwoFilesInDifferentDirs_Returns2()
    {
        var paths = new[] { "src/Foo/A.cs", "src/Bar/B.cs" };
        Assert.Equal(2, DiffEntropyEnricher.CountDistinctDirectories(paths));
    }

    [Fact]
    public void CountDistinctDirectories_TwoFilesInSameDir_Returns1()
    {
        var paths = new[] { "src/Foo/A.cs", "src/Foo/B.cs" };
        Assert.Equal(1, DiffEntropyEnricher.CountDistinctDirectories(paths));
    }

    [Fact]
    public void CountDistinctDirectories_FileAtRoot_Returns1()
    {
        var paths = new[] { "README.md" };
        Assert.Equal(1, DiffEntropyEnricher.CountDistinctDirectories(paths));
    }

    // ── namespace count (first two path segments) ─────────────────────────────

    [Fact]
    public void CountDistinctNamespaces_SameTopTwoSegments_Returns1()
    {
        var paths = new[] { "src/MyApp/Foo/A.cs", "src/MyApp/Bar/B.cs" };
        Assert.Equal(1, DiffEntropyEnricher.CountDistinctNamespaces(paths));
    }

    [Fact]
    public void CountDistinctNamespaces_DifferentTopTwoSegments_Returns2()
    {
        var paths = new[] { "src/MyApp/A.cs", "tests/MyApp/B.cs" };
        Assert.Equal(2, DiffEntropyEnricher.CountDistinctNamespaces(paths));
    }

    [Fact]
    public void ExtractNamespace_DeepPath_ReturnsFirstTwoSegments()
    {
        Assert.Equal("src/MyApp", DiffEntropyEnricher.ExtractNamespace("src/MyApp/Foo/Bar.cs"));
    }

    [Fact]
    public void ExtractNamespace_SingleSegment_ReturnsThatSegment()
    {
        Assert.Equal("file.cs", DiffEntropyEnricher.ExtractNamespace("file.cs"));
    }

    // ── deleted lines counted ─────────────────────────────────────────────────

    [Fact]
    public void ComputeMetrics_DeletedLines_CountedInChangedLines()
    {
        var diff = new[]
        {
            "diff --git a/src/A.cs b/src/A.cs",
            "@@ -1,3 +1,2 @@",
            "-removed line",
        };

        var m = DiffEntropyEnricher.ComputeMetrics(diff);

        Assert.Equal(1, m.TotalLinesChanged);
    }

    // ── empty diff ────────────────────────────────────────────────────────────

    [Fact]
    public void ComputeMetrics_EmptyDiff_AllZeros()
    {
        var m = DiffEntropyEnricher.ComputeMetrics([]);

        Assert.Equal(0, m.FileCount);
        Assert.Equal(0.0, m.ChangeEntropy);
        Assert.Equal(0.0, m.NormalizedEntropy);
    }
}

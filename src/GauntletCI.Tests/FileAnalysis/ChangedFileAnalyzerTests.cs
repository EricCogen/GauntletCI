// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.FileAnalysis;

namespace GauntletCI.Tests.FileAnalysis;

public class ChangedFileAnalyzerTests
{
    private static readonly ChangedFileAnalyzer Analyzer = new();

    private static DiffFile MakeFile(
        string newPath,
        string oldPath = "",
        bool isDeleted = false,
        bool hasHunks  = true)
    {
        var file = new DiffFile
        {
            NewPath   = newPath,
            OldPath   = oldPath,
            IsAdded   = false,
            IsDeleted = isDeleted,
        };
        if (hasHunks)
            file.Hunks.Add(new DiffHunk { Lines = [new DiffLine { Kind = DiffLineKind.Added, Content = "// change" }] });
        return file;
    }

    // ── 1. .cs file is eligible ──────────────────────────────────────────────

    [Fact]
    public void CsFile_IsEligible_EligibleSource()
    {
        var record = Analyzer.Analyze(MakeFile("src/OrderService.cs"));

        Assert.True(record.IsEligible);
        Assert.Equal(FileEligibilityClassification.EligibleSource, record.Classification);
        Assert.Equal(".cs", record.Extension);
        Assert.Contains(".cs is allowed", record.Reason, StringComparison.OrdinalIgnoreCase);
    }

    // ── 2. .md file is KnownNonSource and skipped ────────────────────────────

    [Fact]
    public void MdFile_IsKnownNonSource_Skipped()
    {
        var record = Analyzer.Analyze(MakeFile("docs/readme.md"));

        Assert.False(record.IsEligible);
        Assert.Equal(FileEligibilityClassification.KnownNonSource, record.Classification);
        Assert.Contains("non-source", record.Reason, StringComparison.OrdinalIgnoreCase);
    }

    // ── 3. .svg file is KnownNonSource and skipped ───────────────────────────

    [Fact]
    public void SvgFile_IsKnownNonSource_Skipped()
    {
        var record = Analyzer.Analyze(MakeFile("assets/logo.svg"));

        Assert.False(record.IsEligible);
        Assert.Equal(FileEligibilityClassification.KnownNonSource, record.Classification);
    }

    // ── 4. .prefab file is UnknownUnsupported and skipped ───────────────────

    [Fact]
    public void PrefabFile_IsUnknownUnsupported_Skipped()
    {
        var record = Analyzer.Analyze(MakeFile("Assets/Player.prefab"));

        Assert.False(record.IsEligible);
        Assert.Equal(FileEligibilityClassification.UnknownUnsupported, record.Classification);
        Assert.Contains("not supported", record.Reason, StringComparison.OrdinalIgnoreCase);
    }

    // ── 5. deleted .cs file is Deleted and skipped ──────────────────────────

    [Fact]
    public void DeletedCsFile_IsDeleted_Skipped()
    {
        var record = Analyzer.Analyze(MakeFile("src/OldService.cs", isDeleted: true));

        Assert.False(record.IsEligible);
        Assert.Equal(FileEligibilityClassification.Deleted, record.Classification);
        Assert.True(record.IsDeleted);
    }

    // ── 6. rename-only .cs file is RenamedOnly and skipped ──────────────────

    [Fact]
    public void RenameOnlyCsFile_IsRenamedOnly_Skipped()
    {
        var file = new DiffFile
        {
            NewPath   = "src/NewName.cs",
            OldPath   = "src/OldName.cs",
            IsAdded   = false,
            IsDeleted = false,
            // no hunks → no content changes
        };

        var record = Analyzer.Analyze(file);

        Assert.False(record.IsEligible);
        Assert.Equal(FileEligibilityClassification.RenamedOnly, record.Classification);
        Assert.True(record.IsRename);
        Assert.False(record.HasContentChanges);
    }

    // ── 7. empty path is EmptyPath and skipped ───────────────────────────────

    [Fact]
    public void EmptyPath_IsEmptyPath_Skipped()
    {
        var file = new DiffFile { NewPath = string.Empty, OldPath = string.Empty };

        var record = Analyzer.Analyze(file);

        Assert.False(record.IsEligible);
        Assert.Equal(FileEligibilityClassification.EmptyPath, record.Classification);
    }

    // ── 8. no-extension file is MissingExtension and skipped ────────────────

    [Fact]
    public void FileWithNoExtension_IsMissingExtension_Skipped()
    {
        var record = Analyzer.Analyze(MakeFile("Makefile"));

        Assert.False(record.IsEligible);
        Assert.Equal(FileEligibilityClassification.MissingExtension, record.Classification);
    }

    // ── 9. .designer.cs file is Generated and skipped ───────────────────────

    [Fact]
    public void DesignerCsFile_IsGenerated_Skipped()
    {
        var record = Analyzer.Analyze(MakeFile("src/Form1.designer.cs"));

        Assert.False(record.IsEligible);
        Assert.Equal(FileEligibilityClassification.Generated, record.Classification);
        Assert.True(record.IsGenerated);
    }

    [Fact]
    public void GeneratedCsFile_IsGenerated_Skipped()
    {
        var record = Analyzer.Analyze(MakeFile("src/Foo.generated.cs"));

        Assert.False(record.IsEligible);
        Assert.Equal(FileEligibilityClassification.Generated, record.Classification);
    }

    [Fact]
    public void GCsFile_IsGenerated_Skipped()
    {
        var record = Analyzer.Analyze(MakeFile("src/Foo.g.cs"));

        Assert.False(record.IsEligible);
        Assert.Equal(FileEligibilityClassification.Generated, record.Classification);
    }

    // ── 10. statistics summary counts are correct for mixed input set ────────

    [Fact]
    public void Statistics_MixedInputSet_CountsAreCorrect()
    {
        var files = new[]
        {
            MakeFile("src/OrderService.cs"),          // EligibleSource
            MakeFile("src/PaymentService.cs"),         // EligibleSource
            MakeFile("docs/readme.md"),                // KnownNonSource
            MakeFile("assets/logo.svg"),               // KnownNonSource
            MakeFile("Assets/Player.prefab"),          // UnknownUnsupported
            MakeFile("src/OldService.cs", isDeleted: true),  // Deleted
            MakeFile("src/Form1.designer.cs"),         // Generated
            MakeFile("Makefile"),                      // MissingExtension
        };

        var records = files.Select(f => Analyzer.Analyze(f)).ToList();
        var stats   = FileEligibilityStatistics.From(records);

        Assert.Equal(8, stats.TotalFiles);
        Assert.Equal(2, stats.EligibleFiles);
        Assert.Equal(6, stats.SkippedFiles);
        Assert.Equal(2, stats.EligibleSourceCount);
        Assert.Equal(2, stats.KnownNonSourceCount);
        Assert.Equal(1, stats.UnknownUnsupportedCount);
        Assert.Equal(1, stats.DeletedCount);
        Assert.Equal(1, stats.GeneratedCount);
        Assert.Equal(1, stats.MissingExtensionCount);
    }

    // ── Rename with content changes is eligible ───────────────────────────────

    [Fact]
    public void RenameWithContentChanges_CsFile_IsEligible()
    {
        var file = new DiffFile
        {
            NewPath   = "src/NewName.cs",
            OldPath   = "src/OldName.cs",
            IsAdded   = false,
            IsDeleted = false,
        };
        file.Hunks.Add(new DiffHunk { Lines = [new DiffLine { Kind = DiffLineKind.Added, Content = "// change" }] });

        var record = Analyzer.Analyze(file);

        Assert.True(record.IsEligible);
        Assert.Equal(FileEligibilityClassification.EligibleSource, record.Classification);
    }
}

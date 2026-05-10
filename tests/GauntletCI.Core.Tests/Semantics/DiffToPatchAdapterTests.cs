// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Core.Tests.Semantics;

using Xunit;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Semantics;

public class DiffToPatchAdapterTests
{
    [Fact]
    public void FromDiffContext_EmptyDiff_CreatesPatchWithNoFiles()
    {
        // Arrange
        var diffContext = new DiffContext
        {
            RawDiff = "",
            CommitSha = "abc123",
            Files = []
        };

        // Act
        var patch = DiffToPatchAdapter.FromDiffContext(diffContext);

        // Assert
        Assert.Empty(patch.Files);
        Assert.Equal("abc123", patch.CommitSha);
        Assert.Equal("git diff", patch.Source);
    }

    [Fact]
    public void FromDiffContext_WithCustomSource_PreservesSource()
    {
        // Arrange
        var diffContext = new DiffContext();

        // Act
        var patch = DiffToPatchAdapter.FromDiffContext(diffContext, "github pr");

        // Assert
        Assert.Equal("github pr", patch.Source);
    }

    [Fact]
    public void FromDiffContext_SingleModifiedFile_ConvertsProperly()
    {
        // Arrange
        var diffContext = new DiffContext
        {
            RawDiff = "diff --git a/src/App.cs b/src/App.cs",
            CommitSha = "def456",
            Files =
            [
                new DiffFile
                {
                    OldPath = "src/App.cs",
                    NewPath = "src/App.cs",
                    IsAdded = false,
                    IsDeleted = false,
                    Hunks =
                    [
                        new DiffHunk
                        {
                            OldStartLine = 10,
                            NewStartLine = 10,
                            Lines =
                            [
                                new DiffLine
                                {
                                    Kind = DiffLineKind.Context,
                                    LineNumber = 9,
                                    OldLineNumber = 9,
                                    Content = "public class App"
                                },
                                new DiffLine
                                {
                                    Kind = DiffLineKind.Removed,
                                    LineNumber = 0,
                                    OldLineNumber = 10,
                                    Content = "    public void OldMethod() { }"
                                },
                                new DiffLine
                                {
                                    Kind = DiffLineKind.Added,
                                    LineNumber = 10,
                                    OldLineNumber = 0,
                                    Content = "    public void NewMethod() { }"
                                }
                            ]
                        }
                    ]
                }
            ]
        };

        // Act
        var patch = DiffToPatchAdapter.FromDiffContext(diffContext);

        // Assert
        Assert.Single(patch.Files);
        var file = patch.Files[0];
        Assert.Equal("src/App.cs", file.NewPath);
        Assert.Equal(PatchFileChangeKind.Modified, file.ChangeKind);
        Assert.Equal(".cs", file.FileExtension);
        Assert.True(file.IsProductionFile);
        Assert.False(file.IsTestFile);

        Assert.Single(file.Hunks);
        var hunk = file.Hunks[0];
        Assert.Equal(3, hunk.Lines.Count);
        Assert.Single(hunk.Lines, l => l.Kind == PatchLineKind.Added);
        Assert.Single(hunk.Lines, l => l.Kind == PatchLineKind.Removed);
    }

    [Fact]
    public void FromDiffContext_AddedFile_DetectsAddedKind()
    {
        // Arrange
        var diffContext = new DiffContext
        {
            Files =
            [
                new DiffFile
                {
                    OldPath = "",
                    NewPath = "src/NewFile.cs",
                    IsAdded = true,
                    IsDeleted = false,
                    Hunks = []
                }
            ]
        };

        // Act
        var patch = DiffToPatchAdapter.FromDiffContext(diffContext);

        // Assert
        Assert.Equal(PatchFileChangeKind.Added, patch.Files[0].ChangeKind);
    }

    [Fact]
    public void FromDiffContext_DeletedFile_DetectsDeletedKind()
    {
        // Arrange
        var diffContext = new DiffContext
        {
            Files =
            [
                new DiffFile
                {
                    OldPath = "src/OldFile.cs",
                    NewPath = "",
                    IsAdded = false,
                    IsDeleted = true,
                    Hunks = []
                }
            ]
        };

        // Act
        var patch = DiffToPatchAdapter.FromDiffContext(diffContext);

        // Assert
        Assert.Equal(PatchFileChangeKind.Deleted, patch.Files[0].ChangeKind);
    }

    [Fact]
    public void FromDiffContext_RenamedFile_DetectsRenamedKind()
    {
        // Arrange
        var diffContext = new DiffContext
        {
            Files =
            [
                new DiffFile
                {
                    OldPath = "src/OldName.cs",
                    NewPath = "src/NewName.cs",
                    IsAdded = false,
                    IsDeleted = false,
                    Hunks = []
                }
            ]
        };

        // Act
        var patch = DiffToPatchAdapter.FromDiffContext(diffContext);

        // Assert
        Assert.Equal(PatchFileChangeKind.Renamed, patch.Files[0].ChangeKind);
    }

    [Fact]
    public void FromDiffContext_TestFile_DetectsAsTestFile()
    {
        // Arrange
        var diffContext = new DiffContext
        {
            Files =
            [
                new DiffFile
                {
                    OldPath = "",
                    NewPath = "src/AppTests.cs",
                    IsAdded = true,
                    IsDeleted = false,
                    Hunks = []
                }
            ]
        };

        // Act
        var patch = DiffToPatchAdapter.FromDiffContext(diffContext);

        // Assert
        Assert.True(patch.Files[0].IsTestFile);
        Assert.False(patch.Files[0].IsProductionFile);
    }

    [Fact]
    public void FromDiffContext_MultipleFiles_ConvertsAll()
    {
        // Arrange
        var diffContext = new DiffContext
        {
            Files =
            [
                new DiffFile
                {
                    OldPath = "src/App.cs",
                    NewPath = "src/App.cs",
                    IsAdded = false,
                    IsDeleted = false,
                    Hunks = []
                },
                new DiffFile
                {
                    OldPath = "",
                    NewPath = "src/AppTests.cs",
                    IsAdded = true,
                    IsDeleted = false,
                    Hunks = []
                }
            ]
        };

        // Act
        var patch = DiffToPatchAdapter.FromDiffContext(diffContext);

        // Assert
        Assert.Equal(2, patch.Files.Count);
        Assert.Equal(PatchFileChangeKind.Modified, patch.Files[0].ChangeKind);
        Assert.Equal(PatchFileChangeKind.Added, patch.Files[1].ChangeKind);
    }
}

public class PatchModelTests
{
    [Fact]
    public void CountAddedLines_EmptyPatch_ReturnsZero()
    {
        // Arrange
        var patch = new PatchModel { Files = [] };

        // Act
        var count = patch.CountAddedLines();

        // Assert
        Assert.Equal(0, count);
    }

    [Fact]
    public void CountAddedLines_WithAddedLines_CountsCorrectly()
    {
        // Arrange
        var patch = new PatchModel
        {
            Files =
            [
                new PatchFile
                {
                    NewPath = "src/App.cs",
                    ChangeKind = PatchFileChangeKind.Modified,
                    Hunks =
                    [
                        new PatchHunk
                        {
                            Header = "@@ -1 +1 @@",
                            Lines =
                            [
                                new PatchLine { Kind = PatchLineKind.Added, Text = "line1" },
                                new PatchLine { Kind = PatchLineKind.Added, Text = "line2" },
                                new PatchLine { Kind = PatchLineKind.Removed, Text = "line3" }
                            ]
                        }
                    ]
                }
            ]
        };

        // Act
        var count = patch.CountAddedLines();

        // Assert
        Assert.Equal(2, count);
    }

    [Fact]
    public void CountRemovedLines_WithRemovedLines_CountsCorrectly()
    {
        // Arrange
        var patch = new PatchModel
        {
            Files =
            [
                new PatchFile
                {
                    NewPath = "src/App.cs",
                    ChangeKind = PatchFileChangeKind.Modified,
                    Hunks =
                    [
                        new PatchHunk
                        {
                            Header = "@@ -1 +1 @@",
                            Lines =
                            [
                                new PatchLine { Kind = PatchLineKind.Removed, Text = "line1" },
                                new PatchLine { Kind = PatchLineKind.Removed, Text = "line2" }
                            ]
                        }
                    ]
                }
            ]
        };

        // Act
        var count = patch.CountRemovedLines();

        // Assert
        Assert.Equal(2, count);
    }

    [Fact]
    public void CountContextLines_WithContextLines_CountsCorrectly()
    {
        // Arrange
        var patch = new PatchModel
        {
            Files =
            [
                new PatchFile
                {
                    NewPath = "src/App.cs",
                    ChangeKind = PatchFileChangeKind.Modified,
                    Hunks =
                    [
                        new PatchHunk
                        {
                            Header = "@@ -1 +1 @@",
                            Lines =
                            [
                                new PatchLine { Kind = PatchLineKind.Context, Text = "line1" },
                                new PatchLine { Kind = PatchLineKind.Context, Text = "line2" }
                            ]
                        }
                    ]
                }
            ]
        };

        // Act
        var count = patch.CountContextLines();

        // Assert
        Assert.Equal(2, count);
    }

    [Fact]
    public void GetAllAddedLines_MultipleFiles_ReturnsAllAdded()
    {
        // Arrange
        var patch = new PatchModel
        {
            Files =
            [
                new PatchFile
                {
                    NewPath = "file1.cs",
                    ChangeKind = PatchFileChangeKind.Modified,
                    Hunks =
                    [
                        new PatchHunk
                        {
                            Header = "@@ -1 +1 @@",
                            Lines =
                            [
                                new PatchLine { Kind = PatchLineKind.Added, Text = "added1" }
                            ]
                        }
                    ]
                },
                new PatchFile
                {
                    NewPath = "file2.cs",
                    ChangeKind = PatchFileChangeKind.Modified,
                    Hunks =
                    [
                        new PatchHunk
                        {
                            Header = "@@ -1 +1 @@",
                            Lines =
                            [
                                new PatchLine { Kind = PatchLineKind.Added, Text = "added2" }
                            ]
                        }
                    ]
                }
            ]
        };

        // Act
        var added = patch.GetAllAddedLines().ToList();

        // Assert
        Assert.Equal(2, added.Count);
        Assert.Contains(added, l => l.Text == "added1");
        Assert.Contains(added, l => l.Text == "added2");
    }

    [Fact]
    public void GetAllRemovedLines_MultipleFiles_ReturnsAllRemoved()
    {
        // Arrange
        var patch = new PatchModel
        {
            Files =
            [
                new PatchFile
                {
                    NewPath = "file1.cs",
                    ChangeKind = PatchFileChangeKind.Modified,
                    Hunks =
                    [
                        new PatchHunk
                        {
                            Header = "@@ -1 +1 @@",
                            Lines =
                            [
                                new PatchLine { Kind = PatchLineKind.Removed, Text = "removed1" }
                            ]
                        }
                    ]
                }
            ]
        };

        // Act
        var removed = patch.GetAllRemovedLines().ToList();

        // Assert
        Assert.Single(removed);
        Assert.Equal("removed1", removed[0].Text);
    }
}

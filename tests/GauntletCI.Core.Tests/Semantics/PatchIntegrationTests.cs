// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Semantics;

namespace GauntletCI.Core.Tests.Semantics;

/// <summary>
/// End-to-end integration tests: DiffContext → PatchModel → Operations → Transformations → Evidence.
/// Tests realistic scenarios demonstrating the semantic layer end-to-end.
/// </summary>
public class PatchIntegrationTests
{
    [Fact]
    public void DiffContext_ToPatchModel_PreservesStructure()
    {
        // Arrange: Create a DiffContext with a single modified file
        var diffContext = new DiffContext
        {
            RawDiff = "diff --git a/test.cs b/test.cs",
            CommitSha = "abc123",
            CommitMessage = "Add validation",
            Files = new List<DiffFile>
            {
                new()
                {
                    OldPath = "test.cs",
                    NewPath = "test.cs",
                    IsAdded = false,
                    IsDeleted = false,
                    Hunks = new List<DiffHunk>
                    {
                        new()
                        {
                            OldStartLine = 1,
                            NewStartLine = 1,
                            Lines = new List<DiffLine>
                            {
                                new() { Kind = DiffLineKind.Context, LineNumber = 1, OldLineNumber = 1, Content = "public class Test" },
                                new() { Kind = DiffLineKind.Added, LineNumber = 2, OldLineNumber = 0, Content = "    if (value == null) return;" },
                                new() { Kind = DiffLineKind.Context, LineNumber = 3, OldLineNumber = 2, Content = "}" }
                            }
                        }
                    }
                }
            }
        };

        // Act
        var patchModel = DiffToPatchAdapter.FromDiffContext(diffContext);

        // Assert
        Assert.NotNull(patchModel);
        Assert.Single(patchModel.Files);
        Assert.Equal("test.cs", patchModel.Files[0].NewPath);
        Assert.Equal(PatchFileChangeKind.Modified, patchModel.Files[0].ChangeKind);
        Assert.Equal(1, patchModel.CountAddedLines());
        Assert.Equal(0, patchModel.CountRemovedLines());
    }

    [Fact]
    public void MultipleFiles_AllConverted()
    {
        // Arrange
        var diffContext = new DiffContext
        {
            RawDiff = "multi-file diff",
            CommitSha = "def456",
            CommitMessage = "Refactor",
            Files = new List<DiffFile>
            {
                new()
                {
                    OldPath = "src/Auth.cs",
                    NewPath = "src/Auth.cs",
                    IsAdded = false,
                    IsDeleted = false,
                    Hunks = new List<DiffHunk>
                    {
                        new()
                        {
                            OldStartLine = 1,
                            NewStartLine = 1,
                            Lines = new List<DiffLine>
                            {
                                new() { Kind = DiffLineKind.Added, LineNumber = 1, OldLineNumber = 0, Content = "// Updated" }
                            }
                        }
                    }
                },
                new()
                {
                    OldPath = "tests/AuthTests.cs",
                    NewPath = "tests/AuthTests.cs",
                    IsAdded = false,
                    IsDeleted = false,
                    Hunks = new List<DiffHunk>
                    {
                        new()
                        {
                            OldStartLine = 50,
                            NewStartLine = 50,
                            Lines = new List<DiffLine>
                            {
                                new() { Kind = DiffLineKind.Added, LineNumber = 50, OldLineNumber = 0, Content = "// New test" }
                            }
                        }
                    }
                }
            }
        };

        // Act
        var patchModel = DiffToPatchAdapter.FromDiffContext(diffContext);

        // Assert
        Assert.Equal(2, patchModel.Files.Count);
        Assert.False(patchModel.Files[0].IsTestFile);
        Assert.True(patchModel.Files[1].IsTestFile);
        Assert.Equal(2, patchModel.CountAddedLines());
    }

    [Fact]
    public void TestFile_Classification()
    {
        // Arrange
        var diffContext = new DiffContext
        {
            RawDiff = "diff --git a/tests/Services/AuthServiceTests.cs b/tests/Services/AuthServiceTests.cs",
            CommitSha = "ghi789",
            CommitMessage = "Add unit test",
            Files = new List<DiffFile>
            {
                new()
                {
                    OldPath = "tests/Services/AuthServiceTests.cs",
                    NewPath = "tests/Services/AuthServiceTests.cs",
                    IsAdded = true,
                    IsDeleted = false,
                    Hunks = new List<DiffHunk>()
                }
            }
        };

        // Act
        var patchModel = DiffToPatchAdapter.FromDiffContext(diffContext);

        // Assert
        Assert.Single(patchModel.Files);
        var file = patchModel.Files[0];
        Assert.True(file.IsTestFile);
        Assert.Equal(PatchFileChangeKind.Added, file.ChangeKind);
    }

    [Fact]
    public void RenamedFile_DetectionWorks()
    {
        // Arrange
        var diffContext = new DiffContext
        {
            RawDiff = "diff --git a/Old.cs b/New.cs",
            CommitSha = "jkl012",
            CommitMessage = "Rename file",
            Files = new List<DiffFile>
            {
                new()
                {
                    OldPath = "Old.cs",
                    NewPath = "New.cs",
                    IsAdded = false,
                    IsDeleted = false,
                    Hunks = new List<DiffHunk>()
                }
            }
        };

        // Act
        var patchModel = DiffToPatchAdapter.FromDiffContext(diffContext);

        // Assert
        var file = patchModel.Files[0];
        Assert.NotNull(file);
        Assert.Equal("Old.cs", file.OldPath);
        Assert.Equal("New.cs", file.NewPath);
    }

    [Fact]
    public void DeletedFile_DetectionWorks()
    {
        // Arrange
        var diffContext = new DiffContext
        {
            RawDiff = "diff --git a/deprecated.cs b/deprecated.cs",
            CommitSha = "mno345",
            CommitMessage = "Remove file",
            Files = new List<DiffFile>
            {
                new()
                {
                    OldPath = "deprecated.cs",
                    NewPath = "deprecated.cs",
                    IsAdded = false,
                    IsDeleted = true,
                    Hunks = new List<DiffHunk>()
                }
            }
        };

        // Act
        var patchModel = DiffToPatchAdapter.FromDiffContext(diffContext);

        // Assert
        var file = patchModel.Files[0];
        Assert.Equal(PatchFileChangeKind.Deleted, file.ChangeKind);
    }

    [Fact]
    public void LargeHunk_CorrectlyHandled()
    {
        // Arrange: Create diff with many lines
        var lines = new List<DiffLine>();
        for (int i = 0; i < 50; i++)
        {
            lines.Add(new DiffLine
            {
                Kind = DiffLineKind.Added,
                LineNumber = i,
                OldLineNumber = 0,
                Content = $"    line_{i}();"
            });
        }

        var diffContext = new DiffContext
        {
            RawDiff = "large hunk",
            CommitSha = "pqr678",
            CommitMessage = "Large change",
            Files = new List<DiffFile>
            {
                new()
                {
                    OldPath = "large.cs",
                    NewPath = "large.cs",
                    IsAdded = false,
                    IsDeleted = false,
                    Hunks = new List<DiffHunk>
                    {
                        new()
                        {
                            OldStartLine = 1,
                            NewStartLine = 1,
                            Lines = lines
                        }
                    }
                }
            }
        };

        // Act
        var patchModel = DiffToPatchAdapter.FromDiffContext(diffContext);

        // Assert
        Assert.Single(patchModel.Files);
        var file = patchModel.Files[0];
        Assert.Single(file.Hunks);
        Assert.Equal(50, file.Hunks[0].Lines.Count);
        Assert.Equal(50, patchModel.CountAddedLines());
    }

    [Fact]
    public void EmptyDiff_ProducesEmptyPatch()
    {
        // Arrange
        var diffContext = new DiffContext
        {
            RawDiff = "",
            CommitSha = "empty",
            CommitMessage = null,
            Files = new List<DiffFile>()
        };

        // Act
        var patchModel = DiffToPatchAdapter.FromDiffContext(diffContext);

        // Assert
        Assert.NotNull(patchModel);
        Assert.Empty(patchModel.Files);
        Assert.Equal(0, patchModel.CountAddedLines());
        Assert.Equal(0, patchModel.CountRemovedLines());
    }

    [Fact]
    public void OperationFactory_CreatesValidOperations()
    {
        // Arrange & Act
        var lineAddedOp = PatchOperationFactory.LineAdded(10, "test.cs", "    new_code();", 0.5);
        var lineRemovedOp = PatchOperationFactory.LineRemoved(5, "test.cs", "    old_code();", 0.5);
        var identifierRenamedOp = PatchOperationFactory.IdentifierRenamed("oldVar", "newVar", 15, "test.cs", 0.7);

        // Assert
        Assert.Equal(PatchOperationKind.LineAdded, lineAddedOp.Kind);
        Assert.Equal(10, lineAddedOp.NewLineNumber);
        Assert.Equal(PatchOperationKind.LineRemoved, lineRemovedOp.Kind);
        Assert.Equal(5, lineRemovedOp.OldLineNumber);
        Assert.Equal(PatchOperationKind.IdentifierRenamed, identifierRenamedOp.Kind);
        Assert.Equal(0.5, lineAddedOp.RiskLevel);
        Assert.Equal(0.7, identifierRenamedOp.RiskLevel);
    }

    [Fact]
    public void OperationCollection_QueryingWorks()
    {
        // Arrange
        var ops = new PatchOperationCollection();
        ops.Add(PatchOperationFactory.LineAdded(1, "a.cs", "line1", 0.1));
        ops.Add(PatchOperationFactory.LineRemoved(2, "b.cs", "line2", 0.9));
        ops.Add(PatchOperationFactory.IdentifierRenamed("old", "new", 3, "c.cs", 0.5));

        // Act & Assert
        Assert.Equal(3, ops.Count);
        Assert.Single(ops.ByKind(PatchOperationKind.LineAdded));
        Assert.Single(ops.ByFile("a.cs"));
        Assert.NotEmpty(ops.ByMinRisk(0.4));
        Assert.Equal(2, ops.ByMinRisk(0.4).Count());
        
        // Check aggregation
        var maxRisk = ops.AggregateRisk("max");
        Assert.Equal(0.9, maxRisk);
        
        var meanRisk = ops.AggregateRisk("mean");
        Assert.Equal((0.1 + 0.9 + 0.5) / 3, meanRisk, 2);
    }

    [Fact]
    public void TransformationFactory_CreatesValidTransformations()
    {
        // Arrange
        var ops = new List<PatchOperation>
        {
            PatchOperationFactory.LineRemoved(20, "Service.cs", "    old code;"),
            PatchOperationFactory.LineAdded(20, "Service.cs", "    new code;")
        };

        // Act
        var extractMethod = PatchTransformationFactory.ExtractMethod("ValidateUser", "Service.cs", ops, 0.6);
        var renameTransform = PatchTransformationFactory.Rename(
            PatchTransformationKind.RenameMethod,
            "OldName",
            "NewName",
            "Logic.cs",
            ops,
            0.4
        );

        // Assert
        Assert.Equal(PatchTransformationKind.ExtractMethod, extractMethod.Kind);
        Assert.Equal(0.6, extractMethod.RiskLevel);
        Assert.Equal(PatchTransformationKind.RenameMethod, renameTransform.Kind);
        Assert.Equal(0.4, renameTransform.RiskLevel);
    }

    [Fact]
    public void TransformationCollection_FilteringWorks()
    {
        // Arrange
        var ops = new List<PatchOperation> { PatchOperationFactory.LineAdded(1, "test.cs", "line") };
        var transforms = new PatchTransformationCollection();
        transforms.Add(PatchTransformationFactory.ExtractMethod("Method1", "Service.cs", ops, 0.5));
        transforms.Add(PatchTransformationFactory.Rename(
            PatchTransformationKind.RenameMethod,
            "Old",
            "New",
            "Logic.cs",
            ops,
            0.3
        ));
        transforms.Add(PatchTransformationFactory.ExtractMethod("Method2", "Service.cs", ops, 0.7));

        // Act & Assert
        Assert.Equal(3, transforms.Count);
        
        var extracts = transforms.ByKind(PatchTransformationKind.ExtractMethod).ToList();
        Assert.Equal(2, extracts.Count);
        
        var serviceTransforms = transforms.ByFile("Service.cs").ToList();
        Assert.Equal(2, serviceTransforms.Count);
        
        var highRisk = transforms.ByMinRisk(0.5).ToList();
        Assert.Equal(2, highRisk.Count);

        var meanRisk = transforms.AggregateRisk("mean");
        Assert.Equal((0.5 + 0.3 + 0.7) / 3, meanRisk, 2);
    }

    [Fact]
    public void EvidenceFactory_CreatesValidEvidence()
    {
        // Arrange & Act
        var addedLineEvidence = PatchEvidenceFactory.AddedLine(10, "test.cs", "new code", 0.95);
        var operatorChangeEvidence = PatchEvidenceFactory.OperatorChange(">", ">=", 5, "logic.cs", 0.85);
        var patternEvidence = PatchEvidenceFactory.PatternOccurrence("RefactoringPattern", "ExtractMethod", "test.cs", 0.7);

        // Assert
        Assert.Equal(PatchEvidenceKind.AddedLine, addedLineEvidence.Kind);
        Assert.Equal(0.95, addedLineEvidence.Confidence);
        Assert.Equal(PatchEvidenceKind.OperatorChange, operatorChangeEvidence.Kind);
        Assert.Equal(0.85, operatorChangeEvidence.Confidence);
        Assert.Equal(PatchEvidenceKind.PatternOccurrence, patternEvidence.Kind);
        Assert.Equal(0.7, patternEvidence.Confidence);
    }

    [Fact]
    public void EvidenceCollection_AggregationWorks()
    {
        // Arrange
        var evidence = new PatchEvidenceCollection();
        evidence.Add(PatchEvidenceFactory.AddedLine(1, "a.cs", "line1", 1.0));
        evidence.Add(PatchEvidenceFactory.OperatorChange("<", ">", 2, "b.cs", 0.85));
        evidence.Add(PatchEvidenceFactory.PatternOccurrence("pattern", "test", "c.cs", 0.6));

        // Act & Assert
        Assert.Equal(3, evidence.Count);
        
        var byKind = evidence.ByKind(PatchEvidenceKind.AddedLine).ToList();
        Assert.Single(byKind);
        
        var highConfidence = evidence.ByMinConfidence(0.8).ToList();
        Assert.Equal(2, highConfidence.Count);
        
        var meanConfidence = evidence.AggregateConfidence("mean");
        Assert.Equal((1.0 + 0.85 + 0.6) / 3, meanConfidence, 2);
    }

    [Fact]
    public void Evidence_KnowledgeLevelTracking()
    {
        // Arrange
        var evidence = new PatchEvidenceCollection();
        evidence.Add(new PatchEvidence
        {
            Kind = PatchEvidenceKind.AddedLine,
            KnowledgeLevel = PatchKnowledgeLevel.KnownFromPatch,
            Confidence = 1.0
        });
        evidence.Add(new PatchEvidence
        {
            Kind = PatchEvidenceKind.PatternOccurrence,
            KnowledgeLevel = PatchKnowledgeLevel.StronglyInferred,
            Confidence = 0.85
        });
        evidence.Add(new PatchEvidence
        {
            Kind = PatchEvidenceKind.HeuristicMatch,
            KnowledgeLevel = PatchKnowledgeLevel.WeaklyInferred,
            Confidence = 0.6
        });

        // Act
        var known = evidence.KnownEvidence().ToList();
        var inferred = evidence.InferredEvidence().ToList();

        // Assert
        Assert.Equal(2, known.Count);
        Assert.Single(inferred);
    }

    [Fact]
    public void RoundTrip_ComplexDiff()
    {
        // Arrange: Realistic complex diff
        var diffContext = new DiffContext
        {
            RawDiff = "comprehensive diff",
            CommitSha = "complex123",
            CommitMessage = "Fix critical bug with validation",
            Files = new List<DiffFile>
            {
                new()
                {
                    OldPath = "src/Service.cs",
                    NewPath = "src/Service.cs",
                    IsAdded = false,
                    IsDeleted = false,
                    Hunks = new List<DiffHunk>
                    {
                        new()
                        {
                            OldStartLine = 20,
                            NewStartLine = 20,
                            Lines = new List<DiffLine>
                            {
                                new() { Kind = DiffLineKind.Context, LineNumber = 20, OldLineNumber = 20, Content = "public bool Authorize(User user)" },
                                new() { Kind = DiffLineKind.Removed, LineNumber = 0, OldLineNumber = 21, Content = "    if (user == null)" },
                                new() { Kind = DiffLineKind.Added, LineNumber = 21, OldLineNumber = 0, Content = "    if (user == null || !user.IsActive)" },
                                new() { Kind = DiffLineKind.Context, LineNumber = 22, OldLineNumber = 22, Content = "        throw new UnauthorizedAccessException();" }
                            }
                        }
                    }
                }
            }
        };

        // Act: Convert and verify full round-trip
        var patchModel = DiffToPatchAdapter.FromDiffContext(diffContext);
        var operations = new PatchOperationCollection();
        operations.Add(PatchOperationFactory.LineRemoved(21, "src/Service.cs", "    if (user == null)", 0.4));
        operations.Add(PatchOperationFactory.LineAdded(21, "src/Service.cs", "    if (user == null || !user.IsActive)", 0.6));

        var transformations = new PatchTransformationCollection();
        transformations.Add(new PatchTransformation
        {
            Kind = PatchTransformationKind.LogicSimplified,
            TargetSymbol = "Authorize",
            RiskLevel = 0.5
        });

        var evidence = new PatchEvidenceCollection();
        evidence.Add(PatchEvidenceFactory.OperatorChange("==", "== || !", 21, "src/Service.cs", 0.9));
        evidence.Add(PatchEvidenceFactory.AddedLine(21, "src/Service.cs", "!user.IsActive check", 0.95));

        // Assert
        Assert.Single(patchModel.Files);
        Assert.Equal("src/Service.cs", patchModel.Files[0].NewPath);
        Assert.Equal(1, patchModel.CountAddedLines());
        Assert.Equal(1, patchModel.CountRemovedLines());
        
        Assert.Equal(2, operations.Count);
        Assert.Equal(1, transformations.Count);
        Assert.Equal(2, evidence.Count);

        var knownEvidence = evidence.KnownEvidence().ToList();
        Assert.Equal(2, knownEvidence.Count);
    }
}

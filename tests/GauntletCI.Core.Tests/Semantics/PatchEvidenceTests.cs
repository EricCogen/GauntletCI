// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Core.Tests.Semantics;

using Xunit;
using GauntletCI.Core.Semantics;

public class PatchEvidenceTests
{
    [Fact]
    public void Evidence_CanSetKind()
    {
        var e = new PatchEvidence { Kind = PatchEvidenceKind.AddedLine };
        Assert.Equal(PatchEvidenceKind.AddedLine, e.Kind);
    }

    [Fact]
    public void Evidence_CanSetKnowledgeLevel()
    {
        var e = new PatchEvidence { KnowledgeLevel = PatchKnowledgeLevel.StronglyInferred };
        Assert.Equal(PatchKnowledgeLevel.StronglyInferred, e.KnowledgeLevel);
    }

    [Fact]
    public void Evidence_DefaultKnowledgeLevelIsKnownFromPatch()
    {
        var e = new PatchEvidence();
        Assert.Equal(PatchKnowledgeLevel.KnownFromPatch, e.KnowledgeLevel);
    }

    [Fact]
    public void Evidence_CanSetDescription()
    {
        var e = new PatchEvidence { Description = "Test evidence" };
        Assert.Equal("Test evidence", e.Description);
    }

    [Fact]
    public void Evidence_CanSetConfidence()
    {
        var e = new PatchEvidence { Confidence = 0.85 };
        Assert.Equal(0.85, e.Confidence);
    }

    [Fact]
    public void Evidence_DefaultConfidenceIsOne()
    {
        var e = new PatchEvidence();
        Assert.Equal(1.0, e.Confidence);
    }

    [Fact]
    public void Evidence_CanSetText()
    {
        var e = new PatchEvidence { Text = "some evidence text" };
        Assert.Equal("some evidence text", e.Text);
    }

    [Fact]
    public void Evidence_CanSetFilePath()
    {
        var e = new PatchEvidence { FilePath = "file.cs" };
        Assert.Equal("file.cs", e.FilePath);
    }

    [Fact]
    public void Evidence_CanSetContext()
    {
        var e = new PatchEvidence { Context = "surrounding code" };
        Assert.Equal("surrounding code", e.Context);
    }

    [Fact]
    public void Evidence_CanSetRelatedEvidenceIds()
    {
        var ids = new[] { "ev1", "ev2" };
        var e = new PatchEvidence { RelatedEvidenceIds = ids };
        Assert.Equal(2, e.RelatedEvidenceIds.Count);
    }
}

public class PatchEvidenceCollectionTests
{
    [Fact]
    public void Empty_Collection_ReturnsZeroCount()
    {
        var collection = new PatchEvidenceCollection();
        Assert.Equal(0, collection.Count);
    }

    [Fact]
    public void Add_SingleEvidence_IncreasesCount()
    {
        var collection = new PatchEvidenceCollection();
        var e = new PatchEvidence { Kind = PatchEvidenceKind.AddedLine };

        collection.Add(e);

        Assert.Equal(1, collection.Count);
    }

    [Fact]
    public void AddRange_MultipleEvidence_AllAreStored()
    {
        var collection = new PatchEvidenceCollection();
        var evidence = new[]
        {
            new PatchEvidence { Kind = PatchEvidenceKind.AddedLine },
            new PatchEvidence { Kind = PatchEvidenceKind.OperatorChange }
        };

        collection.AddRange(evidence);

        Assert.Equal(2, collection.Count);
    }

    [Fact]
    public void ByKind_FiltersCorrectly()
    {
        var collection = new PatchEvidenceCollection();
        collection.Add(new PatchEvidence { Kind = PatchEvidenceKind.AddedLine });
        collection.Add(new PatchEvidence { Kind = PatchEvidenceKind.AddedLine });
        collection.Add(new PatchEvidence { Kind = PatchEvidenceKind.RemovedLine });

        var added = collection.ByKind(PatchEvidenceKind.AddedLine).ToList();

        Assert.Equal(2, added.Count);
    }

    [Fact]
    public void ByKnowledgeLevel_FiltersCorrectly()
    {
        var collection = new PatchEvidenceCollection();
        collection.Add(new PatchEvidence { KnowledgeLevel = PatchKnowledgeLevel.KnownFromPatch });
        collection.Add(new PatchEvidence { KnowledgeLevel = PatchKnowledgeLevel.KnownFromPatch });
        collection.Add(new PatchEvidence { KnowledgeLevel = PatchKnowledgeLevel.WeaklyInferred });

        var known = collection.ByKnowledgeLevel(PatchKnowledgeLevel.KnownFromPatch).ToList();

        Assert.Equal(2, known.Count);
    }

    [Fact]
    public void ByFile_FiltersCorrectly()
    {
        var collection = new PatchEvidenceCollection();
        collection.Add(new PatchEvidence { FilePath = "a.cs" });
        collection.Add(new PatchEvidence { FilePath = "a.cs" });
        collection.Add(new PatchEvidence { FilePath = "b.cs" });

        var fromA = collection.ByFile("a.cs").ToList();

        Assert.Equal(2, fromA.Count);
    }

    [Fact]
    public void ByMinConfidence_FiltersCorrectly()
    {
        var collection = new PatchEvidenceCollection();
        collection.Add(new PatchEvidence { Confidence = 0.5 });
        collection.Add(new PatchEvidence { Confidence = 0.8 });
        collection.Add(new PatchEvidence { Confidence = 0.9 });

        var confident = collection.ByMinConfidence(0.75).ToList();

        Assert.Equal(2, confident.Count);
    }

    [Fact]
    public void KnownEvidence_ReturnsHighCertainty()
    {
        var collection = new PatchEvidenceCollection();
        collection.Add(new PatchEvidence { KnowledgeLevel = PatchKnowledgeLevel.KnownFromPatch });
        collection.Add(new PatchEvidence { KnowledgeLevel = PatchKnowledgeLevel.StronglyInferred });
        collection.Add(new PatchEvidence { KnowledgeLevel = PatchKnowledgeLevel.WeaklyInferred });

        var known = collection.KnownEvidence().ToList();

        Assert.Equal(2, known.Count);
    }

    [Fact]
    public void InferredEvidence_ReturnsLowCertainty()
    {
        var collection = new PatchEvidenceCollection();
        collection.Add(new PatchEvidence { KnowledgeLevel = PatchKnowledgeLevel.KnownFromPatch });
        collection.Add(new PatchEvidence { KnowledgeLevel = PatchKnowledgeLevel.WeaklyInferred });
        collection.Add(new PatchEvidence { KnowledgeLevel = PatchKnowledgeLevel.UnknownFromPatchAlone });

        var inferred = collection.InferredEvidence().ToList();

        Assert.Equal(2, inferred.Count);
    }

    [Fact]
    public void ByLineNumber_FiltersCorrectly()
    {
        var collection = new PatchEvidenceCollection();
        collection.Add(new PatchEvidence { NewLineNumber = 10 });
        collection.Add(new PatchEvidence { OldLineNumber = 10 });
        collection.Add(new PatchEvidence { NewLineNumber = 20 });

        var atLine10 = collection.ByLineNumber(10).ToList();

        Assert.Equal(2, atLine10.Count);
    }

    [Fact]
    public void CountByKind_AggregatesCorrectly()
    {
        var collection = new PatchEvidenceCollection();
        collection.Add(new PatchEvidence { Kind = PatchEvidenceKind.AddedLine });
        collection.Add(new PatchEvidence { Kind = PatchEvidenceKind.AddedLine });
        collection.Add(new PatchEvidence { Kind = PatchEvidenceKind.OperatorChange });

        var counts = collection.CountByKind();

        Assert.Equal(2, counts[PatchEvidenceKind.AddedLine]);
        Assert.Equal(1, counts[PatchEvidenceKind.OperatorChange]);
    }

    [Fact]
    public void CountByKnowledgeLevel_AggregatesCorrectly()
    {
        var collection = new PatchEvidenceCollection();
        collection.Add(new PatchEvidence { KnowledgeLevel = PatchKnowledgeLevel.KnownFromPatch });
        collection.Add(new PatchEvidence { KnowledgeLevel = PatchKnowledgeLevel.KnownFromPatch });
        collection.Add(new PatchEvidence { KnowledgeLevel = PatchKnowledgeLevel.WeaklyInferred });

        var counts = collection.CountByKnowledgeLevel();

        Assert.Equal(2, counts[PatchKnowledgeLevel.KnownFromPatch]);
        Assert.Equal(1, counts[PatchKnowledgeLevel.WeaklyInferred]);
    }

    [Fact]
    public void AggregateConfidence_Mean_ReturnsMean()
    {
        var collection = new PatchEvidenceCollection();
        collection.Add(new PatchEvidence { Confidence = 0.5 });
        collection.Add(new PatchEvidence { Confidence = 0.6 });
        collection.Add(new PatchEvidence { Confidence = 0.7 });

        var meanConfidence = collection.AggregateConfidence("mean");

        Assert.Equal(0.6, meanConfidence);
    }

    [Fact]
    public void AggregateConfidence_Min_ReturnsMinimum()
    {
        var collection = new PatchEvidenceCollection();
        collection.Add(new PatchEvidence { Confidence = 0.5 });
        collection.Add(new PatchEvidence { Confidence = 0.8 });
        collection.Add(new PatchEvidence { Confidence = 0.9 });

        var minConfidence = collection.AggregateConfidence("min");

        Assert.Equal(0.5, minConfidence);
    }

    [Fact]
    public void AggregateConfidence_Max_ReturnsMaximum()
    {
        var collection = new PatchEvidenceCollection();
        collection.Add(new PatchEvidence { Confidence = 0.5 });
        collection.Add(new PatchEvidence { Confidence = 0.8 });
        collection.Add(new PatchEvidence { Confidence = 0.9 });

        var maxConfidence = collection.AggregateConfidence("max");

        Assert.Equal(0.9, maxConfidence);
    }

    [Fact]
    public void AggregateConfidence_OnEmpty_ReturnsZero()
    {
        var collection = new PatchEvidenceCollection();

        var confidence = collection.AggregateConfidence("mean");

        Assert.Equal(0, confidence);
    }

    [Fact]
    public void Clear_RemovesAllEvidence()
    {
        var collection = new PatchEvidenceCollection();
        collection.Add(new PatchEvidence());
        collection.Add(new PatchEvidence());

        collection.Clear();

        Assert.Equal(0, collection.Count);
    }
}

public class PatchEvidenceFactoryTests
{
    [Fact]
    public void AddedLine_CreatesCorrectEvidence()
    {
        var e = PatchEvidenceFactory.AddedLine(42, "file.cs", "int x = 5;");

        Assert.Equal(PatchEvidenceKind.AddedLine, e.Kind);
        Assert.Equal(PatchKnowledgeLevel.KnownFromPatch, e.KnowledgeLevel);
        Assert.Equal(42, e.NewLineNumber);
        Assert.Equal("file.cs", e.FilePath);
        Assert.Equal("int x = 5;", e.Text);
        Assert.Equal(1.0, e.Confidence);
    }

    [Fact]
    public void RemovedLine_CreatesCorrectEvidence()
    {
        var e = PatchEvidenceFactory.RemovedLine(42, "file.cs", "int x = 5;");

        Assert.Equal(PatchEvidenceKind.RemovedLine, e.Kind);
        Assert.Equal(PatchKnowledgeLevel.KnownFromPatch, e.KnowledgeLevel);
        Assert.Equal(42, e.OldLineNumber);
        Assert.Equal("file.cs", e.FilePath);
    }

    [Fact]
    public void OperatorChange_CreatesCorrectEvidence()
    {
        var e = PatchEvidenceFactory.OperatorChange("+", "-", 42, "file.cs");

        Assert.Equal(PatchEvidenceKind.OperatorChange, e.Kind);
        Assert.Equal(PatchKnowledgeLevel.KnownFromPatch, e.KnowledgeLevel);
        Assert.Equal("+ → -", e.Text);
        Assert.Equal(1.0, e.Confidence);
    }

    [Fact]
    public void KeywordChange_CreatesCorrectEvidence()
    {
        var e = PatchEvidenceFactory.KeywordChange("if", true, 42, "file.cs");

        Assert.Equal(PatchEvidenceKind.KeywordChange, e.Kind);
        Assert.Equal(PatchKnowledgeLevel.KnownFromPatch, e.KnowledgeLevel);
        Assert.Equal("if", e.Text);
    }

    [Fact]
    public void IdentifierChange_CreatesCorrectEvidence()
    {
        var e = PatchEvidenceFactory.IdentifierChange("count", "total", 42, "file.cs");

        Assert.Equal(PatchEvidenceKind.IdentifierChange, e.Kind);
        Assert.Equal(PatchKnowledgeLevel.KnownFromPatch, e.KnowledgeLevel);
        Assert.Equal("count → total", e.Text);
        Assert.Equal(0.95, e.Confidence);
    }

    [Fact]
    public void PatternOccurrence_CreatesCorrectEvidence()
    {
        var e = PatchEvidenceFactory.PatternOccurrence(
            "builder-pattern",
            "Builder pattern detected",
            "file.cs");

        Assert.Equal(PatchEvidenceKind.PatternOccurrence, e.Kind);
        Assert.Equal(PatchKnowledgeLevel.StronglyInferred, e.KnowledgeLevel);
        Assert.Equal(0.8, e.Confidence);
    }

    [Fact]
    public void RefactoringPattern_CreatesCorrectEvidence()
    {
        var e = PatchEvidenceFactory.RefactoringPattern(
            "extract-method",
            "Extract method refactoring detected",
            new[] { "file1.cs", "file2.cs" });

        Assert.Equal(PatchEvidenceKind.RefactoringPattern, e.Kind);
        Assert.Equal(PatchKnowledgeLevel.StronglyInferred, e.KnowledgeLevel);
        Assert.Equal("extract-method", e.Text);
        Assert.Equal(0.85, e.Confidence);
    }

    [Fact]
    public void SecurityPattern_CreatesCorrectEvidence()
    {
        var e = PatchEvidenceFactory.SecurityPattern(
            "input-validation",
            "Input validation pattern detected",
            "file.cs");

        Assert.Equal(PatchEvidenceKind.SecurityPattern, e.Kind);
        Assert.Equal(PatchKnowledgeLevel.StronglyInferred, e.KnowledgeLevel);
        Assert.Equal(0.75, e.Confidence);
    }

    [Fact]
    public void HeuristicMatch_CreatesCorrectEvidence()
    {
        var e = PatchEvidenceFactory.HeuristicMatch(
            "mutation-detector",
            "Heuristic match for behavioral change",
            "file.cs");

        Assert.Equal(PatchEvidenceKind.HeuristicMatch, e.Kind);
        Assert.Equal(PatchKnowledgeLevel.WeaklyInferred, e.KnowledgeLevel);
        Assert.Equal(0.6, e.Confidence);
    }

    [Fact]
    public void AnomalyDetected_CreatesCorrectEvidence()
    {
        var e = PatchEvidenceFactory.AnomalyDetected(
            "unusual-pattern",
            "Anomaly detected in patch",
            "file.cs");

        Assert.Equal(PatchEvidenceKind.AnomalyDetected, e.Kind);
        Assert.Equal(PatchKnowledgeLevel.WeaklyInferred, e.KnowledgeLevel);
        Assert.Equal(0.7, e.Confidence);
    }

    [Fact]
    public void Factory_CanOverrideConfidence()
    {
        var e = PatchEvidenceFactory.AddedLine(42, "file.cs", "code", confidence: 0.5);

        Assert.Equal(0.5, e.Confidence);
    }
}

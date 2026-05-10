// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Core.Tests.Semantics;

using Xunit;
using GauntletCI.Core.Semantics;

public class PatchTransformationTests
{
    [Fact]
    public void Transformation_CanSetName()
    {
        var t = new PatchTransformation { Name = "Extract method" };
        Assert.Equal("Extract method", t.Name);
    }

    [Fact]
    public void Transformation_CanSetDescription()
    {
        var t = new PatchTransformation { Description = "Test description" };
        Assert.Equal("Test description", t.Description);
    }

    [Fact]
    public void Transformation_CanSetAffectedFiles()
    {
        var files = new[] { "file1.cs", "file2.cs" };
        var t = new PatchTransformation { AffectedFiles = files };
        Assert.Equal(2, t.AffectedFiles.Count);
    }

    [Fact]
    public void Transformation_CanSetOperations()
    {
        var ops = new[] { new PatchOperation() };
        var t = new PatchTransformation { Operations = ops };
        Assert.Single(t.Operations);
    }

    [Fact]
    public void Transformation_CanSetRiskLevel()
    {
        var t = new PatchTransformation { RiskLevel = 0.8 };
        Assert.Equal(0.8, t.RiskLevel);
    }

    [Fact]
    public void Transformation_CanSetBehavioralImpact()
    {
        var t = new PatchTransformation { BehavioralImpact = "Logic" };
        Assert.Equal("Logic", t.BehavioralImpact);
    }

    [Fact]
    public void Transformation_CanSetIsSafePattern()
    {
        var t = new PatchTransformation { IsSafePattern = true };
        Assert.True(t.IsSafePattern);
    }

    [Fact]
    public void Transformation_CanSetTargetSymbol()
    {
        var t = new PatchTransformation { TargetSymbol = "MyMethod" };
        Assert.Equal("MyMethod", t.TargetSymbol);
    }
}

public class PatchTransformationCollectionTests
{
    [Fact]
    public void Empty_Collection_ReturnsZeroCount()
    {
        var collection = new PatchTransformationCollection();
        Assert.Equal(0, collection.Count);
    }

    [Fact]
    public void Add_SingleTransformation_IncreasesCount()
    {
        var collection = new PatchTransformationCollection();
        var t = new PatchTransformation { Kind = PatchTransformationKind.ExtractMethod };

        collection.Add(t);

        Assert.Equal(1, collection.Count);
    }

    [Fact]
    public void AddRange_MultipleTransformations_AllAreStored()
    {
        var collection = new PatchTransformationCollection();
        var ts = new[]
        {
            new PatchTransformation { Kind = PatchTransformationKind.ExtractMethod },
            new PatchTransformation { Kind = PatchTransformationKind.RenameMethod }
        };

        collection.AddRange(ts);

        Assert.Equal(2, collection.Count);
    }

    [Fact]
    public void ByKind_FiltersCorrectly()
    {
        var collection = new PatchTransformationCollection();
        collection.Add(new PatchTransformation { Kind = PatchTransformationKind.ExtractMethod });
        collection.Add(new PatchTransformation { Kind = PatchTransformationKind.ExtractMethod });
        collection.Add(new PatchTransformation { Kind = PatchTransformationKind.RenameMethod });

        var extracted = collection.ByKind(PatchTransformationKind.ExtractMethod).ToList();

        Assert.Equal(2, extracted.Count);
    }

    [Fact]
    public void ByFile_FiltersCorrectly()
    {
        var collection = new PatchTransformationCollection();
        collection.Add(new PatchTransformation { AffectedFiles = ["a.cs"] });
        collection.Add(new PatchTransformation { AffectedFiles = ["a.cs", "b.cs"] });
        collection.Add(new PatchTransformation { AffectedFiles = ["b.cs"] });

        var fromA = collection.ByFile("a.cs").ToList();

        Assert.Equal(2, fromA.Count);
    }

    [Fact]
    public void ByTargetSymbol_FiltersCorrectly()
    {
        var collection = new PatchTransformationCollection();
        collection.Add(new PatchTransformation { TargetSymbol = "MyMethod" });
        collection.Add(new PatchTransformation { TargetSymbol = "MyMethod" });
        collection.Add(new PatchTransformation { TargetSymbol = "OtherMethod" });

        var myMethod = collection.ByTargetSymbol("MyMethod").ToList();

        Assert.Equal(2, myMethod.Count);
    }

    [Fact]
    public void ByMinRisk_FiltersCorrectly()
    {
        var collection = new PatchTransformationCollection();
        collection.Add(new PatchTransformation { RiskLevel = 0.3 });
        collection.Add(new PatchTransformation { RiskLevel = 0.7 });
        collection.Add(new PatchTransformation { RiskLevel = 0.9 });

        var highRisk = collection.ByMinRisk(0.65).ToList();

        Assert.Equal(2, highRisk.Count);
    }

    [Fact]
    public void ByBehavioralImpact_FiltersCorrectly()
    {
        var collection = new PatchTransformationCollection();
        collection.Add(new PatchTransformation { BehavioralImpact = "Logic" });
        collection.Add(new PatchTransformation { BehavioralImpact = "Logic" });
        collection.Add(new PatchTransformation { BehavioralImpact = "Performance" });

        var logic = collection.ByBehavioralImpact("Logic").ToList();

        Assert.Equal(2, logic.Count);
    }

    [Fact]
    public void SafePatterns_FiltersCorrectly()
    {
        var collection = new PatchTransformationCollection();
        collection.Add(new PatchTransformation { IsSafePattern = true });
        collection.Add(new PatchTransformation { IsSafePattern = true });
        collection.Add(new PatchTransformation { IsSafePattern = false });

        var safe = collection.SafePatterns().ToList();

        Assert.Equal(2, safe.Count);
    }

    [Fact]
    public void RiskyTransformations_FiltersCorrectly()
    {
        var collection = new PatchTransformationCollection();
        collection.Add(new PatchTransformation { IsSafePattern = true });
        collection.Add(new PatchTransformation { IsSafePattern = false });
        collection.Add(new PatchTransformation { IsSafePattern = false });

        var risky = collection.RiskyTransformations().ToList();

        Assert.Equal(2, risky.Count);
    }

    [Fact]
    public void CountByKind_AggregatesCorrectly()
    {
        var collection = new PatchTransformationCollection();
        collection.Add(new PatchTransformation { Kind = PatchTransformationKind.ExtractMethod });
        collection.Add(new PatchTransformation { Kind = PatchTransformationKind.ExtractMethod });
        collection.Add(new PatchTransformation { Kind = PatchTransformationKind.RenameMethod });

        var counts = collection.CountByKind();

        Assert.Equal(2, counts[PatchTransformationKind.ExtractMethod]);
        Assert.Equal(1, counts[PatchTransformationKind.RenameMethod]);
    }

    [Fact]
    public void CountByBehavioralImpact_AggregatesCorrectly()
    {
        var collection = new PatchTransformationCollection();
        collection.Add(new PatchTransformation { BehavioralImpact = "Logic" });
        collection.Add(new PatchTransformation { BehavioralImpact = "Logic" });
        collection.Add(new PatchTransformation { BehavioralImpact = "Performance" });

        var counts = collection.CountByBehavioralImpact();

        Assert.Equal(2, counts["Logic"]);
        Assert.Equal(1, counts["Performance"]);
    }

    [Fact]
    public void AggregateRisk_Max_ReturnsMaximum()
    {
        var collection = new PatchTransformationCollection();
        collection.Add(new PatchTransformation { RiskLevel = 0.3 });
        collection.Add(new PatchTransformation { RiskLevel = 0.8 });
        collection.Add(new PatchTransformation { RiskLevel = 0.5 });

        var maxRisk = collection.AggregateRisk("max");

        Assert.Equal(0.8, maxRisk);
    }

    [Fact]
    public void AggregateRisk_Mean_ReturnsMean()
    {
        var collection = new PatchTransformationCollection();
        collection.Add(new PatchTransformation { RiskLevel = 0.3 });
        collection.Add(new PatchTransformation { RiskLevel = 0.6 });
        collection.Add(new PatchTransformation { RiskLevel = 0.9 });

        var meanRisk = collection.AggregateRisk("mean");

        Assert.Equal(0.6, meanRisk);
    }

    [Fact]
    public void Clear_RemovesAllTransformations()
    {
        var collection = new PatchTransformationCollection();
        collection.Add(new PatchTransformation());
        collection.Add(new PatchTransformation());

        collection.Clear();

        Assert.Equal(0, collection.Count);
    }
}

public class PatchTransformationFactoryTests
{
    [Fact]
    public void ExtractMethod_CreatesCorrectTransformation()
    {
        var ops = new[] { new PatchOperation() };
        var t = PatchTransformationFactory.ExtractMethod("Calculate", "math.cs", ops);

        Assert.Equal(PatchTransformationKind.ExtractMethod, t.Kind);
        Assert.Equal("Calculate", t.TargetSymbol);
        Assert.Equal("math.cs", t.AffectedFiles[0]);
        Assert.True(t.IsSafePattern);
    }

    [Fact]
    public void Rename_CreatesCorrectTransformation()
    {
        var ops = new[] { new PatchOperation() };
        var t = PatchTransformationFactory.Rename(
            PatchTransformationKind.RenameMethod,
            "OldName",
            "NewName",
            "file.cs",
            ops);

        Assert.Equal(PatchTransformationKind.RenameMethod, t.Kind);
        Assert.Equal("OldName", t.SourceSymbol);
        Assert.Equal("NewName", t.TargetSymbol);
        Assert.True(t.IsSafePattern);
    }

    [Fact]
    public void LogicChanged_CreatesCorrectTransformation()
    {
        var ops = new[] { new PatchOperation() };
        var t = PatchTransformationFactory.LogicChanged(
            "Condition modified",
            new[] { "file.cs" },
            ops);

        Assert.Equal(PatchTransformationKind.LogicSimplified, t.Kind);
        Assert.False(t.IsSafePattern);
        Assert.Equal("Logic", t.BehavioralImpact);
    }

    [Fact]
    public void AccessModifierChanged_CreatesCorrectTransformation()
    {
        var ops = new[] { new PatchOperation() };
        var t = PatchTransformationFactory.AccessModifierChanged(
            "MyProperty",
            "private",
            "public",
            "file.cs",
            ops);

        Assert.Equal(PatchTransformationKind.AccessModifierChanged, t.Kind);
        Assert.Equal("MyProperty", t.TargetSymbol);
        Assert.Equal("Compatibility", t.BehavioralImpact);
    }

    [Fact]
    public void DependencyChanged_CreatesCorrectTransformation()
    {
        var ops = new[] { new PatchOperation() };
        var t = PatchTransformationFactory.DependencyChanged(
            "Newtonsoft.Json",
            "12.0.1",
            "13.0.1",
            new[] { "packages.config" },
            ops);

        Assert.Equal(PatchTransformationKind.DependencyVersionChanged, t.Kind);
        Assert.Equal("Newtonsoft.Json", t.TargetSymbol);
        Assert.Equal("Compatibility", t.BehavioralImpact);
    }

    [Fact]
    public void PerformanceOptimization_CreatesCorrectTransformation()
    {
        var ops = new[] { new PatchOperation() };
        var t = PatchTransformationFactory.PerformanceOptimization(
            "Added caching",
            "QueryCache",
            new[] { "cache.cs" },
            ops);

        Assert.Equal(PatchTransformationKind.PerformanceOptimization, t.Kind);
        Assert.True(t.IsSafePattern);
        Assert.Equal("Performance", t.BehavioralImpact);
    }
}

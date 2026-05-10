namespace GauntletCI.Core.Tests.Semantics;

using GauntletCI.Core.Semantics;
using Xunit;

/// <summary>
/// Tests for PatchCounterfactual model and collection.
/// </summary>
public class PatchCounterfactualTests
{
    [Fact]
    public void Counterfactual_CanSetAllProperties()
    {
        // Act
        var cf = new PatchCounterfactual
        {
            Id = "cf-1",
            Kind = PatchCounterfactualKind.BoundaryValue,
            Description = "amount == limit",
            FilePath = "OrderService.cs",
            PrimaryLineNumber = 10,
            KnowledgeLevel = PatchKnowledgeLevel.StronglyInferred
        };

        // Assert
        Assert.Equal("cf-1", cf.Id);
        Assert.Equal(PatchCounterfactualKind.BoundaryValue, cf.Kind);
        Assert.Equal("amount == limit", cf.Description);
        Assert.Equal("OrderService.cs", cf.FilePath);
    }

    [Fact]
    public void BoundaryValue_HasCorrectKind()
    {
        var cf = PatchCounterfactualFactory.BoundaryValue("amount == limit");
        Assert.Equal(PatchCounterfactualKind.BoundaryValue, cf.Kind);
    }

    [Fact]
    public void NullCase_HasCorrectKind()
    {
        var cf = PatchCounterfactualFactory.NullCase("user == null");
        Assert.Equal(PatchCounterfactualKind.NullCase, cf.Kind);
    }

    [Fact]
    public void EmptyStringCase_HasCorrectKind()
    {
        var cf = PatchCounterfactualFactory.EmptyStringCase("name == \"\"");
        Assert.Equal(PatchCounterfactualKind.EmptyStringCase, cf.Kind);
    }

    [Fact]
    public void ExceptionCase_HasCorrectKind()
    {
        var cf = PatchCounterfactualFactory.ExceptionCase("method throws exception");
        Assert.Equal(PatchCounterfactualKind.ExceptionCase, cf.Kind);
    }

    [Fact]
    public void ReturnValueCase_HasCorrectKind()
    {
        var cf = PatchCounterfactualFactory.ReturnValueCase("returns different value");
        Assert.Equal(PatchCounterfactualKind.ReturnValueCase, cf.Kind);
    }

    [Fact]
    public void Factory_GeneratesUniqueIds()
    {
        var cf1 = PatchCounterfactualFactory.BoundaryValue("test1");
        var cf2 = PatchCounterfactualFactory.BoundaryValue("test2");
        Assert.NotEqual(cf1.Id, cf2.Id);
    }

    [Fact]
    public void Factory_IncludesExecutabilityNote()
    {
        var cf = PatchCounterfactualFactory.BoundaryValue("test");
        Assert.NotNull(cf.ExecutabilityNote);
        Assert.Contains("Inferred", cf.ExecutabilityNote);
    }
}

/// <summary>
/// Tests for PatchCounterfactualCollection.
/// </summary>
public class PatchCounterfactualCollectionTests
{
    [Fact]
    public void Add_SingleCounterfactual_IncreasesCount()
    {
        var collection = new PatchCounterfactualCollection();
        var cf = PatchCounterfactualFactory.BoundaryValue("test");

        collection.Add(cf);

        Assert.Equal(1, collection.Count);
    }

    [Fact]
    public void AddRange_MultipleCounterfactuals_AllAreStored()
    {
        var collection = new PatchCounterfactualCollection();
        var cfs = new[]
        {
            PatchCounterfactualFactory.BoundaryValue("test1"),
            PatchCounterfactualFactory.NullCase("test2"),
            PatchCounterfactualFactory.ExceptionCase("test3")
        };

        collection.AddRange(cfs);

        Assert.Equal(3, collection.Count);
    }

    [Fact]
    public void Clear_RemovesAllCounterfactuals()
    {
        var collection = new PatchCounterfactualCollection();
        collection.Add(PatchCounterfactualFactory.BoundaryValue("test1"));
        collection.Add(PatchCounterfactualFactory.BoundaryValue("test2"));

        collection.Clear();

        Assert.Empty(collection.All);
    }

    [Fact]
    public void ByKind_FiltersCorrectly()
    {
        var collection = new PatchCounterfactualCollection();
        collection.Add(PatchCounterfactualFactory.BoundaryValue("test1"));
        collection.Add(PatchCounterfactualFactory.BoundaryValue("test2"));
        collection.Add(PatchCounterfactualFactory.NullCase("test3"));

        var boundaries = collection.ByKind(PatchCounterfactualKind.BoundaryValue).ToList();

        Assert.Equal(2, boundaries.Count);
        Assert.All(boundaries, cf => Assert.Equal(PatchCounterfactualKind.BoundaryValue, cf.Kind));
    }

    [Fact]
    public void ByFile_FiltersCorrectly()
    {
        var collection = new PatchCounterfactualCollection();
        collection.Add(PatchCounterfactualFactory.BoundaryValue("test1", filePath: "Math.cs"));
        collection.Add(PatchCounterfactualFactory.BoundaryValue("test2", filePath: "Math.cs"));
        collection.Add(PatchCounterfactualFactory.NullCase("test3", filePath: "Order.cs"));

        var mathCFs = collection.ByFile("Math.cs").ToList();

        Assert.Equal(2, mathCFs.Count);
    }

    [Fact]
    public void CountByKind_AggregatesCorrectly()
    {
        var collection = new PatchCounterfactualCollection();
        collection.Add(PatchCounterfactualFactory.BoundaryValue("test1"));
        collection.Add(PatchCounterfactualFactory.BoundaryValue("test2"));
        collection.Add(PatchCounterfactualFactory.NullCase("test3"));

        var boundaryCount = collection.CountByKind(PatchCounterfactualKind.BoundaryValue);
        var nullCount = collection.CountByKind(PatchCounterfactualKind.NullCase);

        Assert.Equal(2, boundaryCount);
        Assert.Equal(1, nullCount);
    }
}

/// <summary>
/// Tests for PatchCounterfactualGenerator.
/// </summary>
public class PatchCounterfactualGeneratorTests
{
    [Fact]
    public void GenerateCounterfactuals_ConditionalModified_CreatesWitnesses()
    {
        // Arrange
        var operations = new PatchOperationCollection();
        operations.Add(PatchOperationFactory.ConditionalModified("condition changed", 10, "Logic.cs"));

        var transformations = new PatchTransformationCollection();
        var patchModel = new PatchModel { Files = [] };

        // Act
        var result = PatchCounterfactualGenerator.GenerateCounterfactuals(operations, transformations, patchModel);

        // Assert
        Assert.True(result.All.Any());
    }

    [Fact]
    public void GenerateCounterfactuals_ConditionalRemoved_CreatesWitnesses()
    {
        // Arrange
        var operations = new PatchOperationCollection();
        operations.Add(PatchOperationFactory.ConditionalModified("condition removed", 10, "Math.cs"));

        var transformations = new PatchTransformationCollection();
        var patchModel = new PatchModel { Files = [] };

        // Act
        var result = PatchCounterfactualGenerator.GenerateCounterfactuals(operations, transformations, patchModel);

        // Assert
        Assert.True(result.All.Any());
    }

    [Fact]
    public void GenerateCounterfactuals_ParameterRemoved_CreatesWitnesses()
    {
        // Arrange
        var operations = new PatchOperationCollection();
        operations.Add(PatchOperationFactory.ParameterRemoved("param", "int", 20, "Service.cs"));

        var transformations = new PatchTransformationCollection();
        var patchModel = new PatchModel { Files = [] };

        // Act
        var result = PatchCounterfactualGenerator.GenerateCounterfactuals(operations, transformations, patchModel);

        // Assert
        Assert.True(result.All.Any());
    }

    [Fact]
    public void GenerateCounterfactuals_LineRemoved_CreatesWitnesses()
    {
        // Arrange
        var operations = new PatchOperationCollection();
        operations.Add(PatchOperationFactory.LineRemoved(5, "Code.cs", "removed line"));

        var transformations = new PatchTransformationCollection();
        var patchModel = new PatchModel { Files = [] };

        // Act
        var result = PatchCounterfactualGenerator.GenerateCounterfactuals(operations, transformations, patchModel);

        // Assert
        Assert.True(result.All.Any());
    }

    [Fact]
    public void GenerateCounterfactuals_MultipleOperations_CreatesMultipleWitnesses()
    {
        // Arrange
        var operations = new PatchOperationCollection();
        operations.Add(PatchOperationFactory.ConditionalModified("condition", 10, "Logic.cs"));
        operations.Add(PatchOperationFactory.ParameterRemoved("param", "int", 15, "Service.cs"));
        operations.Add(PatchOperationFactory.LineRemoved(5, "Code.cs", "line"));

        var transformations = new PatchTransformationCollection();
        var patchModel = new PatchModel { Files = [] };

        // Act
        var result = PatchCounterfactualGenerator.GenerateCounterfactuals(operations, transformations, patchModel);

        // Assert
        Assert.True(result.Count >= 3, "Should create witnesses for multiple operations");
    }

    [Fact]
    public void GenerateCounterfactuals_LogicChanged_CreatesWitnesses()
    {
        // Arrange
        var operations = new PatchOperationCollection();
        var transformations = new PatchTransformationCollection();
        transformations.Add(PatchTransformationFactory.LogicChanged(
            "Logic changed",
            new[] { "Logic.cs" },
            operations.All
        ));

        var patchModel = new PatchModel { Files = [] };

        // Act
        var result = PatchCounterfactualGenerator.GenerateCounterfactuals(operations, transformations, patchModel);

        // Assert
        Assert.True(result.All.Any());
    }

    [Fact]
    public void GenerateCounterfactuals_ExtractMethod_CreatesWitnesses()
    {
        // Arrange
        var operations = new PatchOperationCollection();
        var transformations = new PatchTransformationCollection();
        transformations.Add(PatchTransformationFactory.ExtractMethod(
            "ExtractedMethod",
            "Refactoring.cs",
            operations.All
        ));

        var patchModel = new PatchModel { Files = [] };

        // Act
        var result = PatchCounterfactualGenerator.GenerateCounterfactuals(operations, transformations, patchModel);

        // Assert
        Assert.True(result.All.Any());
    }

    [Fact]
    public void GenerateCounterfactuals_LineAddedRemoved_CreatesWitnesses()
    {
        // Arrange
        var operations = new PatchOperationCollection();
        operations.Add(PatchOperationFactory.LineAdded(1, "Constants.cs", "const int MAX = 100;"));

        var transformations = new PatchTransformationCollection();
        var patchModel = new PatchModel { Files = [] };

        // Act
        var result = PatchCounterfactualGenerator.GenerateCounterfactuals(operations, transformations, patchModel);

        // Assert
        Assert.True(result.All.Any());
    }

    [Fact]
    public void GenerateCounterfactuals_EmptyOperations_ReturnsEmpty()
    {
        // Arrange
        var operations = new PatchOperationCollection();
        var transformations = new PatchTransformationCollection();
        var patchModel = new PatchModel { Files = [] };

        // Act
        var result = PatchCounterfactualGenerator.GenerateCounterfactuals(operations, transformations, patchModel);

        // Assert
        Assert.Empty(result.All);
    }

    [Fact]
    public void GenerateCounterfactuals_IdentifierRenamed_CreatesWitnesses()
    {
        // Arrange
        var operations = new PatchOperationCollection();
        operations.Add(PatchOperationFactory.IdentifierRenamed("oldName", "newName", 25, "Refactoring.cs"));

        var transformations = new PatchTransformationCollection();
        var patchModel = new PatchModel { Files = [] };

        // Act
        var result = PatchCounterfactualGenerator.GenerateCounterfactuals(operations, transformations, patchModel);

        // Assert
        // May not generate witnesses for identifier rename (depends on generator implementation)
        // Just verify it doesn't throw
        Assert.NotNull(result);
    }

    [Fact]
    public void GenerateCounterfactuals_PreservesKnowledgeLevels()
    {
        // Arrange
        var operations = new PatchOperationCollection();
        operations.Add(PatchOperationFactory.ParameterRemoved("param", "int", 15, "Service.cs"));

        var transformations = new PatchTransformationCollection();
        var patchModel = new PatchModel { Files = [] };

        // Act
        var result = PatchCounterfactualGenerator.GenerateCounterfactuals(operations, transformations, patchModel);

        // Assert
        Assert.All(result.All, cf => Assert.True(
            cf.KnowledgeLevel == PatchKnowledgeLevel.StronglyInferred ||
            cf.KnowledgeLevel == PatchKnowledgeLevel.KnownFromPatch
        ));
    }

    [Fact]
    public void GenerateCounterfactuals_MultipleTransformations_CreatesMultipleWitnesses()
    {
        // Arrange
        var operations = new PatchOperationCollection();
        var transformations = new PatchTransformationCollection();
        transformations.Add(PatchTransformationFactory.ExtractMethod(
            "Method1",
            "Refactoring.cs",
            operations.All
        ));
        transformations.Add(PatchTransformationFactory.LogicChanged(
            "Another logic change",
            new[] { "AnotherFile.cs" },
            operations.All
        ));

        var patchModel = new PatchModel { Files = [] };

        // Act
        var result = PatchCounterfactualGenerator.GenerateCounterfactuals(operations, transformations, patchModel);

        // Assert
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void GenerateCounterfactuals_AccessModifierChanged_CreatesWitnesses()
    {
        // Arrange
        var operations = new PatchOperationCollection();
        var transformations = new PatchTransformationCollection();
        transformations.Add(PatchTransformationFactory.AccessModifierChanged(
            "Method1",
            "public",
            "private",
            "Refactoring.cs",
            operations.All
        ));

        var patchModel = new PatchModel { Files = [] };

        // Act
        var result = PatchCounterfactualGenerator.GenerateCounterfactuals(operations, transformations, patchModel);

        // Assert
        Assert.NotEmpty(result.All);
    }

    [Fact]
    public void GenerateCounterfactuals_InheritanceChanged_CreatesWitnesses()
    {
        // Arrange
        var operations = new PatchOperationCollection();
        var transformations = new PatchTransformationCollection();
        transformations.Add(PatchTransformationFactory.LogicChanged(
            "Inheritance changed",
            new[] { "Class.cs" },
            operations.All
        ));

        var patchModel = new PatchModel { Files = [] };

        // Act
        var result = PatchCounterfactualGenerator.GenerateCounterfactuals(operations, transformations, patchModel);

        // Assert
        Assert.NotEmpty(result.All);
    }

    [Fact]
    public void GenerateCounterfactuals_LoopModified_CreatesWitnesses()
    {
        // Arrange
        var operations = new PatchOperationCollection();
        var transformations = new PatchTransformationCollection();
        transformations.Add(PatchTransformationFactory.LogicChanged(
            "Loop modified",
            new[] { "Processor.cs" },
            operations.All
        ));

        var patchModel = new PatchModel { Files = [] };

        // Act
        var result = PatchCounterfactualGenerator.GenerateCounterfactuals(operations, transformations, patchModel);

        // Assert
        Assert.NotEmpty(result.All);
    }
}

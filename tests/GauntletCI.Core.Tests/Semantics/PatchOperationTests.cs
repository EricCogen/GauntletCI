// SPDX-License-Identifier: Elastic-2.0
namespace GauntletCI.Core.Tests.Semantics;

using Xunit;
using GauntletCI.Core.Semantics;

public class PatchOperationTests
{
    [Fact]
    public void LineAdded_HasCorrectKind()
    {
        var op = new PatchOperation { Kind = PatchOperationKind.LineAdded };
        Assert.Equal(PatchOperationKind.LineAdded, op.Kind);
    }

    [Fact]
    public void OperatorChanged_HasCorrectKind()
    {
        var op = new PatchOperation { Kind = PatchOperationKind.OperatorChanged };
        Assert.Equal(PatchOperationKind.OperatorChanged, op.Kind);
    }

    [Fact]
    public void Operation_CanSetDescription()
    {
        var op = new PatchOperation { Description = "Test description" };
        Assert.Equal("Test description", op.Description);
    }

    [Fact]
    public void Operation_CanSetRiskLevel()
    {
        var op = new PatchOperation { RiskLevel = 0.75 };
        Assert.Equal(0.75, op.RiskLevel);
    }

    [Fact]
    public void Operation_CanSetConfidence()
    {
        var op = new PatchOperation { Confidence = 0.95 };
        Assert.Equal(0.95, op.Confidence);
    }

    [Fact]
    public void Operation_CanSetBeforeAndAfter()
    {
        var op = new PatchOperation { Before = "old", After = "new" };
        Assert.Equal("old", op.Before);
        Assert.Equal("new", op.After);
    }

    [Fact]
    public void Operation_DefaultConfidenceIsOne()
    {
        var op = new PatchOperation();
        Assert.Equal(1.0, op.Confidence);
    }

    [Fact]
    public void Operation_CanSetSymbol()
    {
        var op = new PatchOperation { Symbol = "myVar" };
        Assert.Equal("myVar", op.Symbol);
    }

    [Fact]
    public void Operation_CanSetCategory()
    {
        var op = new PatchOperation { Category = "logic" };
        Assert.Equal("logic", op.Category);
    }
}

public class PatchOperationCollectionTests
{
    [Fact]
    public void Empty_Collection_ReturnsZeroCount()
    {
        var collection = new PatchOperationCollection();
        Assert.Equal(0, collection.Count);
    }

    [Fact]
    public void Empty_Collection_AllIsEmpty()
    {
        var collection = new PatchOperationCollection();
        Assert.Empty(collection.All);
    }

    [Fact]
    public void Add_SingleOperation_IncreasesCount()
    {
        var collection = new PatchOperationCollection();
        var op = new PatchOperation { Kind = PatchOperationKind.LineAdded };

        collection.Add(op);

        Assert.Equal(1, collection.Count);
        Assert.Single(collection.All);
    }

    [Fact]
    public void Add_MultipleOperations_AllAreStored()
    {
        var collection = new PatchOperationCollection();
        var op1 = new PatchOperation { Kind = PatchOperationKind.LineAdded };
        var op2 = new PatchOperation { Kind = PatchOperationKind.LineRemoved };

        collection.Add(op1);
        collection.Add(op2);

        Assert.Equal(2, collection.Count);
    }

    [Fact]
    public void AddRange_Multiple_Operations_AllAreStored()
    {
        var collection = new PatchOperationCollection();
        var ops = new[]
        {
            new PatchOperation { Kind = PatchOperationKind.LineAdded },
            new PatchOperation { Kind = PatchOperationKind.OperatorChanged },
            new PatchOperation { Kind = PatchOperationKind.IdentifierRenamed }
        };

        collection.AddRange(ops);

        Assert.Equal(3, collection.Count);
    }

    [Fact]
    public void ByKind_FiltersCorrectly()
    {
        var collection = new PatchOperationCollection();
        collection.Add(new PatchOperation { Kind = PatchOperationKind.LineAdded });
        collection.Add(new PatchOperation { Kind = PatchOperationKind.LineAdded });
        collection.Add(new PatchOperation { Kind = PatchOperationKind.LineRemoved });

        var added = collection.ByKind(PatchOperationKind.LineAdded).ToList();

        Assert.Equal(2, added.Count);
    }

    [Fact]
    public void ByFile_FiltersCorrectly()
    {
        var collection = new PatchOperationCollection();
        collection.Add(new PatchOperation { FilePath = "a.cs" });
        collection.Add(new PatchOperation { FilePath = "a.cs" });
        collection.Add(new PatchOperation { FilePath = "b.cs" });

        var fromA = collection.ByFile("a.cs").ToList();

        Assert.Equal(2, fromA.Count);
    }

    [Fact]
    public void BySymbol_FiltersCorrectly()
    {
        var collection = new PatchOperationCollection();
        collection.Add(new PatchOperation { Symbol = "myVar" });
        collection.Add(new PatchOperation { Symbol = "myVar" });
        collection.Add(new PatchOperation { Symbol = "otherVar" });

        var myVarOps = collection.BySymbol("myVar").ToList();

        Assert.Equal(2, myVarOps.Count);
    }

    [Fact]
    public void ByCategory_FiltersCorrectly()
    {
        var collection = new PatchOperationCollection();
        collection.Add(new PatchOperation { Category = "logic" });
        collection.Add(new PatchOperation { Category = "logic" });
        collection.Add(new PatchOperation { Category = "performance" });

        var logic = collection.ByCategory("logic").ToList();

        Assert.Equal(2, logic.Count);
    }

    [Fact]
    public void ByMinRisk_FiltersCorrectly()
    {
        var collection = new PatchOperationCollection();
        collection.Add(new PatchOperation { RiskLevel = 0.5 });
        collection.Add(new PatchOperation { RiskLevel = 0.8 });
        collection.Add(new PatchOperation { RiskLevel = 0.9 });

        var highRisk = collection.ByMinRisk(0.75).ToList();

        Assert.Equal(2, highRisk.Count);
    }

    [Fact]
    public void ByMinConfidence_FiltersCorrectly()
    {
        var collection = new PatchOperationCollection();
        collection.Add(new PatchOperation { Confidence = 0.7 });
        collection.Add(new PatchOperation { Confidence = 0.9 });
        collection.Add(new PatchOperation { Confidence = 0.95 });

        var confident = collection.ByMinConfidence(0.85).ToList();

        Assert.Equal(2, confident.Count);
    }

    [Fact]
    public void CountByKind_AggregatesCorrectly()
    {
        var collection = new PatchOperationCollection();
        collection.Add(new PatchOperation { Kind = PatchOperationKind.LineAdded });
        collection.Add(new PatchOperation { Kind = PatchOperationKind.LineAdded });
        collection.Add(new PatchOperation { Kind = PatchOperationKind.OperatorChanged });

        var counts = collection.CountByKind();

        Assert.Equal(2, counts[PatchOperationKind.LineAdded]);
        Assert.Equal(1, counts[PatchOperationKind.OperatorChanged]);
    }

    [Fact]
    public void AggregateRisk_Max_ReturnsMaximum()
    {
        var collection = new PatchOperationCollection();
        collection.Add(new PatchOperation { RiskLevel = 0.5 });
        collection.Add(new PatchOperation { RiskLevel = 0.8 });
        collection.Add(new PatchOperation { RiskLevel = 0.6 });

        var maxRisk = collection.AggregateRisk("max");

        Assert.Equal(0.8, maxRisk);
    }

    [Fact]
    public void AggregateRisk_Mean_ReturnsMean()
    {
        var collection = new PatchOperationCollection();
        collection.Add(new PatchOperation { RiskLevel = 0.5 });
        collection.Add(new PatchOperation { RiskLevel = 0.6 });
        collection.Add(new PatchOperation { RiskLevel = 0.7 });

        var meanRisk = collection.AggregateRisk("mean");

        Assert.Equal(0.6, meanRisk);
    }

    [Fact]
    public void AggregateRisk_OnEmpty_ReturnsZero()
    {
        var collection = new PatchOperationCollection();

        var risk = collection.AggregateRisk("max");

        Assert.Equal(0, risk);
    }

    [Fact]
    public void Clear_RemovesAllOperations()
    {
        var collection = new PatchOperationCollection();
        collection.Add(new PatchOperation());
        collection.Add(new PatchOperation());

        collection.Clear();

        Assert.Equal(0, collection.Count);
    }
}

public class PatchOperationFactoryTests
{
    [Fact]
    public void LineAdded_CreatesCorrectOperation()
    {
        var op = PatchOperationFactory.LineAdded(42, "file.cs", "int x = 5;");

        Assert.Equal(PatchOperationKind.LineAdded, op.Kind);
        Assert.Equal(42, op.NewLineNumber);
        Assert.Equal("file.cs", op.FilePath);
        Assert.Equal("int x = 5;", op.After);
        Assert.Equal(0.3, op.RiskLevel);
    }

    [Fact]
    public void LineRemoved_CreatesCorrectOperation()
    {
        var op = PatchOperationFactory.LineRemoved(42, "file.cs", "int x = 5;");

        Assert.Equal(PatchOperationKind.LineRemoved, op.Kind);
        Assert.Equal(42, op.OldLineNumber);
        Assert.Equal("file.cs", op.FilePath);
        Assert.Equal("int x = 5;", op.Before);
        Assert.Equal(0.3, op.RiskLevel);
    }

    [Fact]
    public void IdentifierRenamed_CreatesCorrectOperation()
    {
        var op = PatchOperationFactory.IdentifierRenamed("count", "total", 42, "file.cs");

        Assert.Equal(PatchOperationKind.IdentifierRenamed, op.Kind);
        Assert.Equal("count", op.Before);
        Assert.Equal("total", op.After);
        Assert.Equal("total", op.Symbol);
        Assert.Equal(0.6, op.RiskLevel);
    }

    [Fact]
    public void OperatorChanged_CreatesCorrectOperation()
    {
        var op = PatchOperationFactory.OperatorChanged("+", "-", 42, "file.cs");

        Assert.Equal(PatchOperationKind.OperatorChanged, op.Kind);
        Assert.Equal("+", op.Before);
        Assert.Equal("-", op.After);
        Assert.Equal(0.8, op.RiskLevel);
    }

    [Fact]
    public void LiteralChanged_CreatesCorrectOperation()
    {
        var op = PatchOperationFactory.LiteralChanged("10", "20", 42, "file.cs");

        Assert.Equal(PatchOperationKind.LiteralChanged, op.Kind);
        Assert.Equal("10", op.Before);
        Assert.Equal("20", op.After);
        Assert.Equal(0.7, op.RiskLevel);
    }

    [Fact]
    public void FunctionSignatureChanged_CreatesCorrectOperation()
    {
        var op = PatchOperationFactory.FunctionSignatureChanged("Calculate", "int Calculate()", "int Calculate(int x)", 42, "file.cs");

        Assert.Equal(PatchOperationKind.FunctionSignatureChanged, op.Kind);
        Assert.Equal("Calculate", op.Symbol);
        Assert.Equal(0.9, op.RiskLevel);
    }

    [Fact]
    public void ConditionalModified_CreatesCorrectOperation()
    {
        var op = PatchOperationFactory.ConditionalModified("if condition changed", 42, "file.cs");

        Assert.Equal(PatchOperationKind.ConditionalModified, op.Kind);
        Assert.Equal(0.85, op.RiskLevel);
    }

    [Fact]
    public void ParameterAdded_CreatesCorrectOperation()
    {
        var op = PatchOperationFactory.ParameterAdded("timeout", "int", 42, "file.cs");

        Assert.Equal(PatchOperationKind.ParameterAdded, op.Kind);
        Assert.Equal("timeout", op.Symbol);
        Assert.Equal(0.75, op.RiskLevel);
    }

    [Fact]
    public void ParameterRemoved_CreatesCorrectOperation()
    {
        var op = PatchOperationFactory.ParameterRemoved("legacyFlag", "bool", 42, "file.cs");

        Assert.Equal(PatchOperationKind.ParameterRemoved, op.Kind);
        Assert.Equal("legacyFlag", op.Symbol);
        Assert.Equal(0.75, op.RiskLevel);
    }

    [Fact]
    public void Factory_CanOverrideRiskLevel()
    {
        var op = PatchOperationFactory.LineAdded(42, "file.cs", "code", risk: 0.9);

        Assert.Equal(0.9, op.RiskLevel);
    }
}

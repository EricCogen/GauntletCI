using GauntletCI.Core.Evaluation;
using GauntletCI.Core.Models;

namespace GauntletCI.Core.Tests;

public sealed class ContextAssemblerTestSignalsTests
{
    [Fact]
    public void Assemble_FlagsTestsChangedWithoutAssertions()
    {
        const string diff = """
            diff --git a/src/OrderProcessor.cs b/src/OrderProcessor.cs
            index 1111111..2222222 100644
            --- a/src/OrderProcessor.cs
            +++ b/src/OrderProcessor.cs
            @@ -1,3 +1,3 @@
             public sealed class OrderProcessor {
            -    public int Next() => 0;
            +    public int Next() => 1;
             }
            diff --git a/tests/OrderProcessorTests.cs b/tests/OrderProcessorTests.cs
            index 1111111..2222222 100644
            --- a/tests/OrderProcessorTests.cs
            +++ b/tests/OrderProcessorTests.cs
            @@ -1,3 +1,6 @@
             public sealed class OrderProcessorTests {
            +    [Fact]
            +    public void Setup_fixture() { var mock = new object(); }
             }
            """;

        ContextAssembler assembler = new();
        GateResult branch = GateResult.Pass("Branch Currency", "ok");
        GateResult test = GateResult.Pass("Test Passage", "ok");
        GauntletConfig config = new();

        AssembledContext assembled = assembler.Assemble(branch, test, diff, config, ["feat: update order path"]);

        Assert.True(assembled.Metadata.TestsChangedWithoutAssertions);
        Assert.Equal(1, assembled.Metadata.TestFilesChanged);
        Assert.Equal(0, assembled.Metadata.TestAssertionLinesAdded);
        Assert.Contains("tests_changed_without_new_assertions: True", assembled.Context, StringComparison.Ordinal);
    }

    [Fact]
    public void Assemble_FlagsRenameOnlyTestChurn()
    {
        const string diff = """
            diff --git a/tests/LegacyOrderTests.cs b/tests/OrderWorkflowTests.cs
            similarity index 100%
            rename from tests/LegacyOrderTests.cs
            rename to tests/OrderWorkflowTests.cs
            """;

        ContextAssembler assembler = new();
        GateResult branch = GateResult.Pass("Branch Currency", "ok");
        GateResult test = GateResult.Pass("Test Passage", "ok");
        GauntletConfig config = new();

        AssembledContext assembled = assembler.Assemble(branch, test, diff, config, ["chore: rename test files"]);

        Assert.Equal(1, assembled.Metadata.TestFilesChanged);
        Assert.Equal(1, assembled.Metadata.TestFilesRenameOnly);
        Assert.True(assembled.Metadata.TestsChangedWithoutAssertions);
        Assert.True(assembled.Metadata.TestChangesAreRenameOrSetupChurn);
    }

    [Fact]
    public void Assemble_DetectsAddedAssertionSignals()
    {
        const string diff = """
            diff --git a/tests/OrderProcessorTests.cs b/tests/OrderProcessorTests.cs
            index 1111111..2222222 100644
            --- a/tests/OrderProcessorTests.cs
            +++ b/tests/OrderProcessorTests.cs
            @@ -4,0 +5,2 @@
            +    int result = sut.Next();
            +    Assert.Equal(1, result);
            """;

        ContextAssembler assembler = new();
        GateResult branch = GateResult.Pass("Branch Currency", "ok");
        GateResult test = GateResult.Pass("Test Passage", "ok");
        GauntletConfig config = new();

        AssembledContext assembled = assembler.Assemble(branch, test, diff, config, ["test: add assertion"]);

        Assert.Equal(1, assembled.Metadata.TestAssertionLinesAdded);
        Assert.False(assembled.Metadata.TestsChangedWithoutAssertions);
    }
}

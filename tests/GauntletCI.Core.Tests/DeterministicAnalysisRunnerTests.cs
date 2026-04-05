using GauntletCI.Core.Evaluation;
using GauntletCI.Core.Models;

namespace GauntletCI.Core.Tests;

public sealed class DeterministicAnalysisRunnerTests
{
    [Fact]
    public void Analyze_FlagsWeakTestCoverage_WhenProductionChangesWithoutAssertions()
    {
        DeterministicAnalysisRunner runner = new();
        DiffMetadata metadata = CreateMetadata(
            linesAdded: 20,
            linesRemoved: 4,
            filesChanged: 3,
            testFilesChanged: 1,
            testAssertionLinesAdded: 0,
            testsChangedWithoutAssertions: true,
            languages: ["csharp"]);

        IReadOnlyList<Finding> findings = runner.Analyze("diff --git a/src/A.cs b/src/A.cs", metadata);

        Assert.Contains(findings, finding => finding.RuleId == "GCI005");
    }

    [Fact]
    public void Analyze_FlagsAsyncBlockingPattern()
    {
        const string diff = """
            diff --git a/src/OrderController.cs b/src/OrderController.cs
            index 1111111..2222222 100644
            --- a/src/OrderController.cs
            +++ b/src/OrderController.cs
            @@ -10,0 +11,2 @@
            +var order = repository.LoadAsync(id).Result;
            +return order;
            """;

        DeterministicAnalysisRunner runner = new();
        DiffMetadata metadata = CreateMetadata(languages: ["csharp"]);

        IReadOnlyList<Finding> findings = runner.Analyze(diff, metadata);

        Assert.Contains(findings, finding => finding.RuleId == "GCI016");
    }

    [Fact]
    public void Analyze_FlagsDestructiveSchemaPattern()
    {
        const string diff = """
            diff --git a/migrations/001_drop_column.sql b/migrations/001_drop_column.sql
            index 1111111..2222222 100644
            --- a/migrations/001_drop_column.sql
            +++ b/migrations/001_drop_column.sql
            @@ -1,0 +1,1 @@
            +ALTER TABLE users DROP COLUMN legacy_name;
            """;

        DeterministicAnalysisRunner runner = new();
        DiffMetadata metadata = CreateMetadata(languages: ["sql"]);

        IReadOnlyList<Finding> findings = runner.Analyze(diff, metadata);

        Assert.Contains(findings, finding => finding.RuleId == "GCI014");
    }

    [Fact]
    public void Analyze_FlagsHardcodedSecretPattern()
    {
        const string diff = """
            diff --git a/src/AppConfig.cs b/src/AppConfig.cs
            index 1111111..2222222 100644
            --- a/src/AppConfig.cs
            +++ b/src/AppConfig.cs
            @@ -1,0 +1,1 @@
            +const string apiKey = "sk-test-value";
            """;

        DeterministicAnalysisRunner runner = new();
        DiffMetadata metadata = CreateMetadata(languages: ["csharp"]);

        IReadOnlyList<Finding> findings = runner.Analyze(diff, metadata);

        Assert.Contains(findings, finding => finding.RuleId == "GCI012");
    }

    private static DiffMetadata CreateMetadata(
        int linesAdded = 8,
        int linesRemoved = 2,
        int filesChanged = 1,
        int testFilesChanged = 0,
        int testAssertionLinesAdded = 0,
        bool testsChangedWithoutAssertions = false,
        IReadOnlyList<string>? languages = null)
    {
        return new DiffMetadata(
            LinesAdded: linesAdded,
            LinesRemoved: linesRemoved,
            FilesChanged: filesChanged,
            TestFilesTouched: testFilesChanged > 0,
            TestFilesChanged: testFilesChanged,
            TestFilesWithContentChanges: testFilesChanged,
            TestFilesRenameOnly: 0,
            TestLinesAdded: testFilesChanged > 0 ? linesAdded : 0,
            TestLinesRemoved: testFilesChanged > 0 ? linesRemoved : 0,
            TestAssertionLinesAdded: testAssertionLinesAdded,
            TestSetupLinesAdded: testFilesChanged > 0 ? Math.Max(0, linesAdded - testAssertionLinesAdded) : 0,
            TestsChangedWithoutAssertions: testsChangedWithoutAssertions,
            TestChangesAreRenameOrSetupChurn: false,
            Languages: languages ?? ["csharp"],
            DiffTrimmed: false,
            EstimatedTokens: 100);
    }
}

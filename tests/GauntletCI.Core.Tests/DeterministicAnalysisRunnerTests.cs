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

    [Fact]
    public void Analyze_FlagsWeakCryptoPattern()
    {
        const string diff = """
            diff --git a/src/Security/Crypto.cs b/src/Security/Crypto.cs
            index 1111111..2222222 100644
            --- a/src/Security/Crypto.cs
            +++ b/src/Security/Crypto.cs
            @@ -3,0 +4,1 @@
            +var digest = MD5.Create();
            """;

        DeterministicAnalysisRunner runner = new();
        DiffMetadata metadata = CreateMetadata(languages: ["csharp"]);

        IReadOnlyList<Finding> findings = runner.Analyze(diff, metadata);

        Assert.Contains(findings, finding => finding.Evidence.Contains("signal=DET-SEC-003", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_DoesNotFlagSecretPlaceholderPattern()
    {
        const string diff = """
            diff --git a/src/AppConfig.cs b/src/AppConfig.cs
            index 1111111..2222222 100644
            --- a/src/AppConfig.cs
            +++ b/src/AppConfig.cs
            @@ -1,0 +1,1 @@
            +const string apiKey = "YOUR_API_KEY";
            """;

        DeterministicAnalysisRunner runner = new();
        DiffMetadata metadata = CreateMetadata(languages: ["csharp"]);

        IReadOnlyList<Finding> findings = runner.Analyze(diff, metadata);

        Assert.DoesNotContain(findings, finding => finding.Evidence.Contains("signal=DET-SEC-001", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_FlagsSqlStringConcatenationPattern()
    {
        const string diff = """
            diff --git a/src/Data/UserRepository.cs b/src/Data/UserRepository.cs
            index 1111111..2222222 100644
            --- a/src/Data/UserRepository.cs
            +++ b/src/Data/UserRepository.cs
            @@ -8,0 +9,1 @@
            +var sql = "SELECT * FROM users WHERE id = " + userId;
            """;

        DeterministicAnalysisRunner runner = new();
        DiffMetadata metadata = CreateMetadata(languages: ["csharp"]);

        IReadOnlyList<Finding> findings = runner.Analyze(diff, metadata);

        Assert.Contains(findings, finding => finding.Evidence.Contains("signal=DET-SEC-002", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_FlagsEmptyCatchPattern()
    {
        const string diff = """
            diff --git a/src/Workers/SyncWorker.cs b/src/Workers/SyncWorker.cs
            index 1111111..2222222 100644
            --- a/src/Workers/SyncWorker.cs
            +++ b/src/Workers/SyncWorker.cs
            @@ -12,0 +13,1 @@
            +try { Sync(); } catch (Exception ex) { }
            """;

        DeterministicAnalysisRunner runner = new();
        DiffMetadata metadata = CreateMetadata(languages: ["csharp"]);

        IReadOnlyList<Finding> findings = runner.Analyze(diff, metadata);

        Assert.Contains(findings, finding => finding.Evidence.Contains("signal=DET-REL-001", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_FlagsAsyncVoidMethodPattern()
    {
        const string diff = """
            diff --git a/src/Api/OrdersController.cs b/src/Api/OrdersController.cs
            index 1111111..2222222 100644
            --- a/src/Api/OrdersController.cs
            +++ b/src/Api/OrdersController.cs
            @@ -11,0 +12,1 @@
            +public async void SubmitOrder(Guid id) { await _service.SubmitAsync(id); }
            """;

        DeterministicAnalysisRunner runner = new();
        DiffMetadata metadata = CreateMetadata(languages: ["csharp"]);

        IReadOnlyList<Finding> findings = runner.Analyze(diff, metadata);

        Assert.Contains(findings, finding => finding.Evidence.Contains("signal=DET-CONC-002", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_DoesNotFlagAsyncTaskMethodPattern()
    {
        const string diff = """
            diff --git a/src/Api/OrdersController.cs b/src/Api/OrdersController.cs
            index 1111111..2222222 100644
            --- a/src/Api/OrdersController.cs
            +++ b/src/Api/OrdersController.cs
            @@ -11,0 +12,1 @@
            +public async Task SubmitOrder(Guid id) { await _service.SubmitAsync(id); }
            """;

        DeterministicAnalysisRunner runner = new();
        DiffMetadata metadata = CreateMetadata(languages: ["csharp"]);

        IReadOnlyList<Finding> findings = runner.Analyze(diff, metadata);

        Assert.DoesNotContain(findings, finding => finding.Evidence.Contains("signal=DET-CONC-002", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_FlagsTlsValidationBypassPattern()
    {
        const string diff = """
            diff --git a/src/Networking/HttpClientFactory.cs b/src/Networking/HttpClientFactory.cs
            index 1111111..2222222 100644
            --- a/src/Networking/HttpClientFactory.cs
            +++ b/src/Networking/HttpClientFactory.cs
            @@ -6,0 +7,1 @@
            +handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
            """;

        DeterministicAnalysisRunner runner = new();
        DiffMetadata metadata = CreateMetadata(languages: ["csharp"]);

        IReadOnlyList<Finding> findings = runner.Analyze(diff, metadata);

        Assert.Contains(findings, finding => finding.Evidence.Contains("signal=DET-SEC-005", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_FlagsInsecureDeserializerPattern()
    {
        const string diff = """
            diff --git a/src/Serialization/LegacyDeserializer.cs b/src/Serialization/LegacyDeserializer.cs
            index 1111111..2222222 100644
            --- a/src/Serialization/LegacyDeserializer.cs
            +++ b/src/Serialization/LegacyDeserializer.cs
            @@ -4,0 +5,1 @@
            +var formatter = new BinaryFormatter();
            """;

        DeterministicAnalysisRunner runner = new();
        DiffMetadata metadata = CreateMetadata(languages: ["csharp"]);

        IReadOnlyList<Finding> findings = runner.Analyze(diff, metadata);

        Assert.Contains(findings, finding => finding.Evidence.Contains("signal=DET-SEC-006", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_FlagsJwtValidationDisabledPattern()
    {
        const string diff = """
            diff --git a/src/Auth/JwtSetup.cs b/src/Auth/JwtSetup.cs
            index 1111111..2222222 100644
            --- a/src/Auth/JwtSetup.cs
            +++ b/src/Auth/JwtSetup.cs
            @@ -8,0 +9,1 @@
            +tokenValidationParameters.ValidateLifetime = false;
            """;

        DeterministicAnalysisRunner runner = new();
        DiffMetadata metadata = CreateMetadata(languages: ["csharp"]);

        IReadOnlyList<Finding> findings = runner.Analyze(diff, metadata);

        Assert.Contains(findings, finding => finding.Evidence.Contains("signal=DET-SEC-007", StringComparison.Ordinal));
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

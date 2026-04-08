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

    // ── New added-line signals ───────────────────────────────────────────────

    [Fact]
    public void Analyze_FlagsThreadStaticAttribute()
    {
        const string diff = """
            diff --git a/src/Cache/RequestCache.cs b/src/Cache/RequestCache.cs
            index 1111111..2222222 100644
            --- a/src/Cache/RequestCache.cs
            +++ b/src/Cache/RequestCache.cs
            @@ -3,0 +4,2 @@
            +[ThreadStatic]
            +private static List<string> _buffer;
            """;

        IReadOnlyList<Finding> findings = new DeterministicAnalysisRunner().Analyze(diff, CreateMetadata());

        Assert.Contains(findings, f => f.Evidence.Contains("signal=DET-CONC-003", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_FlagsGcSuppressFinalizeAdded()
    {
        const string diff = """
            diff --git a/src/IO/FileHandle.cs b/src/IO/FileHandle.cs
            index 1111111..2222222 100644
            --- a/src/IO/FileHandle.cs
            +++ b/src/IO/FileHandle.cs
            @@ -10,0 +11,1 @@
            +GC.SuppressFinalize(this);
            """;

        IReadOnlyList<Finding> findings = new DeterministicAnalysisRunner().Analyze(diff, CreateMetadata());

        Assert.Contains(findings, f => f.Evidence.Contains("signal=DET-DISP-002", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_FlagsNoInliningAdded()
    {
        const string diff = """
            diff --git a/src/Hot/Serializer.cs b/src/Hot/Serializer.cs
            index 1111111..2222222 100644
            --- a/src/Hot/Serializer.cs
            +++ b/src/Hot/Serializer.cs
            @@ -5,0 +6,1 @@
            +[MethodImpl(MethodImplOptions.NoInlining)]
            """;

        IReadOnlyList<Finding> findings = new DeterministicAnalysisRunner().Analyze(diff, CreateMetadata());

        Assert.Contains(findings, f => f.Evidence.Contains("signal=DET-PERF-004", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_FlagsSealedClassAdded()
    {
        const string diff = """
            diff --git a/src/Models/Order.cs b/src/Models/Order.cs
            index 1111111..2222222 100644
            --- a/src/Models/Order.cs
            +++ b/src/Models/Order.cs
            @@ -1,1 +1,1 @@
            -public class Order
            +public sealed class Order
            """;

        IReadOnlyList<Finding> findings = new DeterministicAnalysisRunner().Analyze(diff, CreateMetadata());

        Assert.Contains(findings, f => f.Evidence.Contains("signal=DET-API-004", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_FlagsIgnoreAttributeInTestFile()
    {
        const string diff = """
            diff --git a/tests/OrderServiceTests.cs b/tests/OrderServiceTests.cs
            index 1111111..2222222 100644
            --- a/tests/OrderServiceTests.cs
            +++ b/tests/OrderServiceTests.cs
            @@ -8,0 +9,1 @@
            +[Ignore]
            """;

        IReadOnlyList<Finding> findings = new DeterministicAnalysisRunner().Analyze(diff, CreateMetadata());

        Assert.Contains(findings, f => f.Evidence.Contains("signal=DET-TEST-002", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_DoesNotFlagIgnoreAttributeOutsideTestFile()
    {
        const string diff = """
            diff --git a/src/Attributes/Ignore.cs b/src/Attributes/Ignore.cs
            index 1111111..2222222 100644
            --- a/src/Attributes/Ignore.cs
            +++ b/src/Attributes/Ignore.cs
            @@ -1,0 +2,1 @@
            +[Ignore]
            """;

        IReadOnlyList<Finding> findings = new DeterministicAnalysisRunner().Analyze(diff, CreateMetadata());

        Assert.DoesNotContain(findings, f => f.Evidence.Contains("signal=DET-TEST-002", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_FlagsAllowAnonymousAdded()
    {
        const string diff = """
            diff --git a/src/Controllers/AdminController.cs b/src/Controllers/AdminController.cs
            index 1111111..2222222 100644
            --- a/src/Controllers/AdminController.cs
            +++ b/src/Controllers/AdminController.cs
            @@ -4,0 +5,1 @@
            +[AllowAnonymous]
            """;

        IReadOnlyList<Finding> findings = new DeterministicAnalysisRunner().Analyze(diff, CreateMetadata());

        Assert.Contains(findings, f => f.Evidence.Contains("signal=DET-SEC-011", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_FlagsHttpUrlDowngrade()
    {
        const string diff = """
            diff --git a/src/Config/EndpointConfig.cs b/src/Config/EndpointConfig.cs
            index 1111111..2222222 100644
            --- a/src/Config/EndpointConfig.cs
            +++ b/src/Config/EndpointConfig.cs
            @@ -2,1 +2,1 @@
            -string baseUrl = "https://api.example.com";
            +string baseUrl = "http://api.example.com";
            """;

        IReadOnlyList<Finding> findings = new DeterministicAnalysisRunner().Analyze(diff, CreateMetadata());

        Assert.Contains(findings, f => f.Evidence.Contains("signal=DET-SEC-012", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_DoesNotFlagLocalhostHttpUrl()
    {
        const string diff = """
            diff --git a/src/Config/DevConfig.cs b/src/Config/DevConfig.cs
            index 1111111..2222222 100644
            --- a/src/Config/DevConfig.cs
            +++ b/src/Config/DevConfig.cs
            @@ -2,0 +3,1 @@
            +string devUrl = "http://localhost:5000";
            """;

        IReadOnlyList<Finding> findings = new DeterministicAnalysisRunner().Analyze(diff, CreateMetadata());

        Assert.DoesNotContain(findings, f => f.Evidence.Contains("signal=DET-SEC-012", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_FlagsProcessStartAdded()
    {
        const string diff = """
            diff --git a/src/Exec/ShellRunner.cs b/src/Exec/ShellRunner.cs
            index 1111111..2222222 100644
            --- a/src/Exec/ShellRunner.cs
            +++ b/src/Exec/ShellRunner.cs
            @@ -5,0 +6,1 @@
            +Process.Start(fileName, userInput);
            """;

        IReadOnlyList<Finding> findings = new DeterministicAnalysisRunner().Analyze(diff, CreateMetadata());

        Assert.Contains(findings, f => f.Evidence.Contains("signal=DET-SEC-013", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_FlagsPragmaWarningDisableAdded()
    {
        const string diff = """
            diff --git a/src/Legacy/OldCode.cs b/src/Legacy/OldCode.cs
            index 1111111..2222222 100644
            --- a/src/Legacy/OldCode.cs
            +++ b/src/Legacy/OldCode.cs
            @@ -1,0 +2,1 @@
            +#pragma warning disable CS0618
            """;

        IReadOnlyList<Finding> findings = new DeterministicAnalysisRunner().Analyze(diff, CreateMetadata());

        Assert.Contains(findings, f => f.Evidence.Contains("signal=DET-QUAL-002", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_FlagsDynamicKeywordAdded()
    {
        const string diff = """
            diff --git a/src/Interop/Bridge.cs b/src/Interop/Bridge.cs
            index 1111111..2222222 100644
            --- a/src/Interop/Bridge.cs
            +++ b/src/Interop/Bridge.cs
            @@ -7,0 +8,1 @@
            +dynamic result = GetPayload();
            """;

        IReadOnlyList<Finding> findings = new DeterministicAnalysisRunner().Analyze(diff, CreateMetadata());

        Assert.Contains(findings, f => f.Evidence.Contains("signal=DET-QUAL-004", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_FlagsGotoAdded()
    {
        const string diff = """
            diff --git a/src/Parsing/Parser.cs b/src/Parsing/Parser.cs
            index 1111111..2222222 100644
            --- a/src/Parsing/Parser.cs
            +++ b/src/Parsing/Parser.cs
            @@ -20,0 +21,1 @@
            +goto retry;
            """;

        IReadOnlyList<Finding> findings = new DeterministicAnalysisRunner().Analyze(diff, CreateMetadata());

        Assert.Contains(findings, f => f.Evidence.Contains("signal=DET-QUAL-005", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_FlagsTimeoutInfiniteAdded()
    {
        const string diff = """
            diff --git a/src/Http/Client.cs b/src/Http/Client.cs
            index 1111111..2222222 100644
            --- a/src/Http/Client.cs
            +++ b/src/Http/Client.cs
            @@ -3,1 +3,1 @@
            -client.Timeout = TimeSpan.FromSeconds(30);
            +client.Timeout = Timeout.Infinite;
            """;

        IReadOnlyList<Finding> findings = new DeterministicAnalysisRunner().Analyze(diff, CreateMetadata());

        Assert.Contains(findings, f => f.Evidence.Contains("signal=DET-INFRA-002", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_FlagsEnvironmentExitAdded()
    {
        const string diff = """
            diff --git a/src/Workers/BackgroundWorker.cs b/src/Workers/BackgroundWorker.cs
            index 1111111..2222222 100644
            --- a/src/Workers/BackgroundWorker.cs
            +++ b/src/Workers/BackgroundWorker.cs
            @@ -14,0 +15,1 @@
            +Environment.Exit(1);
            """;

        IReadOnlyList<Finding> findings = new DeterministicAnalysisRunner().Analyze(diff, CreateMetadata());

        Assert.Contains(findings, f => f.Evidence.Contains("signal=DET-INFRA-004", StringComparison.Ordinal));
    }

    // ── New removed-line signals ─────────────────────────────────────────────

    [Fact]
    public void Analyze_FlagsLockRemoved()
    {
        const string diff = """
            diff --git a/src/Cache/MemoryCache.cs b/src/Cache/MemoryCache.cs
            index 1111111..2222222 100644
            --- a/src/Cache/MemoryCache.cs
            +++ b/src/Cache/MemoryCache.cs
            @@ -8,2 +8,1 @@
            -lock (_syncRoot)
            -{
            +{
            """;

        IReadOnlyList<Finding> findings = new DeterministicAnalysisRunner().Analyze(diff, CreateMetadata());

        Assert.Contains(findings, f => f.Evidence.Contains("signal=DET-CONC-004", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_FlagsConcurrentDictionaryRemoved()
    {
        const string diff = """
            diff --git a/src/State/SessionStore.cs b/src/State/SessionStore.cs
            index 1111111..2222222 100644
            --- a/src/State/SessionStore.cs
            +++ b/src/State/SessionStore.cs
            @@ -3,1 +3,1 @@
            -private ConcurrentDictionary<string, Session> _sessions = new();
            +private Dictionary<string, Session> _sessions = new();
            """;

        IReadOnlyList<Finding> findings = new DeterministicAnalysisRunner().Analyze(diff, CreateMetadata());

        Assert.Contains(findings, f => f.Evidence.Contains("signal=DET-CONC-006", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_FlagsVolatileRemoved()
    {
        const string diff = """
            diff --git a/src/Infra/Heartbeat.cs b/src/Infra/Heartbeat.cs
            index 1111111..2222222 100644
            --- a/src/Infra/Heartbeat.cs
            +++ b/src/Infra/Heartbeat.cs
            @@ -2,1 +2,1 @@
            -private volatile bool _running;
            +private bool _running;
            """;

        IReadOnlyList<Finding> findings = new DeterministicAnalysisRunner().Analyze(diff, CreateMetadata());

        Assert.Contains(findings, f => f.Evidence.Contains("signal=DET-CONC-008", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_FlagsReadonlyRemoved()
    {
        const string diff = """
            diff --git a/src/Config/Settings.cs b/src/Config/Settings.cs
            index 1111111..2222222 100644
            --- a/src/Config/Settings.cs
            +++ b/src/Config/Settings.cs
            @@ -4,1 +4,1 @@
            -private readonly List<string> _rules;
            +private List<string> _rules;
            """;

        IReadOnlyList<Finding> findings = new DeterministicAnalysisRunner().Analyze(diff, CreateMetadata());

        Assert.Contains(findings, f => f.Evidence.Contains("signal=DET-MUTAT-001", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_FlagsFinallyBlockRemoved()
    {
        const string diff = """
            diff --git a/src/Data/DbCommand.cs b/src/Data/DbCommand.cs
            index 1111111..2222222 100644
            --- a/src/Data/DbCommand.cs
            +++ b/src/Data/DbCommand.cs
            @@ -12,3 +12,0 @@
            -finally
            -{
            -    connection.Dispose();
            -}
            """;

        IReadOnlyList<Finding> findings = new DeterministicAnalysisRunner().Analyze(diff, CreateMetadata());

        Assert.Contains(findings, f => f.Evidence.Contains("signal=DET-REL-004", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_FlagsRetryLogicRemoved()
    {
        const string diff = """
            diff --git a/src/Http/ApiClient.cs b/src/Http/ApiClient.cs
            index 1111111..2222222 100644
            --- a/src/Http/ApiClient.cs
            +++ b/src/Http/ApiClient.cs
            @@ -5,2 +5,0 @@
            -var retryPolicy = Policy.Handle<HttpRequestException>().Retry(3);
            -await retryPolicy.ExecuteAsync(() => SendAsync(request));
            """;

        IReadOnlyList<Finding> findings = new DeterministicAnalysisRunner().Analyze(diff, CreateMetadata());

        Assert.Contains(findings, f => f.Evidence.Contains("signal=DET-REL-005", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_FlagsCircuitBreakerRemoved()
    {
        const string diff = """
            diff --git a/src/Resilience/PolicyFactory.cs b/src/Resilience/PolicyFactory.cs
            index 1111111..2222222 100644
            --- a/src/Resilience/PolicyFactory.cs
            +++ b/src/Resilience/PolicyFactory.cs
            @@ -7,1 +7,0 @@
            -var circuitBreaker = Policy.Handle<Exception>().CircuitBreaker(5, TimeSpan.FromSeconds(30));
            """;

        IReadOnlyList<Finding> findings = new DeterministicAnalysisRunner().Analyze(diff, CreateMetadata());

        Assert.Contains(findings, f => f.Evidence.Contains("signal=DET-INFRA-001", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_FlagsUsingStatementRemoved()
    {
        const string diff = """
            diff --git a/src/IO/FileProcessor.cs b/src/IO/FileProcessor.cs
            index 1111111..2222222 100644
            --- a/src/IO/FileProcessor.cs
            +++ b/src/IO/FileProcessor.cs
            @@ -4,1 +4,1 @@
            -using (var stream = File.OpenRead(path))
            +var stream = File.OpenRead(path);
            """;

        IReadOnlyList<Finding> findings = new DeterministicAnalysisRunner().Analyze(diff, CreateMetadata());

        Assert.Contains(findings, f => f.Evidence.Contains("signal=DET-DISP-001", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_FlagsArrayPoolRemoved()
    {
        const string diff = """
            diff --git a/src/Serialization/Buffer.cs b/src/Serialization/Buffer.cs
            index 1111111..2222222 100644
            --- a/src/Serialization/Buffer.cs
            +++ b/src/Serialization/Buffer.cs
            @@ -2,1 +2,1 @@
            -byte[] buffer = ArrayPool<byte>.Shared.Rent(size);
            +byte[] buffer = new byte[size];
            """;

        IReadOnlyList<Finding> findings = new DeterministicAnalysisRunner().Analyze(diff, CreateMetadata());

        Assert.Contains(findings, f => f.Evidence.Contains("signal=DET-PERF-003", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_FlagsTestMethodRemoved()
    {
        const string diff = """
            diff --git a/tests/OrderServiceTests.cs b/tests/OrderServiceTests.cs
            index 1111111..2222222 100644
            --- a/tests/OrderServiceTests.cs
            +++ b/tests/OrderServiceTests.cs
            @@ -10,3 +10,0 @@
            -[Fact]
            -public void Submit_ShouldCreateOrder() { }
            """;

        IReadOnlyList<Finding> findings = new DeterministicAnalysisRunner().Analyze(diff, CreateMetadata());

        Assert.Contains(findings, f => f.Evidence.Contains("signal=DET-TEST-001", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_DoesNotFlagFactAttributeRemovedOutsideTestFile()
    {
        const string diff = """
            diff --git a/src/Models/Attributes.cs b/src/Models/Attributes.cs
            index 1111111..2222222 100644
            --- a/src/Models/Attributes.cs
            +++ b/src/Models/Attributes.cs
            @@ -5,1 +5,0 @@
            -[Fact]
            """;

        IReadOnlyList<Finding> findings = new DeterministicAnalysisRunner().Analyze(diff, CreateMetadata());

        Assert.DoesNotContain(findings, f => f.Evidence.Contains("signal=DET-TEST-001", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_FlagsAuthorizeAttributeRemoved()
    {
        const string diff = """
            diff --git a/src/Controllers/AdminController.cs b/src/Controllers/AdminController.cs
            index 1111111..2222222 100644
            --- a/src/Controllers/AdminController.cs
            +++ b/src/Controllers/AdminController.cs
            @@ -3,1 +3,0 @@
            -[Authorize(Roles = "admin")]
            """;

        IReadOnlyList<Finding> findings = new DeterministicAnalysisRunner().Analyze(diff, CreateMetadata());

        Assert.Contains(findings, f => f.Evidence.Contains("signal=DET-SEC-008", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_FlagsNullableEnableRemoved()
    {
        const string diff = """
            diff --git a/src/Core/Parser.cs b/src/Core/Parser.cs
            index 1111111..2222222 100644
            --- a/src/Core/Parser.cs
            +++ b/src/Core/Parser.cs
            @@ -1,1 +1,0 @@
            -#nullable enable
            """;

        IReadOnlyList<Finding> findings = new DeterministicAnalysisRunner().Analyze(diff, CreateMetadata());

        Assert.Contains(findings, f => f.Evidence.Contains("signal=DET-QUAL-003", StringComparison.Ordinal));
    }

    [Fact]
    public void Analyze_FlagsCancellationTokenRemoved()
    {
        const string diff = """
            diff --git a/src/Services/OrderService.cs b/src/Services/OrderService.cs
            index 1111111..2222222 100644
            --- a/src/Services/OrderService.cs
            +++ b/src/Services/OrderService.cs
            @@ -5,1 +5,1 @@
            -public async Task ProcessAsync(int id, CancellationToken cancellationToken)
            +public async Task ProcessAsync(int id)
            """;

        IReadOnlyList<Finding> findings = new DeterministicAnalysisRunner().Analyze(diff, CreateMetadata());

        Assert.Contains(findings, f => f.Evidence.Contains("signal=DET-INFRA-003", StringComparison.Ordinal));
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

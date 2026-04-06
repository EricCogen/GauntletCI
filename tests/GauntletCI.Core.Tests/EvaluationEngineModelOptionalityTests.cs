using GauntletCI.Core.Configuration;
using GauntletCI.Core.Evaluation;
using GauntletCI.Core.Gates;
using GauntletCI.Core.Infrastructure;
using GauntletCI.Core.Models;
using GauntletCI.Core.Telemetry;

namespace GauntletCI.Core.Tests;

[Collection("ModelOptionalityEnv")]
public sealed class EvaluationEngineModelOptionalityTests
{
    [Fact]
    public async Task EvaluateAsync_NoDiff_SkipsModelAndSucceedsWithoutApiKey()
    {
        using EnvironmentVariableScope env = new("ANTHROPIC_API_KEY", "OPENAI_API_KEY", "LOCAL_API_KEY");
        env.ClearAll();
        using TemporaryDirectory repo = TemporaryDirectory.CreateWithConfig(
            """
            {
              "test_command": "dotnet test",
              "model_required": false,
              "model": "claude-sonnet-4-6"
            }
            """);

        FakeCommandRunner commandRunner = new() { DiffResult = new CommandResult(0, "", "") };
        FakeLlmClient llmClient = new(new LlmResponse(true, "[]"));
        EvaluationEngine engine = CreateEngine(commandRunner, llmClient);
        EvaluationRequest request = new(
            WorkingDirectory: repo.Path,
            FullMode: false,
            FastMode: false,
            Rule: null,
            JsonOutput: false,
            NoTelemetry: true,
            ExplicitTestCommand: "fake-test");

        EvaluationResult result = await engine.EvaluateAsync(request, CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Findings);
        Assert.True(result.ModelStepSkipped);
        Assert.Null(result.WarningMessage);
        Assert.Equal(0, llmClient.CallCount);
    }

    [Fact]
    public async Task EvaluateAsync_NoDeterministicFindingsWithDiff_SkipsModelAndSucceedsWithoutApiKey()
    {
        using EnvironmentVariableScope env = new("ANTHROPIC_API_KEY", "OPENAI_API_KEY", "LOCAL_API_KEY");
        env.ClearAll();
        using TemporaryDirectory repo = TemporaryDirectory.CreateWithConfig(
            """
            {
              "test_command": "dotnet test",
              "model_required": false,
              "model": "claude-sonnet-4-6"
            }
            """);

        const string diff = """
            diff --git a/docs/notes.md b/docs/notes.md
            index 1111111..2222222 100644
            --- a/docs/notes.md
            +++ b/docs/notes.md
            @@ -1,0 +1,1 @@
            +Updated docs wording.
            """;

        FakeCommandRunner commandRunner = new();
        FakeLlmClient llmClient = new(new LlmResponse(true, "[]"));
        EvaluationEngine engine = CreateEngine(commandRunner, llmClient);
        EvaluationRequest request = new(
            WorkingDirectory: repo.Path,
            FullMode: false,
            FastMode: false,
            Rule: null,
            JsonOutput: false,
            NoTelemetry: true,
            ExplicitTestCommand: "fake-test",
            ProvidedDiff: diff);

        EvaluationResult result = await engine.EvaluateAsync(request, CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Findings);
        Assert.True(result.ModelStepSkipped);
        Assert.Null(result.WarningMessage);
        Assert.Equal(0, llmClient.CallCount);
    }

    [Fact]
    public async Task EvaluateAsync_ModelUnavailable_NonStrict_UsesDeterministicFallback()
    {
        using EnvironmentVariableScope env = new("ANTHROPIC_API_KEY", "OPENAI_API_KEY", "LOCAL_API_KEY");
        env.ClearAll();
        using TemporaryDirectory repo = TemporaryDirectory.CreateWithConfig(
            """
            {
              "test_command": "dotnet test",
              "blocking_rules": ["GCI016"],
              "model_required": false,
              "model": "claude-sonnet-4-6"
            }
            """);

        const string diff = """
            diff --git a/src/OrderController.cs b/src/OrderController.cs
            index 1111111..2222222 100644
            --- a/src/OrderController.cs
            +++ b/src/OrderController.cs
            @@ -10,0 +11,1 @@
            +var order = repository.LoadAsync(id).Result;
            """;

        FakeCommandRunner commandRunner = new();
        FakeLlmClient llmClient = new(new LlmResponse(true, "[]"));
        EvaluationEngine engine = CreateEngine(commandRunner, llmClient);
        EvaluationRequest request = new(
            WorkingDirectory: repo.Path,
            FullMode: false,
            FastMode: false,
            Rule: null,
            JsonOutput: false,
            NoTelemetry: true,
            ExplicitTestCommand: "fake-test",
            ProvidedDiff: diff);

        EvaluationResult result = await engine.EvaluateAsync(request, CancellationToken.None);

        Assert.Equal(1, result.ExitCode);
        Assert.True(result.ModelStepSkipped);
        Assert.Equal("Model unavailable. Showing deterministic findings only.", result.WarningMessage);
        Assert.Contains(result.Findings, finding => finding.RuleId == "GCI016");
        Assert.Equal(0, llmClient.CallCount);
    }

    [Fact]
    public async Task EvaluateAsync_ModelUnavailable_NonStrict_NonBlockingDeterministicFindingReturnsSuccess()
    {
        using EnvironmentVariableScope env = new("ANTHROPIC_API_KEY", "OPENAI_API_KEY", "LOCAL_API_KEY");
        env.ClearAll();
        using TemporaryDirectory repo = TemporaryDirectory.CreateWithConfig(
            """
            {
              "test_command": "dotnet test",
              "model_required": false,
              "model": "claude-sonnet-4-6"
            }
            """);

        const string diff = """
            diff --git a/src/OrderController.cs b/src/OrderController.cs
            index 1111111..2222222 100644
            --- a/src/OrderController.cs
            +++ b/src/OrderController.cs
            @@ -10,0 +11,1 @@
            +return await _service.ProcessAsync(id);
            """;

        FakeCommandRunner commandRunner = new();
        FakeLlmClient llmClient = new(new LlmResponse(true, "[]"));
        EvaluationEngine engine = CreateEngine(commandRunner, llmClient);
        EvaluationRequest request = new(
            WorkingDirectory: repo.Path,
            FullMode: false,
            FastMode: false,
            Rule: null,
            JsonOutput: false,
            NoTelemetry: true,
            ExplicitTestCommand: "fake-test",
            ProvidedDiff: diff);

        EvaluationResult result = await engine.EvaluateAsync(request, CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.True(result.ModelStepSkipped);
        Assert.Equal("Model unavailable. Showing deterministic findings only.", result.WarningMessage);
        Assert.Contains(result.Findings, finding => finding.RuleId == "GCI005");
        Assert.Equal(0, llmClient.CallCount);
    }

    [Fact]
    public async Task EvaluateAsync_ModelUnavailable_StrictMode_ReturnsConfigFailure()
    {
        using EnvironmentVariableScope env = new("ANTHROPIC_API_KEY", "OPENAI_API_KEY", "LOCAL_API_KEY");
        env.ClearAll();
        using TemporaryDirectory repo = TemporaryDirectory.CreateWithConfig(
            """
            {
              "test_command": "dotnet test",
              "model_required": true,
              "model": "claude-sonnet-4-6"
            }
            """);

        const string diff = """
            diff --git a/src/OrderController.cs b/src/OrderController.cs
            index 1111111..2222222 100644
            --- a/src/OrderController.cs
            +++ b/src/OrderController.cs
            @@ -10,0 +11,1 @@
            +var order = repository.LoadAsync(id).Result;
            """;

        FakeCommandRunner commandRunner = new();
        FakeLlmClient llmClient = new(new LlmResponse(true, "[]"));
        EvaluationEngine engine = CreateEngine(commandRunner, llmClient);
        EvaluationRequest request = new(
            WorkingDirectory: repo.Path,
            FullMode: false,
            FastMode: false,
            Rule: null,
            JsonOutput: false,
            NoTelemetry: true,
            ExplicitTestCommand: "fake-test",
            ProvidedDiff: diff);

        EvaluationResult result = await engine.EvaluateAsync(request, CancellationToken.None);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Model enrichment is required by configuration", result.ErrorMessage);
        Assert.Equal(0, llmClient.CallCount);
    }

    [Fact]
    public async Task EvaluateAsync_ModelAvailable_UsesModelFindings()
    {
        using TemporaryDirectory repo = TemporaryDirectory.CreateWithConfig(
            """
            {
              "test_command": "dotnet test",
              "model_required": false,
              "model": "claude-sonnet-4-6"
            }
            """);

        const string diff = """
            diff --git a/src/OrderController.cs b/src/OrderController.cs
            index 1111111..2222222 100644
            --- a/src/OrderController.cs
            +++ b/src/OrderController.cs
            @@ -10,0 +11,1 @@
            +return await _service.ProcessAsync(id);
            """;

        const string modelJson = """
            [
              {
                "rule_id": "GCI004",
                "rule_name": "Breaking Change Risk",
                "severity": "high",
                "finding": "Public contract changed without compatibility handling.",
                "evidence": "src/OrderController.cs:11",
                "why_it_matters": "Callers can break at runtime.",
                "suggested_action": "Maintain backward compatibility or version the endpoint.",
                "confidence": "High"
              }
            ]
            """;

        FakeCommandRunner commandRunner = new();
        FakeLlmClient llmClient = new(new LlmResponse(true, modelJson));
        EvaluationEngine engine = CreateEngine(commandRunner, llmClient);
        EvaluationRequest request = new(
            WorkingDirectory: repo.Path,
            FullMode: false,
            FastMode: false,
            Rule: null,
            JsonOutput: false,
            NoTelemetry: true,
            ExplicitTestCommand: "fake-test",
            ProvidedDiff: diff,
            LocalEndpoint: "http://127.0.0.1:11434/v1");

        EvaluationResult result = await engine.EvaluateAsync(request, CancellationToken.None);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains(result.Findings, finding => finding.RuleId == "GCI004");
        Assert.Null(result.WarningMessage);
        Assert.Equal(1, llmClient.CallCount);
    }

    [Fact]
    public async Task EvaluateAsync_DeterministicFallback_AttachesAuditTrailWithPolicyReferences()
    {
        using EnvironmentVariableScope env = new("ANTHROPIC_API_KEY", "OPENAI_API_KEY", "LOCAL_API_KEY");
        env.ClearAll();
        using TemporaryDirectory repo = TemporaryDirectory.CreateWithConfig(
            """
            {
              "test_command": "dotnet test",
              "blocking_rules": ["GCI016"],
              "policy_refs": ["policy://security/baseline@v1"],
              "model_required": false,
              "model": "claude-sonnet-4-6"
            }
            """);

        const string diff = """
            diff --git a/src/OrderController.cs b/src/OrderController.cs
            index 1111111..2222222 100644
            --- a/src/OrderController.cs
            +++ b/src/OrderController.cs
            @@ -10,0 +11,1 @@
            +var order = repository.LoadAsync(id).Result;
            """;

        FakeCommandRunner commandRunner = new();
        FakeLlmClient llmClient = new(new LlmResponse(true, "[]"));
        EvaluationEngine engine = CreateEngine(commandRunner, llmClient);
        EvaluationRequest request = new(
            WorkingDirectory: repo.Path,
            FullMode: false,
            FastMode: false,
            Rule: null,
            JsonOutput: false,
            NoTelemetry: true,
            ExplicitTestCommand: "fake-test",
            ProvidedDiff: diff);

        EvaluationResult result = await engine.EvaluateAsync(request, CancellationToken.None);

        Assert.NotNull(result.AuditTrail);
        Assert.Equal(".gauntletci.json", result.AuditTrail!.ConfigResolved.ConfigFormat);
        Assert.Contains("policy://security/baseline@v1", result.AuditTrail.ConfigResolved.PolicyReferences);
        Assert.Contains(result.AuditTrail.RuleFirings, ruleEvent => ruleEvent.RuleId == "GCI016");
    }

    private static EvaluationEngine CreateEngine(FakeCommandRunner commandRunner, FakeLlmClient llmClient)
    {
        return new EvaluationEngine(
            new ConfigLoader(),
            new TestCommandResolver(),
            new BranchCurrencyGate(commandRunner),
            new TestPassageGate(commandRunner),
            commandRunner,
            new ContextAssembler(),
            new DeterministicAnalysisRunner(),
            new PromptBuilder(),
            new FindingParser(),
            new RulesTextProvider(),
            new ModelSelector(),
            llmClient,
            new TelemetryEmitter(new HttpClient { Timeout = TimeSpan.FromSeconds(2) }));
    }

    private sealed class FakeLlmClient(LlmResponse response) : ILlmClient
    {
        public int CallCount { get; private set; }

        public Task<LlmResponse> EvaluateAsync(string model, string systemPrompt, string userPrompt, string apiKey, CancellationToken cancellationToken, string? baseUrl = null)
        {
            CallCount++;
            return Task.FromResult(response);
        }
    }

    private sealed class FakeCommandRunner : ICommandRunner
    {
        public CommandResult DiffResult { get; init; } = new(0, "", "");

        public Task<CommandResult> RunProcessAsync(string fileName, string arguments, string workingDirectory, CancellationToken cancellationToken)
        {
            if (fileName == "git" && arguments.StartsWith("rev-parse --abbrev-ref --symbolic-full-name @{upstream}", StringComparison.Ordinal))
            {
                // No upstream configured => BranchCurrencyGate treats as pass/skip.
                return Task.FromResult(new CommandResult(1, "", "no upstream"));
            }

            if (fileName == "fake-test")
            {
                return Task.FromResult(new CommandResult(0, "ok", ""));
            }

            if (fileName == "git" && arguments == "log -n 3 --pretty=%s")
            {
                return Task.FromResult(new CommandResult(0, "feat: update", ""));
            }

            return Task.FromResult(new CommandResult(1, "", $"Unexpected command: {fileName} {arguments}"));
        }

        public Task<CommandResult> RunShellAsync(string command, string workingDirectory, CancellationToken cancellationToken)
        {
            if (command.StartsWith("git diff", StringComparison.Ordinal))
            {
                return Task.FromResult(DiffResult);
            }

            return Task.FromResult(new CommandResult(1, "", $"Unexpected shell command: {command}"));
        }
    }

    private sealed class EnvironmentVariableScope : IDisposable
    {
        private readonly Dictionary<string, string?> _previous = new(StringComparer.Ordinal);

        public EnvironmentVariableScope(params string[] variableNames)
        {
            foreach (string variableName in variableNames)
            {
                _previous[variableName] = Environment.GetEnvironmentVariable(variableName);
            }
        }

        public void ClearAll()
        {
            foreach (string variableName in _previous.Keys)
            {
                Environment.SetEnvironmentVariable(variableName, null);
            }
        }

        public void Dispose()
        {
            foreach ((string variableName, string? value) in _previous)
            {
                Environment.SetEnvironmentVariable(variableName, value);
            }
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public string Path { get; }

        private TemporaryDirectory(string path)
        {
            Path = path;
        }

        public static TemporaryDirectory CreateWithConfig(string repoConfigJson)
        {
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "GauntletCI.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            File.WriteAllText(System.IO.Path.Combine(path, ".gauntletci.json"), repoConfigJson);
            return new TemporaryDirectory(path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}

[CollectionDefinition("ModelOptionalityEnv", DisableParallelization = true)]
public sealed class ModelOptionalityEnvCollection
{
}

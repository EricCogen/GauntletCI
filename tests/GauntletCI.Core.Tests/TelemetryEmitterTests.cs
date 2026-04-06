using System.Net;
using System.Text.Json;
using GauntletCI.Core.Models;
using GauntletCI.Core.Telemetry;

namespace GauntletCI.Core.Tests;

public sealed class TelemetryEmitterTests
{
    [Fact]
    public async Task EmitAsync_UsesAuditTrailForAnonymousRulePerformancePayload()
    {
        using EnvironmentVariableScope scope = new("GAUNTLETCI_TELEMETRY_ENDPOINT", "https://unit.test/events");
        CapturingHandler handler = new();
        TelemetryEmitter emitter = new(new HttpClient(handler));

        EvaluationResult result = CreateResultWithAuditTrail();
        GauntletConfig config = new()
        {
            Telemetry = true,
            TelemetryConsentRecorded = true,
            PolicyReferences = ["policy://security/baseline@v1"],
            Model = "claude-sonnet-4-6",
        };

        await emitter.EmitAsync(result, config, CancellationToken.None);

        Assert.NotNull(handler.LastContent);
        using JsonDocument document = JsonDocument.Parse(handler.LastContent!);
        JsonElement root = document.RootElement;

        Assert.Equal("1", root.GetProperty("schema_version").GetString());
        Assert.Equal(2, root.GetProperty("rule_fire_counts").GetProperty("GCI012").GetInt32());
        Assert.Equal(1, root.GetProperty("rule_fire_counts").GetProperty("GCI016").GetInt32());
        Assert.Equal(1, root.GetProperty("config").GetProperty("policy_refs_count").GetInt32());
    }

    private static EvaluationResult CreateResultWithAuditTrail()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        DiffMetadata metadata = new(
            LinesAdded: 9,
            LinesRemoved: 2,
            FilesChanged: 1,
            TestFilesTouched: false,
            TestFilesChanged: 0,
            TestFilesWithContentChanges: 0,
            TestFilesRenameOnly: 0,
            TestLinesAdded: 0,
            TestLinesRemoved: 0,
            TestAssertionLinesAdded: 0,
            TestSetupLinesAdded: 0,
            TestsChangedWithoutAssertions: false,
            TestChangesAreRenameOrSetupChurn: false,
            Languages: ["csharp"],
            DiffTrimmed: false,
            EstimatedTokens: 120);

        IReadOnlyList<Finding> findings =
        [
            new Finding("GCI012", "Security Risk", "high", "Hardcoded secret literal added.", "src/App.cs:12 token=\"live-token\"", "Leaks credential material.", "Use secure secret storage.", "High"),
            new Finding("GCI012", "Security Risk", "high", "Weak crypto added.", "src/Crypto.cs:40 MD5.Create()", "Weak primitives are attackable.", "Use SHA-256 or better.", "High"),
            new Finding("GCI016", "Concurrency and State Risk", "high", "Blocking async wait added.", "src/Service.cs:55 .Result", "Can deadlock under load.", "Use async/await.", "High")
        ];

        EvaluationResult result = new(
            ExitCode: 1,
            BranchCurrencyGate: GateResult.Pass("Branch Currency", "ok"),
            TestPassageGate: GateResult.Pass("Test Passage", "ok"),
            Findings: findings,
            ErrorMessage: null,
            DiffTrimmed: false,
            Model: "claude-sonnet-4-6",
            DiffMetadata: metadata,
            EvaluationDurationMs: 150,
            WarningMessage: null,
            ModelStepSkipped: true);

        EvaluationAuditTrail trail = new(
            SchemaVersion: "1",
            EvaluationId: Guid.NewGuid(),
            CapturedAtUtc: now,
            ConfigResolved: new ConfigResolvedEvent(
                EventId: Guid.NewGuid().ToString("N"),
                OccurredAtUtc: now,
                ConfigFormat: ".gauntletci.json",
                Model: "claude-sonnet-4-6",
                ModelRequired: false,
                TelemetryEnabled: true,
                TelemetryConsentRecorded: true,
                DisabledRules: [],
                BlockingRules: ["GCI012"],
                PolicyReferences: ["policy://security/baseline@v1"]),
            AnalysisCompleted: new AnalysisCompletedEvent(
                EventId: Guid.NewGuid().ToString("N"),
                OccurredAtUtc: now,
                ExitCode: 1,
                ModelStepSkipped: true,
                DiffTrimmed: false,
                HasError: false,
                EvaluationDurationMs: 150,
                Model: "claude-sonnet-4-6",
                BranchCurrencyPassed: true,
                TestPassagePassed: true,
                FindingCount: findings.Count,
                DiffMetadata: metadata),
            RuleFirings:
            [
                new RuleFiredEvent(Guid.NewGuid().ToString("N"), now, "GCI012", "Security Risk", "high", "High", "src/App.cs:12 token=\"live-token\""),
                new RuleFiredEvent(Guid.NewGuid().ToString("N"), now, "GCI012", "Security Risk", "high", "High", "src/Crypto.cs:40 MD5.Create()"),
                new RuleFiredEvent(Guid.NewGuid().ToString("N"), now, "GCI016", "Concurrency and State Risk", "high", "High", "src/Service.cs:55 .Result")
            ]);

        return result with { AuditTrail = trail };
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public string? LastContent { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastContent = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private sealed class EnvironmentVariableScope : IDisposable
    {
        private readonly string _name;
        private readonly string? _previous;

        public EnvironmentVariableScope(string name, string value)
        {
            _name = name;
            _previous = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(_name, _previous);
        }
    }
}

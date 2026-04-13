# GauntletCI Architecture

## Project layout

| Project | Role |
|---|---|
| `GauntletCI.Core` | Rule engine, diff parser, static analysis runner, configuration models, domain types |
| `GauntletCI.Cli` | System.CommandLine entry point, output formatters, telemetry pipeline, all CLI commands |
| `GauntletCI.Llm` | ONNX runtime integration (Phi-3 Mini); `NullLlmEngine` is the default no-op |
| `GauntletCI.Corpus` | Corpus ingestion pipeline — pull request hydration, normalization, scoring |
| `GauntletCI.Tests` | xUnit test suite for Core and Cli |
| `GauntletCI.BenchmarkReporter` | Benchmark report generation |
| `GauntletCI.Benchmarks` | BenchmarkDotNet harness (in `/tests/`) |

---

## Analysis pipeline (per-run flow)

```
gauntletci analyze [options]
        │
        ▼
1. Diff ingestion          DiffParser
        │                  ├── --diff <file>       → FromFile()
        │                  ├── --commit <sha>      → FromGitAsync()   (git diff <sha>^..<sha>)
        │                  ├── --staged            → FromStagedAsync() (git diff --cached)
        │                  ├── --unstaged          → FromUnstagedAsync() (git diff)
        │                  ├── --all-changes       → FromAllChangesAsync() (git diff HEAD)
        │                  └── (none)              → Parse(stdin)
        │
        ▼
2. Config loading           ConfigLoader.Load(repoRoot)
        │                  Reads .gauntletci.json → GauntletConfig
        │                  Also loads .gauntletci-ignore → IgnoreList
        │
        ▼
3. Static analysis          StaticAnalysisRunner.RunAsync()
        │                  Roslyn-based; runs only on changed .cs files present on disk.
        │                  Returns null when no repo path is available (--diff mode)
        │                  or when no C# files changed.
        │
        ▼
4. Rule evaluation          RuleOrchestrator.RunAsync()
        │                  Rules are auto-discovered via reflection — all non-abstract
        │                  IRule implementations in the Core assembly are loaded and
        │                  sorted by ID. IConfigurableRule instances receive the config.
        │                  Each rule runs with a 30-second per-rule timeout.
        │
        ▼
5. Post-processing          RuleOrchestrator.PostProcess()
        │                  GCI0018: aggregate finding when >3 distinct rules fire.
        │                  GCI0019: large-diff warning based on total line count.
        │                  Severity overrides from config applied to all findings.
        │                  IgnoreList suppressions applied.
        │
        ▼
6. LLM enrichment           LlmEngineSelector.ResolveAsync()  [opt-in: --with-llm]
        │                  Enriches High-confidence findings with a natural-language
        │                  explanation via Phi-3 Mini ONNX or a CI endpoint (see below).
        │
        ▼
7. Output                   ConsoleReporter (text) | JsonSerializer (--output json)
        │                  GitHubAnnotationWriter (--github-annotations)
        │
        ▼
8. Telemetry                TelemetryCollector.CollectAsync()
                           Anonymous events written to ~/.gauntletci/telemetry.ndjson.
                           Background HTTP upload in Shared mode only.
```

---

## Rule system

### Interfaces

```csharp
public interface IRule
{
    string Id   { get; }
    string Name { get; }
    Task<List<Finding>> EvaluateAsync(DiffContext diff, AnalyzerResult? staticAnalysis, CancellationToken ct);
}
```

`RuleBase` is the abstract base class that all built-in rules extend. It provides:

- `CreateFinding(summary, evidence, whyItMatters, suggestedAction, confidence)` — constructs a `Finding` with the rule's `Id` and `Name` pre-filled.

`IConfigurableRule` is an optional secondary interface for rules that need access to `GauntletConfig` (e.g., GCI0035 Architecture Layer Guard reads `ForbiddenImports`).

### Auto-discovery

`RuleOrchestrator.CreateDefault()` reflects over the `GauntletCI.Core` assembly at startup:

```csharp
typeof(RuleOrchestrator).Assembly.GetTypes()
    .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IRule).IsAssignableFrom(t))
    .Select(t => (IRule)Activator.CreateInstance(t)!)
    .Where(r => IsRuleEnabled(r.Id, config))
```

Adding a new rule requires only dropping a new `IRule` class into the assembly — no registration step.

### Rule IDs

Rules are named `GCI0001` through `GCI0037`. `GCI0028` is reserved.

### Per-rule timeout

Each rule runs under a linked `CancellationTokenSource` with a 30-second deadline (`_ruleTimeout`). A timeout produces a synthetic `Medium`-confidence finding reporting the timeout, so the run still completes.

### Configuration

Rules are configured via `.gauntletci.json`:

```json
{
  "rules": {
    "GCI0002": { "enabled": false },
    "GCI0005": { "severity": "High" }
  }
}
```

`severity` overrides the rule's default `Confidence` level. Valid values: `"High"`, `"Medium"`, `"Low"`.

---

## Diff model

```
DiffContext
 ├── CommitSha        : string
 ├── CommitMessage    : string?
 ├── RawDiff          : string
 └── Files            : List<DiffFile>
       ├── OldPath / NewPath : string
       ├── IsAdded / IsDeleted / IsRenamed : bool
       └── Hunks       : List<DiffHunk>
             ├── OldStartLine / NewStartLine : int
             └── Lines : List<DiffLine>
                   ├── Kind        : DiffLineKind  (Added | Removed | Context)
                   ├── LineNumber  : int  (new-file line; 0 for Removed)
                   ├── OldLineNumber: int (old-file line; 0 for Added)
                   └── Content     : string
```

Cross-file helpers on `DiffContext`:
- `AllAddedLines` — flattens added lines across all files and hunks.
- `AllRemovedLines` — flattens removed lines across all files and hunks.

Per-file helpers on `DiffFile`:
- `AddedLines`, `RemovedLines` — lines within that file only.

`DiffParser` handles both the standard `diff --git` header format and bare unified diff format (e.g., from `git diff` piped through stdin).

---

## Configuration

| File | Purpose |
|---|---|
| `.gauntletci.json` | Per-rule `enabled`/`severity` overrides; `policy_refs`; `llm` block; `forbidden_imports` for GCI0035 |
| `.gauntletci-ignore` | One rule ID per line — suppresses that rule's findings for the entire repo |

`ConfigLoader.Load(repoPath)` returns a default `GauntletConfig` (all rules enabled, no overrides) when the file is absent or unparseable, so no config file is required to run.

---

## Telemetry pipeline

Telemetry is **opt-in** and collected anonymously. No code, no file paths, no PII.

### Consent modes

| Mode | Behavior |
|---|---|
| `Off` | No events written or uploaded |
| `Local` | Events written to `~/.gauntletci/telemetry.ndjson` only |
| `Shared` | Local write + background HTTP upload to the GauntletCI endpoint |

Consent is prompted on first non-`init` run and stored in a local preference file.

### Event types

| Event | When emitted | Key fields |
|---|---|---|
| `analysis` | Once per run | `findingCount`, `filesChanged`, `rulesEvaluated`, `linesAdded`, `linesRemoved` |
| `finding` | Once per finding | `ruleId`, `confidence`, `fileExt` (extension only, never full path) |
| `feedback` | On `gauntletci feedback` | `vote` (`"up"` or `"down"`) |

### Anonymization

- **Install ID**: stable random UUID stored locally; identifies an installation, not a person.
- **Repo hash**: 8-character SHA-256 prefix of the git remote URL — identifies a repo without revealing its path or name.

### Storage and upload

- `TelemetryStore`: appends NDJSON records to `~/.gauntletci/telemetry.ndjson`.
- `TelemetryUploader`: fires a background HTTP upload (`Shared` mode only). Failures are silently swallowed — telemetry never crashes the tool.

---

## LLM integration

GauntletCI supports two LLM enrichment paths:

### Local ONNX (default opt-in path)

- Requires `gauntletci model download` to fetch Phi-3 Mini weights.
- Activated per-run with `--with-llm`.
- Runs in a sidecar daemon process (`LlmDaemonServer`) to isolate ONNX memory from the main process.
- `NullLlmEngine` is the no-op default when no model is present, adding zero dependencies to a standard run.

### CI/CD premium endpoint

- Configured via the `llm` block in `.gauntletci.json`.
- Routes to any OpenAI-chat-completions-compatible endpoint (e.g., `api.openai.com`, Azure OpenAI).
- API key read from the environment variable named by `ciApiKeyEnv` — never stored in config.
- Requires a GauntletCI license key in the environment variable named by `licenseKeyEnv`.

In both cases, enrichment applies only to `High`-confidence findings and appends a `LlmExplanation` string to each.

---

## Corpus pipeline

The corpus pipeline ingests public pull request data for offline rule evaluation and dataset construction. See [`docs/corpus-pipeline.md`](corpus-pipeline.md) for details.

---

## CLI commands

| Command | Description |
|---|---|
| `analyze` | Run rule evaluation against a diff (main entry point) |
| `init` | Interactive first-run setup (telemetry consent, config scaffold) |
| `ignore` | Add a rule ID to `.gauntletci-ignore` |
| `model` | Download/manage the local ONNX model |
| `postmortem` | Analyse a historical commit range |
| `feedback` | Submit a thumbs-up/down vote on a finding |
| `telemetry` | View or change telemetry consent |
| `corpus` | Corpus ingestion and management |

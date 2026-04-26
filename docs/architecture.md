# GauntletCI Architecture

## Project layout

| Project | Role |
|---|---|
| `GauntletCI.Core` | Rule engine, diff parser, static analysis runner, configuration models, domain types |
| `GauntletCI.Cli` | System.CommandLine entry point, output formatters, telemetry pipeline, all CLI commands |
| `GauntletCI.Llm` | ONNX runtime integration (Phi-4 Mini); `NullLlmEngine` is the default no-op |
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
        │                  GCI0019: large-diff warning based on total line count.
        │                  Severity overrides from config applied to all findings.
        │                  IgnoreList suppressions applied.
        │                  When 4+ distinct rules fire, ConsoleReporter emits a
        │                  compound-risk header note (not a separate finding).
        │
        ▼
6. LLM enrichment           LlmEngineSelector.ResolveAsync()  [opt-in: --with-llm]
        │                  Enriches High-confidence findings with a natural-language
        │                  explanation via Phi-4 Mini ONNX or a CI endpoint (see below).
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

Rules span `GCI0001`–`GCI0053`. `GCI0028` is reserved (never issued).

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

### Rule interdependencies and self-interference

Because GauntletCI is analyzed by its own rules during development and in CI, some rules fire on the files that implement other rules. This is called **self-interference** — a rule detecting a pattern inside the implementation of another (or the same) rule.

#### Why it happens

Rules analyze raw diff text using text patterns, regex, and line-count heuristics. They have no awareness of which file they are scanning; they apply the same logic to all eligible `.cs` files. When a rule's own implementation uses the exact pattern the rule is designed to detect — or when two rules share overlapping pattern vocabularies — self-interference occurs.

#### Block-severity false positives resolved (April 2026 audit)

A full-codebase audit (`git diff empty-tree..HEAD`, 83 Block findings) revealed the following confirmed false positives, all since resolved:

| Firing rule | Target | Root cause | Resolution |
|---|---|---|---|
| **GCI0046** PatternConsistencyDeviation | `GCI0038_DependencyInjectionSafety.cs` | `ServiceLocatorPatterns` string array contains the exact strings the rule detects | `IsInsideStringLiteral` quote-counting guard in `CheckServiceLocator` |
| **GCI0042** TodoStubDetection | `GCI0042_TodoStubDetection.cs` | `/// GCI0042 — TODO/Stub Detection` XML doc comment triggered its own TODO detection | Skip lines starting with `///` |
| **GCI0048** InsecureRandomInSecurityContext | `SyntaxGuard.cs` | Code-example comment `// e.g. $"{new Random().Next()}"` — Roslyn syntax guard is null in diff-only mode | `IsAfterLineComment` fallback guard after Roslyn guard |
| **GCI0049** FloatDoubleEqualityComparison | `LlmAdjudicator.cs` | `(tp + fp) == 0 ? 0.0 : (double)tp / (tp + fp)` — `==` is an integer zero-guard, not a float comparison | `IntegerZeroGuardRegex` to detect safe-division ternaries |
| **GCI0044** PerformanceHotpathRisk | All `Rules/Implementations/*.cs` (~14 files) | Analysis loops (`foreach (var file in diff.Files)`) triggered LINQ-in-loop detection for inner `.FirstOrDefault()`/`.Any()` calls | `IsRuleImplementationFile` path exclusion |
| **GCI0044** PerformanceHotpathRisk | `VectorStore.cs` | `while (reader.Read()) { rows.Add(...) }` ADO.NET reader pattern flagged as unbounded collection growth | `.Read()` detection skips DB reader loops in `CheckAddInsideLoop` |
| **GCI0043** NullabilityTypeSafety | All `Commands/*.cs` files (~12 files) | `GetValueForOption(opt)!` is System.CommandLine's idiomatic required-option pattern (3–48 occurrences per file exceeded the threshold of 1) | Filter excludes `GetValueForOption(` lines from the null-forgiving operator count |

#### Remaining interactions (Warn / Info — not false positives)

These cross-rule interactions are retained because they reflect real patterns in tool code. They would be suppressed by a normal `.gauntletci-baseline.json` workflow:

| Firing rule | Target | What fires |
|---|---|---|
| GCI0038 DependencyInjectionSafety | `GCI0038.cs`, `GCI0039.cs`, `GCI0024.cs` | Service-locator pattern strings inside the rule's own `ServiceLocatorPatterns` array (Warn) |
| GCI0024 ResourceLifecycle | `GCI0024.cs` | The rule's own `IDisposable` usage patterns fire on itself (Warn) |
| GCI0036 PureContextMutation | `GCI0036.cs` | 11 Info findings on its own implementation |

#### Guidelines for new rule authors

Before shipping a new rule, run a self-interference check:

```bash
gauntletci analyze --all-changes --severity info --no-baseline
```

Review any Block findings on `Rules/Implementations/` or `GauntletCI.Cli/Commands/` files and ask:

1. **Does the rule fire on its own implementation file?**  If the rule uses the same pattern it detects (a TODO rule with a TODO in its doc comment, a pattern-array rule whose array contains its own patterns), add an exclusion.

2. **Does the rule fire on the analysis loop pattern?**  Rules that detect LINQ calls, loop constructs, or collection growth should consider whether `Rules/Implementations/` files (which use LINQ inside analysis loops as standard practice) warrant a path exclusion.

3. **Does the rule fire on System.CommandLine idioms?**  CLI commands use `GetValueForOption(opt)!` extensively (guaranteed non-null for required options). Rules that count null-forgiving operators or pattern-match on `!` should filter this form.

4. **Does the rule fire on string literal pattern arrays?**  Rules that store their detection strings in `static readonly string[]` fields may trigger pattern-matching rules that do not distinguish between code and data. Use the `IsInsideStringLiteral` quote-counting helper (available in `GCI0046` and `GCI0043`) to guard these cases.

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

- Requires `gauntletci model download` to fetch Phi-4 Mini weights.
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

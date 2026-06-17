# GauntletCI Architecture

## Design Philosophy vs. Traditional CI

To understand why GauntletCI is fast and private, it helps to see how it works fundamentally differently from post-commit pipelines:

| Vector | Traditional CI Pipelines (Post-Commit) | GauntletCI (Pre-Commit Inner Loop) |
| --- | --- | --- |
| **Execution Trigger** | Push to remote branch / PR creation. | Local `git commit` hook or manual CLI invocation. |
| **Analysis Scope** | Whole-project compilation and full test-suite execution. | Staged Git diff isolation via localized Roslyn AST parsing. |
| **Feedback Loop** | 5 to 20+ minutes (Context switch required). | **Seconds on typical staged diffs** (Immediate local feedback; no CI queue). |
| **Data Privacy** | Code is transmitted to third-party cloud runners. | **Core analysis runs locally by default.** No external API calls required for rule detection; optional telemetry and integrations are opt-in. |
| **Target Risk** | Functional regressions (via unit/integration tests). | **Behavioral Change Risk (BCR)** (Silent exception paths, unverified logic). |

## The Diff-Isolation Engine

Instead of executing a heavy build-and-scan pass, GauntletCI intercepts the inner loop at the syntax level:

```
[Staged Changes] ──> [Git Diff Extraction] ──> [Targeted Roslyn AST Parse] ──> [Deterministic Rule Evaluation]
                                                                                        │
  ┌─────────────────────────────────── COLD STOP ────────────────────────────────────────┤
  ▼                                                                                      ▼
[Risk Detected: Block Commit]                                                  [Clean: Pass to Git Engine]
```

1. **Extraction:** The engine queries the local Git index to isolate modified lines and files.
2. **Targeted Parsing:** Only the affected source files are loaded into syntax trees, omitting unchanged projects or assemblies.
3. **Rule Application:** 37 deterministic rules (e.g., `GCI0003` for guard clause removal, `GCI0007` for swallowed exceptions) evaluate the structural delta between the pre-image and post-image of the code.

This approach means GauntletCI runs before you push, gives instant feedback, and never sees your code outside your machine.

---

## Project layout

| Project | Role |
|---|---|
| `GauntletCI.Core` | Rule engine, diff parser, static analysis runner, configuration models, domain types |
| `GauntletCI.Cli` | System.CommandLine entry point, output formatters, telemetry pipeline, all CLI commands |
| `GauntletCI.Llm` | ONNX runtime integration (Phi-4 Mini); `NullLlmEngine` is the default no-op |
| `GauntletCI.Corpus` | Corpus ingestion pipeline: pull request hydration, normalization, scoring |
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
        │                  Rules are auto-discovered via reflection: all non-abstract
        │                  IRule implementations in the Core assembly are loaded and
        │                  sorted by ID. IConfigurableRule instances receive the config.
        │                  Each rule runs with a 30-second per-rule timeout.
        │
        ▼
5. Post-processing          IgnoreList suppressions; IPostProcessor rules (e.g. GCI0019).
        │                  Per-finding severity from ConfigurationService (json >
        │                  .editorconfig > DefaultSeverities). Compound-risk header
        │                  note when 4+ distinct rules fire (display only).
        │
        ▼
6. Trust filters            ProvenanceFindingProcessor → SemanticFindingProcessor
        │                  → DomainFindingProcessor (each may drop or boost findings).
        │
        ▼
7. Delivery                 FindingDeliveryProcessor (coordination boosts, file-level
        │                  demotion, per-rule/per-file caps, ranked global cap).
        │                  DeliverySummary counts what was dropped and why.
        │
        ▼
8. LLM enrichment           LlmEngineSelector.ResolveAsync()  [opt-in: --with-llm]
        │                  Enriches High-confidence findings with a natural-language
        │                  explanation via Phi-4 Mini ONNX or a CI endpoint (see below).
        │
        ▼
9. Output                   ConsoleReporter (text) | JsonSerializer (--output json)
        │                  GitHubAnnotationWriter (--github-annotations)
        │                  SensitivityFilter applied at display/exit time (--sensitivity).
        │
        ▼
10. Telemetry               TelemetryCollector.CollectAsync()
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
    Task<List<Finding>> EvaluateAsync(AnalysisContext context, CancellationToken ct = default);
}
```

`RuleBase` is the abstract base class that all built-in rules extend. It provides:

- `CreateFinding(summary, evidence, whyItMatters, suggestedAction, confidence)`: constructs a `Finding` with the rule's `Id` and `Name` pre-filled.

`IConfigurableRule` is an optional secondary interface for rules that need access to `GauntletConfig` (e.g., GCI0035 Architecture Layer Guard reads `ForbiddenImports`).

### Auto-discovery

`RuleOrchestrator.CreateDefault()` reflects over the `GauntletCI.Core` assembly at startup:

```csharp
typeof(RuleOrchestrator).Assembly.GetTypes()
    .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IRule).IsAssignableFrom(t))
    .Select(t => (IRule)Activator.CreateInstance(t)!)
    .Where(r => IsRuleEnabled(r.Id, config))
```

Adding a new rule requires only dropping a new `IRule` class into the assembly: no registration step.

### Rule IDs

39 rule implementations ship in Core; **37 are active by default** (GCI0054 and GCI0055 disabled as duplicates). IDs span `GCI0001`–`GCI0059` with gaps in the sequence. `GCI0028` is reserved (never issued).

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

`severity` overrides the rule's effective `RuleSeverity` (Block, Warn, Advisory, Info, or None to disable). Valid string values match `.editorconfig` conventions: `"error"`, `"warning"`, `"suggestion"`, `"none"`, etc.

Each rule also sets a default `Confidence` (High, Medium, Low) when it calls `CreateFinding(...)`. Confidence and severity are independent dimensions used together in delivery ranking and display filtering (see [Finding trust and delivery](#finding-trust-and-delivery)).

---

## Finding trust and delivery

GauntletCI separates **what a rule detected** from **what the user sees**. Several layers run after rule evaluation; each layer is deterministic and configurable. JSON output (`--output json`) and MCP analyze tools include a `delivery` block with drop counts so hidden findings are not silent.

### Two axes: severity and confidence

| Axis | Set by | Purpose |
| --- | --- | --- |
| **Severity** (`RuleSeverity`) | `ConfigurationService` priority chain: `.gauntletci.json` → `.editorconfig` → `DefaultSeverities` | CI gate (`--exit-on`), grouping in console output, delivery ranking |
| **Confidence** (`Confidence`) | Rule implementation via `CreateFinding(...)`; may be boosted by coordination or semantic witnesses | Delivery ranking; `--sensitivity` display filter; LLM enrichment eligibility |

`SeverityOverride` on a finding (when present) wins over config for that instance.

### Pipeline order (after rules fire)

1. **Ignore list** — `.gauntletci-ignore` suppressions by rule ID and file path.
2. **Post-processors** — `IPostProcessor` rules (e.g. GCI0019 large-diff synthesis).
3. **Provenance filter** — `ProvenanceFindingProcessor` drops findings inconsistent with diff provenance (`provenance` config block).
4. **Semantic witnesses** — `SemanticFindingProcessor` may boost confidence when patch semantics support a finding (`semantics` config block).
5. **Domain filter** — `DomainFindingProcessor` drops findings outside the classified repo domain profile (`domain` config block).
6. **Delivery** — `FindingDeliveryProcessor` (see below). Output is the **delivered** finding list stored in `EvaluationResult.Findings`.

LLM enrichment (`--with-llm`) runs **after** delivery, only on `High`-confidence delivered findings.

### Delivery processor (`FindingDeliveryProcessor`)

Controlled by `output.delivery` in `.gauntletci.json` (`FindingDeliveryConfig`). Default: enabled, global max **25**, default **5** findings per (rule, file) group.

Steps (in order):

1. **Coordination boosts** — `FindingCoordinationEngine` raises confidence when compound-risk patterns appear (e.g. GCI0016 present → boost GCI0039/GCI0044; GCI0032 + GCI0003 → boost both). Mutates confidence in a cloned working set only.
2. **File-level demotion** — For rules in `FileLevelRulesToDemote` (default: GCI0001, GCI0003), file-scoped findings with no line number are dropped when any line-anchored finding exists in the run.
3. **Per-rule per-file caps** — Groups by `(RuleId, FilePath)`. Within each group, **all Block findings are kept**; remaining slots up to the cap are filled by highest delivery score among non-Block findings. Per-rule overrides exist for noisy rules (e.g. GCI0038, GCI0043 → cap 3).
4. **Global ranking and cap** — All kept findings sorted by delivery score; global max applied with the same Block-first reservation.

**Delivery score** (higher surfaces first):

```
severity tier (Block=1000, Warn=100, Advisory=50, Info=10)
+ 50 if line-anchored
+ 10 if file path present
+ confidence tier (High=30, Medium=20, Low=0)
```

`FindingDeliverySummary` reports `inputCount`, `outputCount`, and drops by cap, demotion, provenance, and domain filter. Console output prints a yellow delivery notice when findings were hidden; JSON/MCP expose the same fields.

Set `output.delivery.enabled: false` to bypass coordination and caps (full raw list after trust filters).

### Display-time sensitivity (`SensitivityFilter`)

Separate from delivery. `--sensitivity` (`strict` | `balanced` | `permissive`, default `balanced`) filters which **delivered** findings appear in console output and count toward `--exit-on`:

| Threshold | Shown |
| --- | --- |
| **Strict** | Block + Medium or High confidence only |
| **Balanced** (default) | All Block; Warn + Medium or High confidence |
| **Permissive** | All Block and Warn |
| **Advisory** | Always shown at any threshold |
| **Info** | Permissive only; use `--verbose` for explicit Info in other modes |

JSON output includes all delivered findings regardless of sensitivity; the CLI applies sensitivity when rendering text and when computing exit codes.

### Evidence promotion (regex → Roslyn)

Several rules use regex only as a **candidate detector**. Before emitting a finding, `RegexEvidencePromotion` confirms the match against Roslyn syntax (comment/string suppression, member access, object creation, `System.IO.File` sync calls, etc.). GCI0006, GCI0010, GCI0024, GCI0048, GCI0049, and GCI0057 use this path when a syntax tree is available. Diff-only mode (no repo path) falls back to regex guards documented per rule.

### Deterministic vs LLM boundary

| Stage | Deterministic | LLM (opt-in) |
| --- | --- | --- |
| Rule evaluation | Yes | No |
| Trust filters and delivery | Yes | No |
| `--with-llm` explanation text | No | Yes (append-only on High confidence) |
| Corpus labeling / silver labels | Heuristic | Separate offline path |

Product claims about precision should cite **labeled corpus metrics** (`corpus audit-snapshot`, `LabeledRuleMetricsReader`), not discovery-tier trigger rates alone.

---

Because GauntletCI is analyzed by its own rules during development and in CI, some rules fire on the files that implement other rules. This is called **self-interference**: a rule detecting a pattern inside the implementation of another (or the same) rule.

#### Why it happens

Rules analyze raw diff text using text patterns, regex, and line-count heuristics. They have no awareness of which file they are scanning; they apply the same logic to all eligible `.cs` files. When a rule's own implementation uses the exact pattern the rule is designed to detect: or when two rules share overlapping pattern vocabularies: self-interference occurs.

#### Block-severity false positives resolved (April 2026 audit)

A full-codebase audit (`git diff empty-tree..HEAD`, 83 Block findings) revealed the following confirmed false positives, all since resolved:

| Firing rule | Target | Root cause | Resolution |
|---|---|---|---|
| **GCI0046** PatternConsistencyDeviation | `GCI0038_DependencyInjectionSafety.cs` | `ServiceLocatorPatterns` string array contains the exact strings the rule detects | `IsInsideStringLiteral` quote-counting guard in `CheckServiceLocator` |
| **GCI0042** TodoStubDetection | `GCI0042_TodoStubDetection.cs` | `/// GCI0042: TODO/Stub Detection` XML doc comment triggered its own TODO detection | Skip lines starting with `///` |
| **GCI0048** InsecureRandomInSecurityContext | `SyntaxGuard.cs` | Code-example comment `// e.g. $"{new Random().Next()}"`: Roslyn syntax guard is null in diff-only mode | `IsAfterLineComment` fallback guard after Roslyn guard |
| **GCI0049** FloatDoubleEqualityComparison | `LlmAdjudicator.cs` | `(tp + fp) == 0 ? 0.0 : (double)tp / (tp + fp)`: `==` is an integer zero-guard, not a float comparison | `IntegerZeroGuardRegex` to detect safe-division ternaries |
| **GCI0044** PerformanceHotpathRisk | All `Rules/Implementations/*.cs` (~14 files) | Analysis loops (`foreach (var file in diff.Files)`) triggered LINQ-in-loop detection for inner `.FirstOrDefault()`/`.Any()` calls | `IsRuleImplementationFile` path exclusion |
| **GCI0044** PerformanceHotpathRisk | `VectorStore.cs` | `while (reader.Read()) { rows.Add(...) }` ADO.NET reader pattern flagged as unbounded collection growth | `.Read()` detection skips DB reader loops in `CheckAddInsideLoop` |
| **GCI0043** NullabilityTypeSafety | All `Commands/*.cs` files (~12 files) | `GetValueForOption(opt)!` is System.CommandLine's idiomatic required-option pattern (3-48 occurrences per file exceeded the threshold of 1) | Filter excludes `GetValueForOption(` lines from the null-forgiving operator count |

#### Remaining interactions (Warn / Info: not false positives)

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
- `AllAddedLines`: flattens added lines across all files and hunks.
- `AllRemovedLines`: flattens removed lines across all files and hunks.

Per-file helpers on `DiffFile`:
- `AddedLines`, `RemovedLines`: lines within that file only.

`DiffParser` handles both the standard `diff --git` header format and bare unified diff format (e.g., from `git diff` piped through stdin).

---

## Configuration

| File | Purpose |
|---|---|
| `.gauntletci.json` | Per-rule `enabled`/`severity` overrides; `policy_refs`; `llm` block; `forbidden_imports` for GCI0035 |
| `.gauntletci-ignore` | One rule ID per line: suppresses that rule's findings for the entire repo |

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
- **Repo hash**: 8-character SHA-256 prefix of the git remote URL: identifies a repo without revealing its path or name.

### Storage and upload

- `TelemetryStore`: appends NDJSON records to `~/.gauntletci/telemetry.ndjson`.
- `TelemetryUploader`: fires a background HTTP upload (`Shared` mode only). Failures are silently swallowed: telemetry never crashes the tool.

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
- API key read from the environment variable named by `ciApiKeyEnv`: never stored in config.
- Requires a GauntletCI license key in the environment variable named by `licenseKeyEnv`.

### License subscription check

Paid-tier features trigger a remote subscription check against the GauntletCI license worker. Behavior:

| Condition | Result |
| --- | --- |
| Fresh cache (< 24 h, same token) | Uses cached active/inactive status |
| Network error + stale cache | Reuses last known status (revoked licenses stay blocked) |
| Network error + no cache | Fails open, prints a warning; set `GAUNTLETCI_OFFLINE=1` for intentional air-gap |
| `GAUNTLETCI_OFFLINE=1` | Skips network check entirely |

Run `gauntletci license status` to inspect JWT validity and subscription state.

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

### Corpus command factory architecture

The `corpus` command is decomposed into 4 focused factory classes, each responsible for a domain of commands. This pattern reduces complexity (EI-5) and clarifies ownership (EI-4).

```
CorpusCommand (Orchestrator)
├── CorpusOperationsFactory
│   ├── CreateAddPr()          - Add PR to corpus
│   ├── CreateNormalize()      - Normalize corpus data
│   ├── CreateList()           - List corpus contents
│   ├── CreateShow()           - Display corpus details
│   ├── CreateStatus()         - Show corpus status
│   └── CreateBatchHydrate()   - Batch hydrate PRs
│
├── CorpusAnalysisFactory
│   ├── CreateDiscover()       - Discover repositories
│   ├── CreateRun()            - Run analysis on single PR
│   ├── CreateRunAll()         - Batch run analysis
│   ├── CreateScore()          - Score predictions
│   └── CreateReport()         - Generate report
│
├── CorpusLabelingFactory
│   ├── CreateLabel()          - Label single PR
│   ├── CreateLabelAll()       - Batch label PRs
│   └── CreateResetStats()     - Reset labeling statistics
│
└── CorpusUtilityFactory
    ├── CreatePurge()          - Clean up corpus by language
    ├── CreateErrors()         - Show error statistics
    ├── CreateRejectedRepos()  - List rejected repositories
    └── CreateDoctor()         - Diagnostic tool
```

Each factory implements an interface (`ICorpusOperationsFactory`, etc.) that extends the base `ICommandFactory` interface. This enables dependency injection while preserving the stateless factory pattern suitable for command builders.

**Key Benefits:**
- **Reduced Complexity:** CorpusCommand reduced from 3,154 to 1,483 LOC (53% reduction)
- **Clear Ownership:** Each factory owns a specific domain of commands
- **Testability:** 43 unit and integration tests cover factory behavior and CLI routing
- **Maintainability:** Adding new corpus commands requires modification to a single focused factory
- **Extensibility:** New factories can be added following the same pattern

# GauntletCI Architecture Guide

For frequently asked questions, see [FAQ](FAQ.md).

---

## Part 1: Conceptual Architecture

### Technical Architecture: GauntletCI vs. Traditional CI

To understand why GauntletCI is fast and private, it helps to see how it works fundamentally differently from post-commit pipelines:

| Vector | Traditional CI Pipelines (Post-Commit) | GauntletCI (Pre-Commit Inner Loop) |
| --- | --- | --- |
| **Execution Trigger** | Push to remote branch / PR creation. | Local `git commit` hook or manual CLI invocation. |
| **Analysis Scope** | Whole-project compilation and full test-suite execution. | Staged Git diff isolation via localized Roslyn AST parsing. |
| **Feedback Loop** | 5 to 20+ minutes (Context switch required). | **Sub-second (<0.5s)** (Immediate local feedback). |
| **Data Privacy** | Code is transmitted to third-party cloud runners. | **100% Local.** No external API calls, zero data exfiltration. |
| **Target Risk** | Functional regressions (via unit/integration tests). | **Behavioral Change Risk (BCR)** (Silent exception paths, unverified logic). |

### How It Works: The Diff-Isolation Engine

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
3. **Rule Application:** 30+ deterministic rules (e.g., `GCI0003` for guard clause removal, `GCI0007` for swallowed exceptions) evaluate the structural delta between the pre-image and post-image of the code.

This approach means GauntletCI runs before you push, gives instant feedback, and never sees your code outside your machine.

### Roslyn Integration Strategy

GauntletCI makes a deliberate trade-off: **speed and privacy over semantic completeness**.

**What we get:**
- Full access to syntax tree structure (`SyntaxTree`, `SyntaxWalker`, `SyntaxVisitor`)
- Local semantic analysis via targeted `SemanticModel` instantiation
- Direct Git index manipulation (no full repository clone needed)

**What we give up:**
- Cross-project symbol resolution (full `Compilation` graph)
- Binding to external assembly types
- Runtime behavior simulation

This is intentional. Full compilation would require 5–20 seconds and massive memory allocations. Instead, we run in the **pre-commit local loop** in <1 second with zero external dependencies.

---

## Part 2: Memory Optimization

### CSharpSyntaxWalker: Single-Pass AST Analysis

Roslyn syntax trees are memory-intensive. A naive approach using LINQ-to-AST queries causes:
- Multiple intermediate arrays in memory
- Recursive stack frames for deep node hierarchies
- Garbage collection spikes on large files

**GauntletCI's approach:** Single-pass `CSharpSyntaxWalker` structures.

- **Stateful traversal:** Walk the tree once, collecting state as we go
- **TextSpan-aware short-circuiting:** The Git diff tells us the exact modified regions. The walker drops out of parsing sub-trees that fall outside the change boundaries
- **Result:** Memory footprint rarely exceeds a few megabytes, even on 5,000+ line files

This makes GauntletCI performant on monolithic legacy codebases.

---

## Part 3: C# Language Feature Handling

### Source Generators

**The Challenge:** Source generators run at compile time. GauntletCI runs pre-commit, before compilation. Generated code doesn't exist on disk yet.

**The Resolution:** GauntletCI evaluates **human-authored code**, not machine-generated output.

- Rules analyze the partial class signatures, partial methods, and explicit attributes that *you* wrote
- The engine checks whether your manual declarations introduce unvalidated architectural boundaries
- Generated validation (correct mapping, serialization, etc.) is deferred to the standard Roslyn compilation phase
- This keeps the pre-commit loop fast and human-focused

### Control Flow Graph (CFG) Analysis

GauntletCI analyzes **logical execution paths**, not syntactic patterns.

**Example:** Developers write branching logic in many styles:
- Nested `if/else` statements
- Traditional `switch` blocks
- Modern C# `switch` expressions
- Ternary operator chains
- Pattern matching

**GauntletCI's approach:**
- Maps syntax nodes to their underlying logical paths via the Roslyn CFG
- Counts discrete execution branches uniformly
- Detects when cyclomatic complexity increases, regardless of syntactic style
- Flags when new exception paths are introduced, whether via `if(x == null) throw` or modern patterns

This prevents false negatives from developers using modern C# syntax.

### Refactoring Detection: Syntactic Equivalence

**The Challenge:** "Extract Method" refactoring looks like 50 lines deleted + 50 lines added with no test coverage. Is it new unverified logic?

**The Resolution:** Syntactic Equivalence Matching.

- When code blocks are extracted, the engine compares the structural signatures
- It measures cyclomatic complexity, exception profiles, and dependency paths
- If the new method is structurally equivalent to the extracted block, the engine recognizes the refactoring and suppresses the false positive
- Developers can refactor freely without fighting GauntletCI

---

## Part 4: Symbol Resolution Without Full Compilation

GauntletCI uses a **lightweight symbol resolution strategy**:

1. **Local File Context:** Load the complete `SyntaxTree` for modified files (you have full structural context within that file)
2. **Project File Parsing:** Read the `.csproj` to resolve direct internal project references and assembly metadata (no MSBuild workspace)
3. **Basic Type Caching:** Cache basic type information for common .NET types and internal classes
4. **Graceful Degradation:** If a rule requires deep semantic binding, it's excluded from pre-commit and deferred to post-commit CI

**Example:**
- A rule detecting removed null-checks: Works perfectly (syntax-level analysis)
- A rule detecting breaking API changes: Works well (project file resolution)
- A rule detecting cross-assembly type binding issues: Deferred to compiler (too expensive)

---

## Part 5: Implementation Details

### Project Layout

| Project | Role |
|---|---|
| `GauntletCI.Core` | Rule engine, diff parser, static analysis runner, configuration models, domain types |
| `GauntletCI.Cli` | System.CommandLine entry point, output formatters, telemetry pipeline, all CLI commands |
| `GauntletCI.Llm` | ONNX runtime integration (Phi-4 Mini); `NullLlmEngine` is the default no-op |
| `GauntletCI.Corpus` | Corpus ingestion pipeline: pull request hydration, normalization, scoring |
| `GauntletCI.Tests` | xUnit test suite for Core and Cli |
| `GauntletCI.BenchmarkReporter` | Benchmark report generation |
| `GauntletCI.Benchmarks` | BenchmarkDotNet harness (in `/tests/`) |

### Analysis Pipeline (Per-Run Flow)

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
5. Post-processing          RuleOrchestrator.PostProcess()
                            Aggregates findings, applies baseline filters, and
                            formats output (JSON, Sarif, markdown, console).
```

---

## Further Reading

- **[CLI Reference](cli-reference.md)** - Complete command-line usage
- **[Rule Documentation](rules/README.md)** - All 50+ detection rules
- **[Architecture Decision Records](architecture/)** - Detailed design trade-offs
- **[FAQ](FAQ.md)** - Frequently asked technical questions

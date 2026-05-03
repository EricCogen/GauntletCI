# Development Guide

This guide covers building GauntletCI from source, running tests, and understanding the project layout.

---

## Prerequisites

| Requirement | Version | Notes |
|-------------|---------|-------|
| [.NET SDK](https://dotnet.microsoft.com/download/dotnet/8.0) | 8.0.x or later | Required to build and run |
| Git | Any recent version | For cloning and corpus pipeline |
| GitHub PAT | Optional | For `corpus maintainers fetch` |

No other global dependencies are required. The project uses `GauntletCI.slnx` (Visual Studio solution file).

---

## Clone and build

```bash
git clone https://github.com/EricCogen/GauntletCI.git
cd GauntletCI
dotnet build GauntletCI.slnx
```

Expected output: all projects build cleanly, 0 errors.

---

## Run tests

```bash
# Run all tests
dotnet test GauntletCI.slnx --nologo

# Quick quiet run (shows pass/fail counts only)
dotnet test GauntletCI.slnx --nologo -q

# Run with code coverage
dotnet test GauntletCI.slnx --nologo --collect:"XPlat Code Coverage" --results-directory ./TestResults
```

### Test categories

| Project | Purpose |
|---------|---------|
| `GauntletCI.Tests` | Unit tests for rules, diff parser, config, LLM templates (~570 tests) |
| `GauntletCI.Benchmarks` | Curated fixture tests: true/false positive regression checks |

### Opt-in tests

Some tests require environment variables to run:

| Env var | Enables |
|---------|---------|
| `GAUNTLETCI_HALLUCINATION_PROBE=1` | Phi-4 Mini hallucination probe (loads 2.6 GB ONNX model, ~6 min) |
| `ANTHROPIC_API_KEY` | Anthropic-backed LLM integration tests |
| `OPENAI_API_KEY` | OpenAI-compatible LLM integration tests |

Without these, the affected tests return early and count as passed.

---

## Project layout

```
GauntletCI.slnx
├── src/
│   ├── GauntletCI.Core/        # Rule engine, diff parser, configuration, models
│   │   ├── Rules/              # IRule interface, RuleBase, RuleOrchestrator
│   │   │   └── Implementations/  # One file per active rule (GCI0001_*.cs …)
│   │   ├── Diff/               # DiffParser, DiffContext, DiffFile, DiffLine
│   │   ├── Analysis/           # AnalysisContext, FileEligibilityFilter
│   │   ├── Configuration/      # GauntletConfig, ConfigLoader, schema
│   │   └── Model/              # Finding, Confidence, EvaluationResult
│   ├── GauntletCI.Cli/         # System.CommandLine CLI (commands, banner, telemetry)
│   │   ├── Commands/           # One file per command group
│   │   └── Telemetry/          # TelemetryEvent, TelemetryStore, ConsentMode
│   ├── GauntletCI.Llm/         # LLM integration
│   │   ├── LocalLlmEngine.cs   # ONNX Phi-4 Mini (CPU/DirectML)
│   │   ├── RemoteLlmEngine.cs  # OpenAI-compatible REST endpoint
│   │   ├── NullLlmEngine.cs    # No-op default (no model required)
│   │   ├── PromptTemplates.cs  # Phi-4 chat format prompts
│   │   └── Embeddings/         # OllamaEmbeddingEngine, VectorStore, Distillery
│   └── GauntletCI.Corpus/      # Corpus pipeline (hydration, labeling, storage)
│       └── Storage/            # CorpusDb, SchemaInitializer, FixtureIdHelper
├── tests/
│   ├── GauntletCI.Tests/       # xUnit unit tests
│   └── GauntletCI.Benchmarks/  # Curated fixture benchmark tests
├── data/
│   └── gauntletci-corpus.db    # SQLite corpus database (fixtures, findings, labels)
├── docs/                       # Documentation
└── schemas/
    └── gauntletci-schema.json  # JSON Schema for .gauntletci.json (IDE IntelliSense)
```

---

## Running the CLI locally

After building, run the CLI directly from the output directory:

```bash
dotnet run --project src/GauntletCI.Cli -- analyze --staged
```

Or install as a local tool:

```bash
dotnet pack src/GauntletCI.Cli -o ./nupkg
dotnet tool install --global --add-source ./nupkg GauntletCI
```

---

## Key conventions

- **SPDX header**: all source files start with `// SPDX-License-Identifier: Elastic-2.0`
- **Namespaces**: `GauntletCI.{Project}.{Area}` (e.g., `GauntletCI.Core.Rules`)
- **Commit tags**: `[RULE]`, `[CLI]`, `[CONFIG]`, `[TEST]`, `[INFRA]`, `[DOCS]`: see [CONTRIBUTING.md](../.github/CONTRIBUTING.md)
- **Rule IDs**: never reuse a retired ID; check `docs/rules.md` for the current registry

---

## Adding a rule

See the [Contributing guide](../.github/CONTRIBUTING.md#writing-a-new-rule) for a step-by-step walkthrough.

---

## Adding a corpus command

Corpus commands are organized into 4 factory classes. To add a new corpus subcommand:

1. **Choose the appropriate factory** based on command domain:
   - **CorpusOperationsFactory**: CRUD operations, batch operations (add-pr, normalize, list, show, status, batch-hydrate)
   - **CorpusAnalysisFactory**: Discovery, analysis, scoring, reporting (discover, run, run-all, score, report)
   - **CorpusLabelingFactory**: Labeling and training (label, label-all, reset-stats)
   - **CorpusUtilityFactory**: Utilities, diagnostics, cleanup (purge, errors, rejected-repos, doctor)

2. **Add a method to the factory** following the naming convention `CreateXxx()`:

   ```csharp
   public static Command CreateNewCommand()
   {
       var cmd = new Command("new-command", "Description of the new command");
       
       // Add options
       var repoOption = new Option<string>("--repo", "Repository reference");
       cmd.AddOption(repoOption);
       
       // Add handler
       cmd.SetHandler(async (repo, ctx) => {
           try {
               // Implementation here
               Console.WriteLine($"Processing {repo}");
               await Task.CompletedTask;
           } catch (Exception ex) {
               ctx.ExitCode = 1;
               Console.Error.WriteLine($"Error: {ex.Message}");
           }
       }, repoOption, new InvocationContextBinder());
       
       return cmd;
   }
   ```

3. **Register in CorpusCommand.Create()**:

   ```csharp
   var corpus = new Command("corpus", "...");
   
   // Add to appropriate factory group
   corpus.AddCommand(newFactory.CreateNewCommand());
   ```

4. **Add unit tests** to `CorpusCommandFactoriesTests.cs`:

   ```csharp
   [Fact]
   public void NewCommandFactory_CreateNewCommand_ReturnsValidCommand()
   {
       var cmd = factory.CreateNewCommand();
       
       Assert.NotNull(cmd);
       Assert.Equal("new-command", cmd.Name);
       Assert.NotEmpty(cmd.Options);
   }
   ```

5. **Add integration test** to verify CLI routing:

   ```csharp
   [Fact]
   public void CorpusCommand_HasNewCommand()
   {
       var cmd = CorpusCommand.Create();
       var subcommands = cmd.Subcommands.Select(c => c.Name).ToList();
       
       Assert.Contains("new-command", subcommands);
   }
   ```

6. **Run tests** to verify:

   ```bash
   dotnet test GauntletCI.slnx --filter "CorpusCommandFactoriesTests"
   ```

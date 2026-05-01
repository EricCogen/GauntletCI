# Contributing to GauntletCI

GauntletCI is a diff-first change-risk detection tool for C# and .NET. It focuses on behavior-changing code diffs, validation gaps, breaking-change risk, edge-case gaps, error-handling changes, and other high-signal risks.

## Ways to contribute

- Report false positives
- Submit risky diffs
- Suggest rules
- Improve documentation
- Add demo scenarios
- Improve tests
- Fix bugs

## Before opening an issue

Please do not include secrets, credentials, private customer data, proprietary code, or anything you do not have permission to share.

When possible, reduce examples to the smallest safe diff that demonstrates the issue.

## Reporting false positives

Use the [Report a false positive](https://github.com/EricCogen/GauntletCI/issues/new?template=false_positive.yml) issue template.

Include:

- GauntletCI version
- Rule ID
- Finding output
- Minimal sanitized diff
- Why the finding was noisy or incorrect

## Submitting risky diffs

Use the [Submit a risky diff](https://github.com/EricCogen/GauntletCI/issues/new?template=risky_diff.yml) issue template.

Good examples usually include:

- A small code change
- A behavior change
- A validation gap
- A reason the change could pass tests or review

## Suggesting rules

Use the [Rule request](https://github.com/EricCogen/GauntletCI/issues/new?template=rule_request.yml) issue template.

A good rule request explains:

- The risky pattern
- Why the pattern matters
- Example diff
- Potential false positives
- Suggested category

## Signal quality philosophy

GauntletCI is designed for high-signal findings.

A finding is not a claim that the code is definitely broken. A finding is evidence that a diff introduced behavior worth validating.

Rules should avoid style opinions and broad whole-repo scanning. The focus is change risk.

## License

GauntletCI is licensed under the Elastic License 2.0.

By contributing, you agree that your contribution may be included under the repository license.

---

## Developer guide

The sections below cover the technical workflow, commit conventions, and a step-by-step rule writing tutorial.

---

## Getting started

1. Fork the repository and clone locally
2. Build: `dotnet build GauntletCI.slnx`
3. Test: `dotnet test GauntletCI.slnx --nologo -q`
4. Create a branch: `git checkout -b feature/<short-description>`
5. Open an issue before starting large changes: alignment first saves rework

See [docs/DEVELOPMENT.md](../docs/DEVELOPMENT.md) for full build and project layout details.

---

## Repository organization

### Permanent rule: Temporary and internal files must be in `.misc/`

All analysis scripts, phase-related work, debugging utilities, and internal documentation that are not part of the public codebase **must be stored in the `.misc/` folder**. This includes:

- Phase analysis scripts (`phase*.py`, `phase*.json`, etc.)
- Debugging and investigation scripts (`debug-*.py`, `check-*.py`, `analyze-*.py`, etc.)
- Database synchronization utilities (`sync-*.py`, `recompute-*.py`)
- Internal documentation and notes
- Spotchecks, validation tools, and temporary fixtures

These files are kept locally for development purposes but are gitignored and not committed to the public repository.

**Why:** This keeps the public repo clean, focused on production code, and prevents accumulation of development artifacts that clutter the browsing experience.

---

## Commit conventions

Every commit must have a bracketed tag prefix:

| Tag | When to use |
|-----|-------------|
| `[RULE]` | Adding, modifying, or deleting a rule file or its tests |
| `[CONFIG]` | Configuration models, loaders, or JSON schema |
| `[CLI]` | CLI layer changes (`GauntletCI.Cli`) |
| `[TEST]` | Test-only changes (no production code touched) |
| `[INFRA]` | CI/CD, build, packaging, or tooling |
| `[DOCS]` | Documentation-only changes |

**Example:** `[RULE] Add GCI0043 Missing Rate Limit Guard`

All tests must pass before submitting a PR:

```bash
dotnet test GauntletCI.slnx --nologo -q
```

---

## Rule inclusion criteria

GauntletCI rules detect **behavioral risk** in diffs, not style. A rule is eligible if it meets all of the following from [CHARTER.md](../CHARTER.md):

- The pattern has caused production incidents in real systems
- It is detectable from the diff alone (no runtime information required)
- It produces a low false-positive rate on typical PRs
- The finding is actionable: a developer can fix it immediately

Rules that check formatting, naming conventions, or code style are **not eligible**.

---

## Writing a new rule

### 1. Choose an ID

Check `docs/rules.md` for the current registry. Pick the next unused `GCI00XX` ID. Never reuse a retired ID.

### 2. Create the rule file

Create `src/GauntletCI.Core/Rules/Implementations/GCI00XX_YourRuleName.cs`:

```csharp
// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Diff;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI00XX: Your Rule Name
/// One-sentence description of what this rule detects.
/// </summary>
public class GCI00XX_YourRuleName : RuleBase
{
    public override string Id   => "GCI00XX";
    public override string Name => "Your Rule Name";

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var findings = new List<Finding>();

        foreach (var file in context.Diff.Files)
        {
            foreach (var line in file.AddedLines)
            {
                if (!line.Content.Contains("YOUR_PATTERN", StringComparison.Ordinal))
                    continue;

                findings.Add(CreateFinding(
                    file:            file,
                    line:            line,
                    summary:         "Short description of what was found.",
                    evidence:        line.Content.Trim(),
                    whyItMatters:    "Why this pattern is risky in production.",
                    suggestedAction: "Concrete step the developer can take to fix it.",
                    confidence:      Confidence.Medium));
            }
        }

        return Task.FromResult(findings);
    }
}
```

**No registration step is needed.** `RuleOrchestrator.CreateDefault()` discovers all `IRule` implementations in the assembly via reflection automatically.

### 3. Key APIs

**`AnalysisContext`**: passed to every rule:

| Property | Type | Description |
|----------|------|-------------|
| `context.Diff` | `DiffContext` | The full diff, pre-filtered to eligible files |
| `context.Diff.Files` | `IList<DiffFile>` | All changed files |
| `context.EligibleFiles` | `IReadOnlyList<ChangedFileAnalysisRecord>` | File classification metadata |
| `context.StaticAnalysis` | `AnalyzerResult?` | Optional static analysis results (may be null) |

**`DiffFile`**: a changed file:

| Property | Description |
|----------|-------------|
| `file.NewPath` | File path after the change |
| `file.AddedLines` | Lines added in this diff |
| `file.RemovedLines` | Lines removed in this diff |
| `file.Hunks` | All hunks (each hunk has `.Lines` with `+`/`-`/` ` context) |

**`CreateFinding()` overloads:**

```csharp
// Diff-wide finding (no file/line attribution)
CreateFinding(summary, evidence, whyItMatters, suggestedAction, confidence);

// File-level finding
CreateFinding(file, summary, evidence, whyItMatters, suggestedAction, confidence);

// Line-level finding (most precise)
CreateFinding(file, summary, evidence, whyItMatters, suggestedAction, confidence, line);
```

**`Confidence` enum:** `High`, `Medium`, `Low`
- `High`: pattern is almost certainly a problem; reviewer should block
- `Medium`: likely a problem; reviewer should verify
- `Low`: possible concern; reviewer should be aware

### 4. Configurable rules (optional)

If your rule needs config (e.g., user-supplied lists), implement `IConfigurableRule`:

```csharp
public class GCI00XX_YourRuleName : RuleBase, IConfigurableRule
{
    private GauntletConfig _config = new();

    public void Configure(GauntletConfig config) => _config = config;

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        // Access _config.Rules["GCI00XX"] etc.
    }
}
```

See `GCI0035_ArchitectureLayerGuard.cs` for a real example using `ForbiddenImports`.

### 5. Write tests

Create `src/GauntletCI.Tests/Rules/GCI00XXTests.cs`:

```csharp
// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Diff;
using GauntletCI.Core.Rules.Implementations;
using Xunit;

namespace GauntletCI.Tests.Rules;

public class GCI00XXTests
{
    private static Task<List<Finding>> Run(string rawDiff)
    {
        var rule = new GCI00XX_YourRuleName();
        var diff = DiffParser.Parse(rawDiff);
        var context = new AnalysisContext { Diff = diff };
        return rule.EvaluateAsync(context);
    }

    [Fact]
    public async Task TruePositive_PatternPresent_ShouldFlag()
    {
        var findings = await Run("""
            diff --git a/src/MyFile.cs b/src/MyFile.cs
            index abc..def 100644
            --- a/src/MyFile.cs
            +++ b/src/MyFile.cs
            @@ -1,3 +1,4 @@
             namespace MyApp;
            +YOUR_PATTERN_HERE
            """);

        Assert.Single(findings);
        Assert.Equal("GCI00XX", findings[0].RuleId);
    }

    [Fact]
    public async Task FalsePositive_SafePattern_ShouldNotFlag()
    {
        var findings = await Run("""
            diff --git a/src/MyFile.cs b/src/MyFile.cs
            index abc..def 100644
            --- a/src/MyFile.cs
            +++ b/src/MyFile.cs
            @@ -1,3 +1,4 @@
             namespace MyApp;
            +SAFE_EQUIVALENT_HERE
            """);

        Assert.Empty(findings);
    }
}
```

Cover at minimum: one true positive, one false positive, and one edge case (empty diff, test file exclusion, etc.).

### 6. Update docs/rules.md

Add an entry for your rule in the appropriate section of `docs/rules.md`. Follow the existing format:

```markdown
### GCI00XX: Your Rule Name

| | |
|---|---|
| **Confidence** | Medium |
| **Fires when** | Description of the trigger condition |
| **Does not fire** | Safe patterns that are excluded |
| **Why it matters** | Production risk explanation |
| **Suggested action** | What the developer should do |
```

### 7. Submit

```bash
git add -A
git commit -m "[RULE] Add GCI00XX Your Rule Name"
git push origin feature/gci00xx-your-rule-name
```

Open a pull request. The CI workflow will run `dotnet test` on Ubuntu, Windows, and macOS.

---

## Retiring a rule

If a rule is no longer useful:

1. Delete the rule file from `src/GauntletCI.Core/Rules/Implementations/`
2. Delete the test file from `src/GauntletCI.Tests/Rules/`
3. Mark the rule as retired in `docs/rules.md` (do **not** delete the entry; the ID is permanently reserved)
4. Commit with `[RULE] Retire GCI00XX <reason>`

Never reuse a retired rule ID.

---

## Questions

Open an issue or start a discussion on GitHub.

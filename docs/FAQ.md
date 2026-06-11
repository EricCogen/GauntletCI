# Frequently Asked Questions

## General Questions

### How can a diff-only tool have full context? Doesn't it miss the surrounding code?

GauntletCI uses Git to isolate *which* files have staged modifications, but it does not evaluate those files as raw text snippets. The engine loads the **entire source file** into a local Roslyn `SyntaxTree`.

This means rules have full structural context within the modified files. For example, if a rule flags a potential unhandled exception path introduced inside a modified code block, it automatically walks up the syntax tree node ancestors to check if an enclosing `try/catch` block further up the file already mitigates the risk.

### What about cross-project or semantic breaking changes?

Because GauntletCI optimizes for fast local feedback in the pre-commit inner loop, it skips full project compilation and assembly linking.

* **What it catches:** Behavioral Change Risk (BCR) within the logical units you are actively modifying—such as silent exception leaks, algorithmic shifts, or structural regressions lacking test coverage.
* **What it leaves to your CI/Compiler:** Cross-assembly breaking changes (e.g., changing a method signature in Project A that breaks an uncompiled consumer in Project B).

GauntletCI is designed to act as a fast, localized safety net *before* your compiler or remote CI pipeline handles whole-project verification.

### We already use IDE Analyzers (SonarLint/Roslynators). Why do we need this?

Traditional analyzers evaluate the **state** of an entire codebase, which frequently leads to massive warning fatigue on legacy systems—developers end up suppressing or ignoring them.

GauntletCI evaluates the **delta**. It introduces a temporal constraint: it only executes rules against code that is actively being staged for a commit. It doesn't nag you about historical technical debt; it strictly prevents the introduction of *new, unvalidated behavioral risk paths*.

---

## Technical Deep-Dive (For Architects)

*These FAQs address technical objections based on validated GauntletCI behavior.*

### How do you handle Roslyn `Compilation` dependencies without running a full project build? If you don't build, how do you resolve external symbols?

**The Hard Truth:** If you don't compile, you do not have a full `SemanticModel`. If a rule relies heavily on cross-project symbol resolution or binding external types, a pure syntax-isolated pass will face limitations.

**The Architecture:** GauntletCI is explicitly optimized for **structural and local semantic analysis**.

* For pure syntactic rules (e.g., catching structural changes without corresponding test alterations), the engine operates instantly on the `SyntaxTree`.
* For rules requiring basic type resolution, GauntletCI reads the target project file (`.csproj`) to resolve direct internal references and caches basic metadata without spinning up a heavy MSBuild workspace instance.
* If a behavioral rule *strictly* requires a fully bound, cross-assembly semantic graph to execute without false positives, that rule is intentionally excluded from the pre-commit engine and deferred to traditional post-commit compilation suites. We choose execution speed over multi-project type linking.

### Pre-commit hooks are notoriously hated because they slow down quick, iterative commits. How does GauntletCI compare?

**The Friction:** Tools like `dotnet format` or full-suite hooks are notorious inner-loop killers, often taking 5–15 seconds even on modest changes because they spin up the full .NET runtime context or scan the entire solution directory.

**The Performance:** GauntletCI executes in **<1.5 seconds** on typical staged changes.

* It bypasses the MSBuild workspace loader entirely.
* It reads the Git index directly, isolating *only* the specific files containing staged blocks.
* If you stage a 10-line change in a solution containing 150 projects and 10,000 files, Roslyn only parses the syntax trees for the *exact files* changed.

### How do you prevent "Hook Fatigue"? If the tool blocks a legitimate, high-velocity commit, developers will just run `git commit --no-verify`.

**The Friction:** The moment a pre-commit utility throws a false positive or interrupts a developer during an active debugging/hotfix flow, they will bypass it permanently.

**The Governance:** GauntletCI is engineered around a **Pessimistic, Non-Intrusive Model**.

* **Zero Style/Linting Rules:** The engine does not block commits for tabs vs. spaces, naming conventions, or formatting issues. It *only* blocks on explicit **Behavioral Change Risk (BCR)** indicators—such as adding complex conditional logic or exposing unhandled exception paths without adjusting associated verification files.
* **Granular Rule Suppression:** Teams can explicitly suppress a rule directly within the codebase using standard, granular comments (e.g., `// gauntlet-disable GCI0032`) or via a localized `.gauntletci` configuration file, allowing valid architectural exceptions without bypassing the entire gate.

### We have massive codebases with complex solution structures (multi-targeting, generated code, shared source files). How does GauntletCI parse these without choking?

**The Architecture:**

* **Generated Code Exclusion:** By default, the engine uses strict pattern-matching to automatically ignore files containing standard autogenerated headers (e.g., Protobuf outputs, EF Core Migrations, Source Generators, `*.designer.cs`). This ensures your change-risk metrics are never skewed by machine-written infrastructure.
* **Shared/Multi-Targeted Files:** Because GauntletCI evaluates code at the syntactic AST layer rather than loading a compiled assembly binary, it parses conditional compilation symbols (`#if NET8_0`, `#if WINDOWS`) structurally. A rule evaluates the syntax paths within all defined blocks, ensuring coverage regardless of the underlying compilation target configuration.

### How does GauntletCI recognize refactorings like Extract Method? Won't it flag legitimate code movements as "new unverified logic"?

**The Pattern Recognition:** When a developer uses IDE refactoring tools (like "Extract Method"), code blocks are moved but not behaviorally altered.

**The Engine Behavior:** GauntletCI evaluates **structural signatures and syntactic equivalence**. When it detects code movement without semantic alteration—such as extracting 50 lines of logic into a new private method—the engine recognizes the refactoring pattern and avoids throwing false-positive behavioral change alerts.

This means developers can refactor freely without fighting false positives.

### How does GauntletCI treat equivalent logic written in different C# syntax styles?

**The Challenge:** A rule states that complex branching logic requires corresponding test updates. One developer writes nested `if/else` statements; another writes a compact C# switch expression using relational patterns. To a basic analyzer, these look completely different.

**The Engine Resolution:** GauntletCI does not evaluate raw token patterns. It analyzes **logical execution paths**.

* Whether the branching logic is written as an elegant C# switch expression, a traditional switch block, or a chain of ternary operators, the engine maps the syntax nodes to their underlying logical execution paths.
* If the number of discrete execution paths increases within the modified code, the engine registers a behavioral change signal, regardless of syntactic style.

### What happens if the code inside the staged Git diff contains syntax errors?

**The Edge Case:** A developer is mid-refactor and attempts to make a partial commit of a file that currently has missing braces, unclosed parentheses, or broken expressions.

**The Resolution:** GauntletCI is engineered to be **Fault-Tolerant and Non-Crashing**.

* Roslyn's parser can build a full syntax tree even from broken source code by introducing "Diagnostic Nodes" to patch holes.
* When GauntletCI encounters severe syntax diagnostics, it evaluates severity. If code is too broken to reliably determine structural behavior, the engine gracefully aborts analysis for that file and permits the commit with a warning that validation was skipped.
* The tool never crashes or blocks a developer due to an unexpected parsing error.

### Does GauntletCI analyze only the modified lines, or does it load entire files?

**The Approach:** GauntletCI loads the **entire source file** into a local Roslyn `SyntaxTree`, but uses Git diff boundaries (`TextSpan`) to focus rule evaluation.

**Why:** This gives rules full structural context. For example, if a rule flags a potential unhandled exception path introduced inside a modified code block, it automatically walks up the syntax tree node ancestors to check if an enclosing `try/catch` block further up the file already mitigates the risk.

The Git index optimization ensures that only files with staged modifications are processed, keeping execution fast even on monorepos.

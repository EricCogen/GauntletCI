# The GauntletCI Charter

## A Framework for Behavioral Risk Auditing in .NET

### 0. Current State

GauntletCI is an evolving system.

The deterministic rule engine is implemented and actively tested against a growing fixture corpus. The **Corpus of Failure** is under active construction, with ongoing ingestion of real-world pull requests, linked issues, and documented .NET runtime failure cases.

This Charter defines the methodology and direction as the system matures. It is the philosophical anchor—the principles do not change, but the evidence base deepens over time.

---

### 1. Preamble: The Crisis of Confidence

The software development landscape of 2026 is defined by unprecedented velocity. AI-assisted coding tools have democratized the act of creation, generating syntactically flawless code at a scale previously unimaginable.

Yet, a shadow accompanies this acceleration. Engineering teams report a troubling divergence: CI pipelines glow green with passing tests and clean linter logs, while "unexplained" production incidents rise in frequency and severity. This phenomenon is the **Coverage Mirage**—the false sense of security derived from high quantitative metrics that conceal systemic, behavioral fragility.

Code can be formatted perfectly, pass every unit test, and satisfy every rule in a traditional SAST scanner, while still harboring hidden async deadlocks, resource exhaustion vectors, or silent data corruption paths specific to the .NET Common Language Runtime.

**Thesis:** GauntletCI exists to restore confidence in the pull request. We reject the optimistic assumption that "clean code is safe code." Instead, we adopt the stance of the **Pessimistic Verifier**, auditing every change for its capacity to *survive* the known, documented failure modes of the .NET ecosystem.

---

### 2. Core Tenets of the Pessimistic Verifier

The following principles are non-negotiable. They form the bedrock of every rule, every heuristic, and every line of code within GauntletCI.

**I. Coverage is Not Correctness**
A green unit test proves the code executed a path. It does not prove the code released its resources, respected the `SynchronizationContext`, or handled an exceptional cancellation. We audit the space *between* the lines.

**II. Falsification Over Verification**
Traditional tools ask: *"Is this code compliant with Style X?"*
We ask a more rigorous question: *"Given the known failure vectors of the .NET runtime, does this implementation survive?"* We seek to falsify the hypothesis of safety.

**III. Intent is Material Context**
A code change is not an isolated event; it is a response to a requirement. When available, linked Issues and commit messages provide critical context that enhances the fidelity of behavioral auditing. The absence of Intent does not block analysis—it simply reduces the confidence interval of the adjudication.

**IV. Privacy is Absolute**
Behavioral auditing is a deep inspection of proprietary logic. It must never require code exfiltration to a third-party cloud. Intelligence lives at the edge. GauntletCI's reasoning is local-first, ensuring zero trust and zero data leakage.

**V. Determinism Anchors Intelligence**
Large Language Models are powerful but non-deterministic. GauntletCI uses a local, small-parameter model **exclusively for context mapping and explanation**. The enforcement of a Hard Fail is always governed by deterministic Roslyn-based rules (`GCI0001`–`GCI0037`). The model advises; the analyzer enforces.

---

### 3. The Methodology of Evidence-Driven Auditing

GauntletCI is not "Semantic Grep." It is a structured analytical pipeline designed to test code against a curated corpus of failure.

#### 3.1 The Corpus of Failure

Our engine is fueled by a corpus of .NET anti-patterns. This corpus is not derived from abstract coding standards; it is built from two converging sources:

1.  **Top-Down Authority:** Official Microsoft .NET Runtime Documentation, post-mortems of critical production outages, and CVEs related to .NET library misuse.
2.  **Bottom-Up Empiricism:** Real-world Pull Requests and their linked Issues. By analyzing the delta between *Intent* (Issue) and *Implementation* (PR), GauntletCI continuously refines its understanding of how safe-looking code can silently introduce behavioral risk.

This dual-source approach ensures the corpus is both **authoritative** (grounded in runtime truth) and **empirical** (validated against the messy reality of day-to-day development).

#### 3.2 The Evidence-Driven Validation Loop

For every change set analyzed, GauntletCI executes the following process:

1.  **Parse Implementation:** The Roslyn engine constructs a semantic model of the Pull Request diff, identifying changed methods, dependency graphs, and lifecycle management.
2.  **Parse Intent (where available):** Natural Language Processing (local ONNX) extracts key requirements and constraints from the linked GitHub Issue or Commit Message.
3.  **Cross-Reference Failure Modes:** The system maps the semantic change to the relevant subset of the **Corpus of Failure**.
4.  **Adjudicate Survival:** The tool issues a verdict. Did the implementation *survive* the test? Or did it introduce a new vulnerability to a known failure mode?

#### 3.3 The Role of the Local Model (Phi-3-Mini ONNX)

The local model is not a code generator. It is a **Contextual Adjudicator**. Its sole purpose is to bridge the gap between the deterministic Roslyn rule violation and the human-readable context of the pull request. It provides the *explanation* of *why* a specific line of code is a high-risk vector in this *specific* scenario, referencing the Charter's tenets. This ensures the "Expert Adjudication" is grounded in the Charter, not hallucination.

---

### 4. The Behavioral Risk Spectrum

To provide actionable intelligence, GauntletCI classifies findings into a clear risk hierarchy.

| Classification | Severity | Definition | Example |
| :--- | :--- | :--- | :--- |
| **🔴 GCI-HARD-FAIL** | **Existential** | Deterministic violation of a rule with a proven, high-probability path to crash, data loss, or security breach. | `async void` method in a non-event handler context; `SqlConnection` not disposed in an exception path. |
| **🟡 GCI-INTENT-MISMATCH** | **Semantic Drift** | The code is syntactically correct and "clean," but it fails to satisfy the acceptance criteria of the linked Issue or introduces a side-effect contrary to the stated goal. | Issue requests "Add caching to reduce latency." Implementation adds a static `Dictionary` with no expiration policy (Memory Leak Vector). |
| **🔵 GCI-CONTEXT-WARNING** | **Environmental Fragility** | The code is safe in a vacuum but demonstrates high risk when analyzed against the surrounding architecture or the PR's aggregate changes. | Use of `Task.Run` on a known UI thread without `ConfigureAwait(false)`; async method lacking `CancellationToken` propagation. |

---

### 5. Industry Positioning

GauntletCI does not seek to replace the foundational tools of modern development. It seeks to complete them.

| Category | Relationship to GauntletCI |
| :--- | :--- |
| **Generators (Copilot, Cursor)** | **Complementary.** These tools optimize for *Write Velocity*. GauntletCI optimizes for *Change Survivability*. We audit what they generate. |
| **Linters / SAST (SonarQube, Snyk)** | **Differentiated.** These tools focus on *Syntactic Vulnerabilities* and *Style*. GauntletCI focuses on *Runtime Behavioral Risk* specific to the .NET CLR. |
| **Frontier Reasoning (DeepSeek, o1)** | **Applied.** We leverage the broader advancements in AI reasoning but constrain them to a local, domain-specific, and privacy-preserving adjudication model. |

---

### 6. Governance and Evolution

The integrity of GauntletCI relies on the discipline of its rule set. This Charter governs the introduction and maintenance of all Behavioral Risk Auditing rules (`GCIXXXX`).

**Criteria for New Rule Inclusion:**
A proposal for a new `GCI` rule must satisfy at least one of the following evidential standards:

1.  **Runtime Authority:** Direct citation of official Microsoft .NET documentation or runtime repository guidance warning against the specific pattern.
2.  **Empirical Outage:** A documented post-mortem or widely-corroborated incident report from the .NET community where this pattern resulted in a significant production failure.
3.  **Security Vulnerability:** A link to a CVE directly attributable to the misuse pattern in a .NET application context.
4.  **PR/Issue Recurrence:** A demonstrated pattern observed across multiple real-world Pull Requests and linked Issues where the same behavioral defect evaded traditional review and static analysis.

**Adjudication of False Positives:**
GauntletCI is a "Pessimistic Verifier," but it must not be a tyrannical one. Users may challenge a `HARD-FAIL` by demonstrating that the specific implementation context mitigates the documented failure mode. The local model will provide the reasoning path, allowing the human architect to make the final, informed override decision.

---

### 7. Ratification

This Charter is ratified by the maintainers of GauntletCI. It serves as the immutable foundation for all development, rule curation, and adjudication logic. To contribute to GauntletCI is to uphold these principles.

*End of Charter*
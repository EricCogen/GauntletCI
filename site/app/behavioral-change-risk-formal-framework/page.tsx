import type { Metadata } from "next";
import Link from "next/link";
import { Header } from "@/components/header";
import { Footer } from "@/components/footer";
import { RulesApplied } from "@/components/rules-applied";
import { AuthorBio } from "@/components/author-bio";

export const metadata: Metadata = {
  title: "Behavioral Change Risk: A Formal Framework for Validation Gaps in Evolving Software",
  description:
    "A formal definition of Behavioral Change Risk (BCR) and Behavioral Change Risk Validation (BCRV): the methodology for detecting validation gaps introduced by code changes before they reach production.",
  alternates: { canonical: "/behavioral-change-risk-formal-framework" },
  openGraph: { images: [{ url: "/og/why-tests-miss-bugs.png", width: 1200, height: 630 }] },
};

const jsonLd = {
  "@context": "https://schema.org",
  "@type": "ScholarlyArticle",
  headline: "Behavioral Change Risk: A Formal Framework for Validation Gaps in Evolving Software",
  description:
    "Formal definition of Behavioral Change Risk (BCR) and Behavioral Change Risk Validation (BCRV), a diff-centric methodology for detecting validation gaps introduced by code changes.",
  url: "https://gauntletci.com/behavioral-change-risk-formal-framework",
  author: { "@type": "Person", name: "Eric Cogen" },
  publisher: { "@type": "Organization", name: "GauntletCI", url: "https://gauntletci.com" },
};

function Cite({ n }: { n: number }) {
  return (
    <sup>
      <a href={`#cite-${n}`} className="text-cyan-400 hover:text-cyan-300 font-mono text-xs">
        [{n}]
      </a>
    </sup>
  );
}

export default function BCRFormalFrameworkPage() {
  return (
    <>
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLd) }} />
      <Header />
      <main className="min-h-screen bg-background pt-24">
        <div className="mx-auto max-w-4xl px-4 sm:px-6 lg:px-8 py-16 sm:py-20 space-y-16">

          {/* Hero */}
          <div className="space-y-5 border-b border-border pb-12">
            <div className="flex items-center justify-between">
              <p className="text-sm font-semibold uppercase tracking-widest text-cyan-400">Technical Report</p>
              <Link href="/articles" className="text-sm text-muted-foreground hover:text-cyan-400 transition-colors">← All articles</Link>
            </div>
            <h1 className="text-4xl sm:text-5xl font-bold tracking-tight text-balance">
              Behavioral Change Risk: A Formal Framework for Validation Gaps in Evolving Software
            </h1>
            <p className="text-xl text-muted-foreground leading-relaxed text-pretty">
              What tests miss, why green builds lie, and how to audit change.
            </p>
            <div className="flex items-center gap-2 pt-1">
              <span className="text-sm font-medium text-foreground">Eric Cogen</span>
              <span className="text-muted-foreground/40 text-sm">·</span>
              <span className="text-sm text-muted-foreground">Founder, GauntletCI</span>
              <span className="text-muted-foreground/40 text-sm">·</span>
              <time className="text-sm text-muted-foreground" dateTime="2026-04-21">April 21, 2026</time>
            </div>
            <nav className="flex items-center justify-between pt-2 text-sm border-t border-border/50">
              <Link href="/detect-breaking-changes-before-merge" className="flex items-center gap-1 text-muted-foreground hover:text-cyan-400 transition-colors">
                <span aria-hidden="true">‹</span> Detect Breaking Changes Before Merge
              </Link>
              <Link href="/articles" className="flex items-center gap-1 text-muted-foreground hover:text-cyan-400 transition-colors">
                All articles <span aria-hidden="true">›</span>
              </Link>
            </nav>
          </div>

          {/* Abstract */}
          <section className="space-y-4">
            <h2 className="text-xs font-bold uppercase tracking-widest text-muted-foreground/60">Abstract</h2>
            <div className="rounded-xl border border-border bg-card/40 p-6 space-y-4 text-sm text-muted-foreground leading-relaxed">
              <p>
                Modern continuous integration (CI) pipelines rely heavily on automated test suites to validate software
                changes. A passing test suite is widely interpreted as a signal of correctness. However, a growing body
                of empirical research demonstrates that test suites are structurally incapable of detecting specific
                classes of behavioral modification. This gap, wherein a code change alters runtime behavior without
                triggering a test failure, represents a distinct and under-researched category of software risk.
              </p>
              <p>
                This article defines <strong className="text-foreground">Behavioral Change Risk (BCR)</strong> and
                proposes <strong className="text-foreground">Behavioral Change Risk Validation (BCRV)</strong>, a
                complementary methodology focused on the systematic analysis of code change semantics rather than the
                verification of existing assertions. The article synthesizes recent findings from the software
                engineering literature, including the diagnostic value of flaky test failures and the limitations of
                code coverage metrics, to establish the intellectual foundation for BCRV as a necessary practice in the
                maintenance of evolving software systems.
              </p>
              <p>
                The primary contributions are: (1) the formalization of{" "}
                <strong className="text-foreground">Behavioral Change Risk (BCR)</strong> as a distinct software risk
                category, and (2) the introduction of{" "}
                <strong className="text-foreground">Behavioral Change Risk Validation (BCRV)</strong> as a structured,
                diff-centric methodology for detecting and mitigating BCR before it reaches production.
              </p>
            </div>
          </section>

          {/* Section 1 */}
          <section className="space-y-5">
            <h2 className="text-2xl font-bold tracking-tight">1. Introduction</h2>
            <p className="text-muted-foreground leading-relaxed">
              The software industry has invested decades in refining the practice of automated testing. Unit tests,
              integration tests, and end-to-end tests form the backbone of modern CI/CD pipelines. The logic is
              intuitive: if a code change does not break any existing tests, the change is presumed safe. This binary
              signal, green build or red build, governs the decision to merge, deploy, and release software to
              production. This paper refers to the implicit assumption that a passing build implies behavioral
              correctness as the{" "}
              <strong>Green Build Validity Assumption</strong>: a heuristic that is operationally useful but
              theoretically unsound.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              Yet production incidents occur. Bugs ship. And often, the post-mortem reveals a disquieting fact: the
              test suite was green.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              This phenomenon is not merely a failure of test coverage. It is a structural limitation in how test
              suites validate software behavior. Tests are oracles for <em>expected</em> behavior. They assert what the
              software must do. They are silent on behaviors that were never explicitly specified as assertions.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              Recent academic research has begun to quantify this blindness. A 2023 study of the Chromium continuous
              integration system found that flaky tests, traditionally dismissed as noise, were responsible for
              detecting over one-third of all regression faults. When these flaky failures were filtered out by
              automated tooling, <strong>76.2% of real faults were missed</strong>.<Cite n={1} /> Separately, an
              empirical study on automated program repair found that patches passing all available tests were
              frequently <strong>semantically incorrect</strong>, because the test suite under-specified the correct
              behavior.<Cite n={2} />
            </p>
            <p className="text-muted-foreground leading-relaxed">
              These findings point to a gap in the software validation landscape. This article names that gap,
              formalizes its definition, and proposes a methodology to address it.
            </p>
          </section>

          {/* Section 2 */}
          <section className="space-y-5">
            <h2 className="text-2xl font-bold tracking-tight">2. The Structural Blindness of Test Suites</h2>
            <p className="text-muted-foreground leading-relaxed">
              To understand why tests miss certain bugs, one must first understand what a test can and cannot verify.
              A test case consists of three components: an input, an execution, and an oracle: an assertion that
              evaluates the output. The <strong>oracle problem</strong><Cite n={5} /> states that a test can only
              detect a fault if that fault produces an observable output that violates a specific, pre-written
              assertion.
            </p>
            <div className="rounded-lg border border-border bg-card/50 p-5">
              <p className="text-sm font-semibold text-cyan-400 mb-2">The oracle problem, in brief</p>
              <p className="text-sm text-muted-foreground leading-relaxed">
                A test suite cannot detect what it was never written to expect. Correctness is bounded by the
                completeness of prior specification.
              </p>
            </div>
            <p className="text-muted-foreground leading-relaxed">
              Consider a developer who removes a null-check guard clause; the code changes from{" "}
              <code className="text-xs bg-secondary px-1 py-0.5 rounded">if (user == null) return;</code> to a state
              where the guard is simply absent. If the test suite never exercises the code path with a null user, all
              tests will continue to pass. The behavior of the system has changed: it will now throw a{" "}
              <code className="text-xs bg-secondary px-1 py-0.5 rounded">NullReferenceException</code> where it
              previously handled the condition gracefully, but no test will fail. The change is{" "}
              <em>invisible</em> to the validation mechanism.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              This is not a coverage problem. The line of code may have been executed by other tests. It is an{" "}
              <strong>oracle problem</strong>. The test suite never asserted that the system should handle null input
              safely; it merely assumed the system would not crash under the tested inputs. This is the simplest
              possible instance of Behavioral Change Risk.
            </p>

            <h3 className="text-xl font-semibold tracking-tight pt-2">2.1 The Limits of Code Coverage</h3>
            <p className="text-muted-foreground leading-relaxed">
              Code coverage is frequently used as a proxy for test suite quality. Coverage is a necessary baseline:
              code that is never executed cannot be tested at all, and low coverage is a reliable signal of
              undertested paths. Yet a 2018 study investigating faults missed by high-coverage test suites found
              that <strong>coverage metrics do not correlate with fault detection for several important bug
              classes</strong>.<Cite n={3} /> Specifically, missing guard clauses, logic inversions, and missing
              assignments were systematically missed even when line and branch coverage exceeded 90%.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              Coverage measures execution. It does not measure <em>behavioral specification</em>.
            </p>

            <h3 className="text-xl font-semibold tracking-tight pt-2">2.2 The Limits of Mutation Testing</h3>
            <p className="text-muted-foreground leading-relaxed">
              Mutation testing, which introduces artificial faults to evaluate test suite sensitivity, is the most
              rigorous proxy for test suite effectiveness currently available. However, recent work has shown that
              traditional mutation operators do not adequately model real-world faults.<Cite n={4} /> Many real faults
              involve the <em>removal</em> of behavior, a change that is difficult to simulate with standard
              syntactic mutants.
            </p>
          </section>

          {/* Section 3 */}
          <section className="space-y-5">
            <h2 className="text-2xl font-bold tracking-tight">3. Defining Behavioral Change Risk (BCR)</h2>
            <p className="text-muted-foreground leading-relaxed">
              The gap described above can be formalized. Let a codebase be denoted as{" "}
              <code className="text-xs bg-secondary px-1 py-0.5 rounded font-mono">C</code>. A change set{" "}
              <code className="text-xs bg-secondary px-1 py-0.5 rounded font-mono">ΔC</code> represents a
              modification to that codebase. The observable behavior space of the codebase is{" "}
              <code className="text-xs bg-secondary px-1 py-0.5 rounded font-mono">B(C)</code>. A test suite{" "}
              <code className="text-xs bg-secondary px-1 py-0.5 rounded font-mono">T</code> validates a subset of
              that behavior space, denoted{" "}
              <code className="text-xs bg-secondary px-1 py-0.5 rounded font-mono">V(T, C)</code>.
            </p>
            <div className="rounded-xl border border-cyan-500/20 bg-cyan-500/5 p-6 space-y-4">
              <p className="text-sm font-semibold text-cyan-400">Formal definition: Behavioral Change Risk (BCR)</p>

              <p className="text-sm text-muted-foreground leading-relaxed">
                BCR is defined as a condition where both of the following hold:
              </p>

              <ol className="space-y-2 text-sm text-muted-foreground">
                <li className="flex gap-3">
                  <span className="shrink-0 font-mono text-cyan-400">1.</span>
                  <span>
                    <code className="bg-background/60 px-2 py-0.5 rounded font-mono text-xs">
                      B(C + ΔC) ≠ B(C)
                    </code>
                    : the modification alters observable behavior; and
                  </span>
                </li>

                <li className="flex gap-3">
                  <span className="shrink-0 font-mono text-cyan-400">2.</span>
                  <span>
                    <code className="bg-background/60 px-2 py-0.5 rounded font-mono text-xs">
                      ΔB ∉ V(T, C + ΔC)
                    </code>
                    : the altered behavior is not covered by the test suite.
                  </span>
                </li>
              </ol>

              <p className="text-sm text-muted-foreground/70 italic leading-relaxed border-t border-border pt-3">
                BCR arises when the system's behavior space extends beyond what is validated by tests.
              </p>

              <p className="text-sm text-muted-foreground/70 italic leading-relaxed">
                <span className="font-semibold">More formally</span>:<br />
                  For a given system, behavior B is a function of code C. Any non-zero change ΔC introduces behavioral divergence potential such that B(C + ΔC) ≠ B(C).
              </p>
            </div>
            <p className="text-muted-foreground leading-relaxed">
              In plain terms: <strong>BCR is the divergence between what the system does and what the tests can see.</strong>{" "}
              It exists whenever <code className="text-xs bg-secondary px-1 py-0.5 rounded font-mono">B(C + ΔC)</code>{" "}
              expands beyond <code className="text-xs bg-secondary px-1 py-0.5 rounded font-mono">V(T, C + ΔC)</code>,
              the actual behavior space of the modified system outgrowing the validated behavior space of its
              test suite.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              This definition distinguishes BCR from a traditional software defect. A <em>bug</em> is code that
              violates a stated requirement; it is detectable because a test or specification can be written to catch
              it. BCR is categorically different: the code may behave exactly as the developer intended, and yet
              introduce behavioral change that no existing test is positioned to observe. It is not a failure of
              implementation. It is a <strong>validation gap</strong>.
            </p>
            <div className="rounded-lg border border-border bg-card/50 p-5 space-y-2">
              <p className="text-sm font-semibold text-foreground">Scope boundary</p>
              <p className="text-sm text-muted-foreground leading-relaxed">
                BCR as defined here is bounded to changes in functional, observable behavior detectable in principle
                by a correctly written test oracle. It explicitly excludes:{" "}
                <strong>performance regressions</strong> (changes in execution speed, memory, or throughput that do
                not alter observable outputs);{" "}
                <strong>security vulnerabilities</strong> (weaknesses requiring threat-model analysis beyond
                behavioral assertion); and{" "}
                <strong>concurrency hazards</strong> (race conditions, deadlocks, and non-deterministic interleavings
                outside the scope of a functional test oracle). BCR addresses the specific gap between what a change
                does and what the test suite is positioned to see.
              </p>
            </div>
          </section>

          {/* Section 4 */}
          <section className="space-y-5">
            <h2 className="text-2xl font-bold tracking-tight">4. A Taxonomy of Behavioral Change</h2>
            <p className="text-muted-foreground leading-relaxed">
              Behavioral changes that escape test detection can be categorized. The following taxonomy, derived from
              recurring patterns documented in production incident analyses and the empirical studies cited in §2,
              provides a structured lens for analysis.
            </p>
            <div className="overflow-x-auto rounded-xl border border-border">
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b border-border bg-card/60">
                    <th className="text-left px-4 py-3 font-semibold text-foreground w-1/4">Category</th>
                    <th className="text-left px-4 py-3 font-semibold text-foreground w-2/5">Description</th>
                    <th className="text-left px-4 py-3 font-semibold text-foreground">Example</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-border">
                  {[
                    {
                      cat: "Removed Guard Clause",
                      desc: "A defensive condition is deleted, exposing the system to previously handled edge cases.",
                      ex: 'Deletion of if (input == null) return;',
                    },
                    {
                      cat: "Stricter Condition",
                      desc: "A logical operator is tightened, excluding previously valid inputs.",
                      ex: "Changing age > 18 to age >= 21",
                    },
                    {
                      cat: "Implicit Contract Change",
                      desc: "The order of side effects or the timing of state mutations is altered without changing return values.",
                      ex: "Reordering cache invalidation and database write",
                    },
                    {
                      cat: "Error Handling Alteration",
                      desc: "The system's response to exceptional conditions is modified, but the exception path is untested.",
                      ex: "Changing catch (Exception) to catch (SpecificException)",
                    },
                    {
                      cat: "State Transition Modification",
                      desc: "The rules governing state machine transitions are updated, but only the happy path is tested.",
                      ex: "Removing a validation check before state advancement",
                    },
                    {
                      cat: "Configuration Drift",
                      desc: "A change relies on an environment variable or setting that is absent in production.",
                      ex: "Adding a feature flag that defaults to false in tests but true in staging",
                    },
                  ].map((row) => (
                    <tr key={row.cat} className="bg-card/20 hover:bg-card/40 transition-colors">
                      <td className="px-4 py-3 font-medium text-foreground align-top">{row.cat}</td>
                      <td className="px-4 py-3 text-muted-foreground align-top leading-relaxed">{row.desc}</td>
                      <td className="px-4 py-3 text-muted-foreground align-top">
                        <code className="text-xs">{row.ex}</code>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <p className="text-sm text-muted-foreground">
              Each of these changes can pass a thorough test suite while introducing meaningful behavioral risk.
            </p>
          </section>

          {/* Section 5 */}
          <section className="space-y-5">
            <h2 className="text-2xl font-bold tracking-tight">5. The Diagnostic Value of Flaky Tests</h2>
            <p className="text-muted-foreground leading-relaxed">
              One of the most counter-intuitive findings in recent software engineering research concerns flaky
              tests, which exhibit non-deterministic behavior, passing and failing without apparent code
              changes. The conventional engineering response is to quarantine, disable, or automatically retry
              flaky tests to reduce CI noise.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              The Chromium study challenges this practice.<Cite n={1} /> The researchers analyzed over 1.5 million
              test executions across 14,000 commits. They found:
            </p>
            <div className="space-y-3">
              <div className="rounded-lg border border-amber-500/20 bg-amber-500/5 p-4">
                <p className="text-sm text-amber-300 leading-relaxed">
                  <strong>Flaky tests exposed more than one-third of all regression faults</strong> in the Chromium
                  system.
                </p>
              </div>
              <div className="rounded-lg border border-amber-500/20 bg-amber-500/5 p-4">
                <p className="text-sm text-amber-300 leading-relaxed">
                  State-of-the-art flakiness detection tools, while achieving 99.2% precision,{" "}
                  <strong>caused 76.2% of real regression faults to be missed</strong>.
                </p>
              </div>
            </div>
            <p className="text-sm text-muted-foreground">
              The Chromium CI system is substantially larger than most industrial codebases; the precise fault
              suppression rate will vary by system. The directional finding, that automated flakiness filtering
              discards genuine fault signal, is corroborated by independent work on non-deterministic test
              behavior.<Cite n={8} />
            </p>
            <p className="text-muted-foreground leading-relaxed">
              A flaky test is often a test that is <em>sensitive</em> to a behavioral change that deterministic
              tests ignore. It may fail due to a timing shift, a resource contention issue, or an altered execution
              order, all of which are genuine behavioral changes. By silencing the flaky test, the CI system
              silences the <strong>signal</strong>.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              This finding has direct implications for the BCR framework. A flaky test failure is not noise to be
              suppressed; it is an <strong>early indicator</strong> of unvalidated behavioral change. Behavioral
              Change Risk Validation incorporates this insight by treating flaky failures as diagnostic artifacts
              rather than engineering nuisances.
            </p>
          </section>

          {/* Section 6 */}
          <section className="space-y-5">
            <h2 className="text-2xl font-bold tracking-tight">6. Preliminary Corpus Analysis</h2>
            <p className="text-muted-foreground leading-relaxed">
              The theoretical case for BCR rests on structural arguments about what tests can and cannot detect.
              A preliminary empirical signal supports the framework&apos;s practical relevance.
            </p>

            <div className="space-y-4">
              <div className="rounded-lg border border-border bg-card/40 p-5">
                <p className="text-sm font-semibold text-foreground mb-2">Corpus construction</p>
                <p className="text-sm text-muted-foreground leading-relaxed">
                  A corpus of <strong>598 pull requests from 57 open-source .NET repositories</strong> was assembled
                  using GauntletCI&apos;s corpus pipeline. Repositories were identified via GitHub code search across
                  the .NET ecosystem; the full set includes Polly, Dapper, Newtonsoft.Json, Avalonia, PowerShell,
                  dotnet/aspnetcore, dotnet/efcore, dotnet/maui, dotnet/roslyn, and dotnet/runtime, among others.
                  Pull requests were selected with a bias toward changes involving substantive review activity, which
                  introduces a selection effect favoring higher-complexity changes over routine maintenance commits.
                  Each pull request was evaluated against the behavioral change taxonomy described in §4 using the
                  automated rule engine. The corpus metadata (repository names, pull request numbers, size
                  classification, test-change presence, and per-finding counts) is published at{" "}
                  <a
                    href="https://github.com/EricCogen/GauntletCI/blob/main/data/corpus-fixtures.csv"
                    className="text-cyan-400 hover:text-cyan-300 underline underline-offset-2 break-all"
                    target="_blank"
                    rel="noopener noreferrer"
                  >
                    github.com/EricCogen/GauntletCI/blob/main/data/corpus-fixtures.csv
                  </a>{" "}
                  for independent review and replication.
                </p>
              </div>

              <div className="rounded-lg border border-border bg-card/40 p-5">
                <p className="text-sm font-semibold text-foreground mb-2">Test file classification</p>
                <p className="text-sm text-muted-foreground leading-relaxed">
                  The field <code className="text-xs bg-background/60 px-1 rounded">has_tests_changed</code> was
                  determined by automated path-pattern classification: a pull request was marked as including test
                  changes if any modified file matched test naming conventions, including files with{" "}
                  <code className="text-xs bg-background/60 px-1 rounded">Tests.cs</code>,{" "}
                  <code className="text-xs bg-background/60 px-1 rounded">.test.cs</code>, or{" "}
                  <code className="text-xs bg-background/60 px-1 rounded">.tests.cs</code> suffixes, or files
                  residing in <code className="text-xs bg-background/60 px-1 rounded">/test/</code> or{" "}
                  <code className="text-xs bg-background/60 px-1 rounded">/tests/</code> path segments. This is a
                  structural proxy, not a measure of whether behavioral assertions were added or updated.
                </p>
              </div>

              <div className="rounded-lg border border-border bg-card/40 p-5">
                <p className="text-sm font-semibold text-foreground mb-2">Confidence scoring</p>
                <p className="text-sm text-muted-foreground leading-relaxed">
                  GauntletCI assigns each finding one of three internal confidence tiers: 0.25 (low), 0.5 (medium),
                  or 1.0 (high). The high-confidence tier (1.0) reflects the strongest structural pattern match; it
                  does not represent external validation or human review. Lower tiers were excluded from the primary
                  counts below to reduce noise from ambiguous matches.
                </p>
              </div>
            </div>

            <p className="text-sm font-semibold text-foreground">Two findings emerge from this analysis:</p>
            <div className="grid sm:grid-cols-2 gap-4">
              <div className="rounded-xl border border-cyan-500/30 bg-cyan-500/5 p-5">
                <p className="text-3xl font-bold text-cyan-400 mb-1">34.6%</p>
                <p className="text-sm text-muted-foreground leading-relaxed">
                  of pull requests (207 of 598) contained at least one high-confidence behavioral risk indicator,
                  spanning 11 distinct rule categories.
                </p>
              </div>
              <div className="rounded-xl border border-cyan-500/30 bg-cyan-500/5 p-5">
                <p className="text-3xl font-bold text-cyan-400 mb-1">71%</p>
                <p className="text-sm text-muted-foreground leading-relaxed">
                  of pull requests submitted without test file modifications (118 of 166) contained at least one
                  behavioral risk indicator. When test authorship effort is absent, risk patterns are not merely
                  possible; they are prevalent.
                </p>
              </div>
            </div>

            <div className="rounded-lg border border-border bg-card/40 p-5">
              <p className="text-sm font-semibold text-foreground mb-2">Methodological limitations</p>
              <p className="text-sm text-muted-foreground leading-relaxed">
                The current corpus carries no human-labeled ground truth. Precision and recall of these rates are
                unknown: findings represent automated pattern matches, and false positives are expected. The corpus
                was not a random sample of production software; it was drawn from well-maintained open-source
                projects with active code review histories, which may exhibit different behavioral change patterns
                than closed-source enterprise or legacy codebases. Formal empirical validation (including human
                labeling of findings, precision and recall measurement, and controlled studies across broader
                repository populations) is identified as future work.
              </p>
            </div>

            <p className="text-muted-foreground leading-relaxed">
              The preliminary signal is nonetheless consistent with the BCR framework&apos;s central prediction:
              behavioral risk patterns occur in a substantial fraction of real-world pull requests, and their
              incidence is elevated precisely in the pull requests that arrive without test coverage updates: the
              gap that BCRV is designed to address.
            </p>
          </section>

          {/* Section 7 */}
          <section className="space-y-5">
            <h2 className="text-2xl font-bold tracking-tight">7. Introducing Behavioral Change Risk Validation (BCRV)</h2>
            <p className="text-muted-foreground leading-relaxed">
              The Chromium data makes the case directly: a CI pipeline achieving 99.2% precision in flakiness
              detection still caused 76.2% of real regression faults to be missed.<Cite n={1} /> A green build, in
              that system, was an unreliable signal. If state-of-the-art tooling on one of the world&apos;s largest
              CI systems cannot be trusted to surface behavioral regression, the implication is clear:{" "}
              <strong>the diff must be audited independently of what the test suite reports.</strong>
            </p>

            <h3 className="text-xl font-semibold tracking-tight pt-2">7.1 Requirements for Addressing BCR</h3>
            <p className="text-muted-foreground leading-relaxed">
              Before introducing the methodology, the requirements it must satisfy are worth stating explicitly,
              since they emerge from the problem, not from the solution.
            </p>
            <div className="space-y-3">
              {[
                {
                  n: "1",
                  label: "Diff-scoped",
                  detail: "Analysis must be anchored to the change, not the full codebase. BCR is introduced by a specific modification; the audit must match that scope to remain tractable.",
                },
                {
                  n: "2",
                  label: "Semantics-aware",
                  detail: "The methodology must reason about behavioral meaning (what the code does and what it no longer does) rather than structural properties such as line count or test coverage percentage alone.",
                },
                {
                  n: "3",
                  label: "Validation-aware",
                  detail: "Findings must be interpreted in relation to the existing test suite. A behavioral change that is fully covered by updated assertions carries low risk; a behavioral change that is unobserved by any assertion represents an unresolved gap.",
                },
                {
                  n: "4",
                  label: "Low integration cost",
                  detail: "Pre-merge validation that imposes significant workflow friction will be bypassed. An effective BCR methodology must integrate into existing review and CI practices without requiring new infrastructure or cultural upheaval.",
                },
              ].map((req) => (
                <div key={req.n} className="flex gap-4 rounded-lg border border-border bg-card/40 p-4">
                  <span className="shrink-0 text-xs font-mono text-cyan-400 mt-0.5">{req.n}.</span>
                  <div className="space-y-1">
                    <p className="text-sm font-semibold text-foreground">{req.label}</p>
                    <p className="text-sm text-muted-foreground leading-relaxed">{req.detail}</p>
                  </div>
                </div>
              ))}
            </div>
            <p className="text-sm text-muted-foreground">
              These requirements do not emerge from any particular tool or workflow preference. They emerge from the
              structure of the problem itself. A methodology that satisfies all four addresses BCR at its root.
            </p>

            <p className="text-muted-foreground leading-relaxed pt-2">
              <strong>Behavioral Change Risk Validation (BCRV)</strong> is a methodology for systematically
              evaluating the behavioral implications of a code change before or during the review process. It shifts
              the unit of analysis from <em>test results</em> to <em>code semantics</em>.
            </p>
            <div className="rounded-lg border border-border bg-card/60 p-5">
              <p className="text-sm font-semibold text-cyan-400 mb-2">The core principle of BCRV</p>
              <p className="text-base font-medium text-foreground leading-relaxed">
                A code change must be audited for behavioral risk independently of test suite output.
              </p>
            </div>
            <p className="text-muted-foreground leading-relaxed">
              This is not a replacement for testing. It is an augmentation. BCRV acknowledges that tests are a
              partial specification and that the <strong>diff</strong> contains information about behavioral intent
              that tests cannot fully capture.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              BCRV is also the economically rational choice. Auditing a diff at the moment of authorship requires
              evaluating tens or hundreds of changed lines in context. The alternative, discovering the behavioral
              gap in production, requires reproducing the fault, tracing it back through deployment history, and
              remediating under pressure. The <strong>engineering tax</strong> of a pre-commit audit is a fraction
              of the cost of a post-incident post-mortem. Shift-left is not a slogan; it is arithmetic.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              It is worth stating explicitly: BCRV is{" "}
              <strong>complementary to Test-Driven Development</strong>, not a replacement for it. TDD builds the
              behavioral specification: it encodes what the system must do before the code is written. BCRV audits
              the evolution of that specification: it asks whether a subsequent change has altered behavior in ways
              the original specification no longer covers. The two practices address different moments in the
              software lifecycle: TDD governs creation; BCRV governs change.
            </p>
            <p className="text-sm text-muted-foreground leading-relaxed">
              The term &quot;audit-driven&quot; has appeared informally in prior software engineering discourse
              (e.g., &quot;Audit Driven Design&quot; in 2007, &quot;Audit-Driven SRE&quot; in 2026), and it is
              worth distinguishing those uses from the methodology proposed here. While BCRV does not carry the
              word &quot;audit&quot; in its name, it shares a commitment to structured examination. The prior uses
              are retrospective and organizationally oriented. BCRV is a{" "}
              <strong>pre-merge validation discipline</strong> applied to the code diff, concerned with behavioral
              risk to a running system rather than with organizational visibility or post-incident remediation.
            </p>

            <h3 className="text-xl font-semibold tracking-tight pt-4">7.2 The BCRV Workflow</h3>
            <p className="text-muted-foreground leading-relaxed">
              BCRV can be integrated into existing development practices with minimal disruption. The workflow
              consists of three stages:
            </p>
            <figure className="my-6 rounded-xl overflow-hidden border border-border bg-card/30 p-6 text-center">
              <img
                src="/articles/bcrv-workflow-diagram.svg"
                alt="BCRV Three-Stage Workflow: Diff Analysis feeds into Impact Assessment, which feeds into Risk Mitigation. Risk Mitigation branches into three outcomes: Add an Assertion (preferred), Document Accepted Risk (intentional tradeoff), or Revert or Redesign (unintended consequence)."
                className="w-full max-w-2xl mx-auto h-auto"
              />
              <figcaption className="mt-3 text-xs text-muted-foreground/60">
                <strong className="text-muted-foreground">Figure 1.</strong> The BCRV three-stage workflow. Every
                flagged diff change resolves to exactly one of three outcomes.
              </figcaption>
            </figure>
            <div className="space-y-4">
              {[
                {
                  stage: "Stage 1: Diff Analysis",
                  detail: "The developer or reviewer examines the change set with a specific focus on removed or altered logic, not just added code. Deletions of conditional branches, changes to loop boundaries, and modifications to error handling are flagged for deeper scrutiny.",
                },
                {
                  stage: "Stage 2: Behavioral Impact Assessment",
                  detail: "For each flagged change, the reviewer asks: Does this change alter the system's response to a specific input or state? Is that input or state represented in the existing test suite? If not, is the new behavior intentional and documented?",
                },
                {
                  stage: "Stage 3: Risk Mitigation",
                  detail: "If a behavioral change is identified as unvalidated, one of three actions is taken: (1) Add an assertion to capture the new behavior. (2) Document the accepted risk: record the change as intentional with explicit justification. (3) Revert or redesign the change to eliminate the unvalidated behavioral shift.",
                },
              ].map((s) => (
                <div key={s.stage} className="rounded-lg border border-border bg-card/40 p-5 space-y-2">
                  <p className="text-sm font-semibold text-foreground">{s.stage}</p>
                  <p className="text-sm text-muted-foreground leading-relaxed">{s.detail}</p>
                </div>
              ))}
            </div>

            <h3 className="text-xl font-semibold tracking-tight pt-4">7.3 Tooling Considerations</h3>
            <p className="text-muted-foreground leading-relaxed">
              While BCRV is a human-centric methodology, tooling can assist in flagging high-risk change patterns.
              A prototype implementation, <strong>GauntletCI</strong>, was developed to explore the feasibility of
              automated BCR detection. The prototype analyzes code diffs to identify structural patterns associated
              with BCR categories.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              Existing static analysis tools such as Semgrep, CodeQL, and SonarQube perform pattern-based analysis
              across the full codebase and offer partial overlap with automated BCR detection. The distinguishing
              characteristic of a BCR-oriented tool is that analysis is scoped to the diff rather than the full
              repository, and the integration point is pre-commit rather than post-merge, ensuring findings are
              surfaced at the moment of lowest remediation cost.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              This reflects a principled division of labor. Automated tooling excels at pattern recognition: it
              can reliably flag that a guard clause was removed or that an exception handler was narrowed. What it
              cannot determine is whether that removal was intentional, whether the edge case is reachable in
              production, or whether the behavioral shift is acceptable given the system&apos;s current requirements.
              That semantic judgment belongs to the human auditor.{" "}
              <strong>GauntletCI surfaces the <em>what</em>. The developer is responsible for the <em>why</em>.</strong>
            </p>
          </section>

          {/* Section 8 */}
          <section className="space-y-5">
            <h2 className="text-2xl font-bold tracking-tight">8. Related Work</h2>
            <p className="text-muted-foreground leading-relaxed">
              The limitations of test suites have been documented extensively. Inozemtseva and Holmes<Cite n={6} />{" "}
              demonstrated that coverage metrics are a poor predictor of test suite effectiveness. Just et al.<Cite n={7} />{" "}
              showed that mutation testing, while valuable, does not fully capture real-world fault characteristics.
              The oracle problem was formally surveyed by Barr et al.,<Cite n={5} /> establishing the theoretical
              bound on test-based validation. The core bound, that a test can only detect faults observable through
              pre-written assertions, has not been substantially revised; subsequent work has focused on automated
              oracle generation as a mitigation rather than a challenge to the bound itself.<Cite n={9} />
            </p>

            <h3 className="text-xl font-semibold tracking-tight pt-2">8.1 Change-Aware Testing and Test Gap Analysis</h3>
            <p className="text-muted-foreground leading-relaxed">
              The relationship between code change and test coverage has been studied as a distinct problem from
              global coverage metrics. Test Gap Analysis (TGA) examines the alignment between code modifications
              and the tests that exercise those modifications, independently of aggregate coverage measurements. An
              industrial study by Eder et al. found that a substantial proportion of modified code paths ship
              without corresponding test updates, and that error probability in untested changed code is
              significantly higher than in changed code accompanied by test modifications.<Cite n={10} />{" "}
              Contemporary work has extended TGA to risk-based prioritization, enabling teams to triage uncovered
              changes by defect likelihood rather than treating all test gaps equally.<Cite n={11} />
            </p>
            <p className="text-muted-foreground leading-relaxed">
              Change-aware testing, which restricts testing effort to the scope of a specific change rather than
              the full system, has a parallel history in unit testing research. Wloka et al. introduced JUnitMX,
              a change-aware unit testing tool that uses a change model to guide the authoring of new tests in
              direct response to specific code modifications.<Cite n={12} /> The tool operationalizes the principle
              that test authoring effort should be directed by what changed, not by what exists, an orientation
              that anticipates the diff-centric analysis proposed by BCRV.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              Behavioral Regression Testing (BERT), introduced by Orso, Xie, and Jin, takes a dynamic approach:
              it executes an existing test suite against both the pre-change and post-change versions of a system
              and flags behavioral divergences.<Cite n={13} /> BERT detects differences observable through executed
              assertions, but is structurally bounded by the oracle problem<Cite n={5} />: if no test exercises a
              changed code path, the divergence remains invisible. This limitation motivates the pre-merge,
              diff-side analysis that BCRV proposes.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              More recently, LLM-based approaches have extended change-aware reasoning to GUI testing.
              RippleGUItester applies change-impact analysis to direct LLM-driven GUI exploration toward regions
              of an application affected by a specific commit.<Cite n={14} /> An evaluation across four
              open-source applications identified 26 previously unknown defects, demonstrating that change-scoped
              exploration substantially outperforms undirected test generation in surface area relevance.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              This article contributes to that literature by defining BCR as a distinct risk category and
              proposing BCRV as a structured response. Unlike prior work focused on test generation or oracle
              improvement, BCRV addresses the <strong>diff-side</strong> of the validation equation: the change
              itself.
            </p>
          </section>

          {/* Section 9 */}
          <section className="space-y-5">
            <h2 className="text-2xl font-bold tracking-tight">9. Limitations and Future Work</h2>
            <p className="text-muted-foreground leading-relaxed">
              Behavioral Change Risk Validation is a methodology, not a formal verification technique. It relies
              on human judgment and does not guarantee the absence of behavioral risk. The taxonomy presented is
              descriptive, not exhaustive. Additional categories of behavioral change may emerge as the practice
              is applied across diverse codebases.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              Two additional limitations warrant explicit acknowledgment. First, BCRV depends on the quality of
              the human auditor. A reviewer who lacks domain knowledge of the changed system may fail to recognize
              the behavioral significance of a flagged pattern, a risk that increases as codebases grow and team
              ownership becomes diffuse. Second, <strong>audit fatigue</strong> is a real operational concern. If
              every diff surfaces a large number of flags, reviewers will begin to dismiss findings as noise,
              recreating the same suppression problem that motivates the methodology. Effective BCRV practice
              requires tuning signal quality: surfacing fewer, higher-confidence flags rather than exhaustive
              pattern lists.
            </p>
            <div className="rounded-lg border border-border bg-card/40 p-5">
              <p className="text-sm font-semibold text-foreground mb-3">Future work</p>
              <ul className="space-y-2 text-sm text-muted-foreground">
                {[
                  "Empirical measurement of BCR prevalence in industrial codebases.",
                  "Development of lightweight static analysis rules to flag high-BCR change patterns.",
                  "Integration of flaky test signal analysis into BCRV workflows.",
                ].map((item) => (
                  <li key={item} className="flex gap-2">
                    <span className="text-cyan-400 shrink-0">: </span>
                    <span>{item}</span>
                  </li>
                ))}
              </ul>
            </div>
          </section>

          {/* Section 10 */}
          <section className="space-y-5">
            <h2 className="text-2xl font-bold tracking-tight">10. Threats to Validity</h2>

            <h3 className="text-xl font-semibold tracking-tight">Internal Validity</h3>
            <p className="text-muted-foreground leading-relaxed">
              The primary threat to internal validity is interpretation bias in the BCR taxonomy. The six change
              categories in §4 were derived from pattern analysis and practitioner judgment rather than a
              systematic fault taxonomy study. Categories may overlap, under-specify, or conflate distinct
              phenomena. Additionally, the corpus findings reported in §6 are produced by an automated rule engine
              with no human-labeled ground truth: the correlation between GauntletCI&apos;s confidence scores and
              actual behavioral divergence has not been empirically established. The selection bias in the corpus,
              favoring pull requests with substantive review activity, may inflate the observed BCR rate
              relative to a random sample of commits.
            </p>

            <h3 className="text-xl font-semibold tracking-tight">External Validity</h3>
            <p className="text-muted-foreground leading-relaxed">
              The corpus analysis is restricted to open-source C# repositories. BCR patterns may manifest
              differently in dynamically typed languages, functional codebases, or systems with non-standard
              control flow idioms. The findings may not generalize to closed-source enterprise software, where
              codebase age, ownership diffusion, and testing culture differ substantially from well-maintained
              open-source projects. The BCRV workflow itself has not been evaluated in a controlled industrial
              study; its effectiveness under real team conditions, varying auditor expertise, and large-scale
              diff volumes remains to be demonstrated empirically.
            </p>

            <h3 className="text-xl font-semibold tracking-tight">Construct Validity</h3>
            <p className="text-muted-foreground leading-relaxed">
              Two construct validity threats warrant acknowledgment. First, behavioral change is operationalized
              as pattern matches against known BCR indicators, a structural proxy for the formal definition in
              §3. A pattern match is not a proof that{" "}
              <code className="text-xs bg-secondary px-1 py-0.5 rounded font-mono">ΔB ∉ V(T, C + ΔC)</code>;
              it is a heuristic signal that the diff contains a change class historically associated with
              validation gaps. Second, test coverage of behavioral changes is approximated by the presence of
              modified test files (<code className="text-xs bg-secondary px-1 rounded">has_tests_changed</code>),
              not by assertion-level analysis of whether the specific behavioral delta is newly covered. A pull
              request may include test file changes entirely unrelated to the flagged behavioral pattern,
              overstating coverage.
            </p>
          </section>

          {/* Section 11 */}
          <section className="space-y-5">
            <h2 className="text-2xl font-bold tracking-tight">11. Conclusion</h2>
            <p className="text-muted-foreground leading-relaxed">
              The green checkmark of a passing CI build has become the primary expression of the Green Build
              Validity Assumption in practice, a symbol of software quality that obscures a structural blind
              spot. Tests validate what was written; they cannot validate what was removed. They assert expected
              outcomes; they are silent on the consequences of altered behavior.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              <strong>Behavioral Change Risk (BCR)</strong> is the formal name for this gap. It is the risk that
              a code change introduces new behavior that no test is positioned to detect. Empirical evidence from
              large-scale CI systems and automated program repair research confirms that this risk is both real
              and underappreciated.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              <strong>Behavioral Change Risk Validation (BCRV)</strong> offers a methodology for addressing BCR.
              By shifting attention from test results to change semantics, BCRV provides a framework for
              identifying and mitigating the behavioral risks that slip through conventional validation pipelines.
            </p>
            <p className="text-lg font-medium text-foreground leading-relaxed">
              The software industry has spent decades learning to test what code does. It is time to develop the
              discipline to audit what code changes.
            </p>
          </section>

          {/* References */}
          <section className="space-y-5 border-t border-border pt-12">
            <h2 className="text-2xl font-bold tracking-tight">References</h2>
            <ol className="space-y-4">
              {[
                { id: 1, authors: "Haben, G., Habchi, S., Papadakis, M., Cordy, M., & Le Traon, Y.", year: "2023", title: "The Importance of Discerning Flaky from Fault-triggering Test Failures: A Case Study on the Chromium CI.", venue: "arXiv:2302.10594.", url: "https://arxiv.org/abs/2302.10594" },
                { id: 2, authors: "Zemín, L., Godio, A., Cornejo, C., Degiovanni, R., Gutiérrez Brida, S., Regis, G., Aguirre, N., & Frias, M.F.", year: "2025", title: "An Empirical Study on the Suitability of Test-based Patch Acceptance Criteria.", venue: "ACM Transactions on Software Engineering and Methodology, 34(3), 57:1-57:20. DOI: 10.1145/3702971.", url: null },
                { id: 3, authors: "Schwartz, A., Puckett, D., Meng, Y., & Gay, G.", year: "2018", title: "Investigating Faults Missed by Test Suites Achieving High Code Coverage.", venue: "Journal of Systems and Software, 144, 106-120. DOI: 10.1016/j.jss.2018.06.024.", url: null },
                { id: 4, authors: "Gay, G. & Salahirad, A.", year: "2023", title: "How Closely are Common Mutation Operators Coupled to Real Faults?", venue: "IEEE ICST, pp. 129-140. DOI: 10.1109/ICST57152.2023.00021.", url: null },
                { id: 5, authors: "Barr, E. T., et al.", year: "2015", title: "The Oracle Problem in Software Testing: A Survey.", venue: "IEEE Transactions on Software Engineering.", url: null },
                { id: 6, authors: "Inozemtseva, L., & Holmes, R.", year: "2014", title: "Coverage is Not Strongly Correlated with Test Suite Effectiveness.", venue: "ICSE.", url: "https://dl.acm.org/doi/10.1145/2568225.2568271" },
                { id: 7, authors: "Just, R., et al.", year: "2014", title: "Are Mutants a Valid Substitute for Real Faults in Software Testing?", venue: "FSE.", url: null },
                { id: 8, authors: "Luo, Q., et al.", year: "2014", title: "An Empirical Analysis of Flaky Tests.", venue: "FSE.", url: null },
                { id: 9, authors: "Fraser, G. & Arcuri, A.", year: "2013", title: "Whole Test Suite Generation.", venue: "IEEE Transactions on Software Engineering, 39(2), 276-291. DOI: 10.1109/TSE.2012.14.", url: null },
                { id: 10, authors: "Eder, S., Hauptmann, B., Junker, M., Jürgens, E., Vaas, R., & Prommer, J.", year: "2013", title: "Did we test our changes? Assessing alignment between tests and development in practice.", venue: "AST@ICSE 2013, pp. 107-110. DOI: 10.1109/IWAST.2013.6595800.", url: null },
                { id: 11, authors: "Haas, D., Sailer, L., Joblin, M., Juergens, E., & Apel, S.", year: "2025", title: "Prioritizing Test Gaps by Risk in Industrial Practice.", venue: "IEEE Transactions on Software Engineering. DOI: 10.1109/TSE.2025.3556248.", url: null },
                { id: 12, authors: "Wloka, J., Ryder, B. G., & Tip, F.", year: "2009", title: "JUnitMX: A change-aware unit testing tool.", venue: "ICSE 2009, pp. 567-570. DOI: 10.1109/ICSE.2009.5070557.", url: null },
                { id: 13, authors: "Jin, W., Orso, A., & Xie, T.", year: "2010", title: "Automated Behavioral Regression Testing.", venue: "ICST 2010, pp. 137-146. DOI: 10.1109/ICST.2010.64.", url: null },
                { id: 14, authors: "Su, Y., Pradel, M., & Chen, C.", year: "2026", title: "RippleGUItester: Change-Aware Exploratory Testing.", venue: "arXiv:2603.03121.", url: "https://arxiv.org/abs/2603.03121" },
              ].map((ref) => (
                <li key={ref.id} id={`cite-${ref.id}`} className="flex gap-3 text-sm text-muted-foreground leading-relaxed">
                  <span className="shrink-0 font-mono text-muted-foreground/50 w-5">[{ref.id}]</span>
                  <span>
                    {ref.authors} ({ref.year}). <em>{ref.title}</em> {ref.venue}
                    {ref.url && (
                      <>{" "}<a href={ref.url} target="_blank" rel="noopener noreferrer" className="text-cyan-400 hover:text-cyan-300 break-all">{ref.url}</a></>
                    )}
                  </span>
                </li>
              ))}
            </ol>
          </section>

          {/* Real-world examples */}
          <section className="space-y-4 border-t border-border pt-12">
            <h2 className="text-xl font-bold tracking-tight">Real-world examples from .NET OSS</h2>
            <p className="text-sm text-muted-foreground leading-relaxed">
              The BCR categories above are not theoretical. These case studies show GauntletCI
              detecting the exact patterns in real pull requests to widely-used .NET libraries.
            </p>
            <div className="grid gap-3 sm:grid-cols-2">
              <Link
                href="/case-studies/stackexchange-redis-swallowed-exception"
                className="block rounded-xl border border-border bg-card p-4 hover:border-cyan-500/40 hover:bg-card/80 transition-colors"
              >
                <div className="flex items-center gap-2 mb-2">
                  <span className="font-mono text-xs text-muted-foreground/60">StackExchange/StackExchange.Redis</span>
                  <span className="font-mono text-xs text-muted-foreground/40">PR#2995</span>
                </div>
                <h3 className="text-sm font-semibold text-foreground mb-1">
                  Swallowed Exception in StackExchange.Redis
                </h3>
                <p className="text-xs text-muted-foreground leading-relaxed">
                  GCI0007 catches a bare catch {} block that silently drops all exceptions in the message dispatch loop.
                </p>
              </Link>
              <Link
                href="/case-studies/nunit-thread-sleep-async"
                className="block rounded-xl border border-border bg-card p-4 hover:border-cyan-500/40 hover:bg-card/80 transition-colors"
              >
                <div className="flex items-center gap-2 mb-2">
                  <span className="font-mono text-xs text-muted-foreground/60">nunit/nunit</span>
                  <span className="font-mono text-xs text-muted-foreground/40">PR#5192</span>
                </div>
                <h3 className="text-sm font-semibold text-foreground mb-1">
                  Thread.Sleep in Async Context - NUnit
                </h3>
                <p className="text-xs text-muted-foreground leading-relaxed">
                  GCI0016 catches Thread.Sleep blocking the thread pool in an async context inside the NUnit test framework itself.
                </p>
              </Link>
            </div>
          </section>

          {/* CTAs */}
          <div className="border-t border-border pt-10 flex flex-col sm:flex-row gap-4">
            <Link
              href="/docs"
              className="inline-flex items-center justify-center gap-2 rounded-lg bg-cyan-500 px-6 py-3 text-sm font-semibold text-background hover:bg-cyan-400 transition-colors"
            >
              Try GauntletCI free
            </Link>
            <Link
              href="/why-tests-miss-bugs"
              className="inline-flex items-center gap-2 rounded-lg border border-border bg-card px-6 py-3 text-sm font-semibold hover:bg-card/80 transition-colors"
            >
              Why tests miss bugs →
            </Link>
            <Link
              href="/what-is-diff-based-analysis"
              className="inline-flex items-center gap-2 rounded-lg border border-border bg-card px-6 py-3 text-sm font-semibold hover:bg-card/80 transition-colors"
            >
              What is diff-based analysis? →
            </Link>
          </div>

          <RulesApplied ids={["GCI0003", "GCI0036", "GCI0016", "GCI0007"]} />
          <AuthorBio variant="short" />
        </div>
      </main>
      <Footer />
    </>
  );
}

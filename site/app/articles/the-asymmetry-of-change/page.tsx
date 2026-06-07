import type { Metadata } from "next";
import { ArticleLayout } from "../_components/article-layout";

const slug = "the-asymmetry-of-change";
const title = "The Asymmetry of Change: Why Your Tests Are Looking the Wrong Way";
const description =
  "Why passing tests don't guarantee correct behavior. How diff-scanning can close the gap between code changes and test validation.";
const readingTime = "12 min read";
const ruleIds = ["GCI0003", "GCI0041", "GCI0044"];

export const metadata: Metadata = {
  title: `${title} | GauntletCI`,
  description,
  alternates: { canonical: `/articles/${slug}` },
  authors: [{ name: "Eric Cogen" }],
  keywords: ["testing", "ci", "code-review", "diff-scanning", "quality-gates", "gittest", "behavioral-testing"],
  openGraph: {
    title,
    description,
    type: "article",
    url: `https://gauntletci.com/articles/${slug}`,
    publishedTime: "2026-05-08T00:00:00Z",
    authors: ["Eric Cogen"],
  },
};

export default function AsymmetryOfChangePage() {
  return (
    <ArticleLayout
      category="Article"
      title={title}
      intro={description}
      dateTime="2026-05-08"
      dateLabel="May 8, 2026"
      readingTime={readingTime}
      ruleIds={ruleIds}
    >
      <div className="prose dark:prose-invert prose-lg max-w-none space-y-8">
          <section className="space-y-6">
            <h2 className="text-3xl font-bold tracking-tight text-foreground">1. The "Wrong Question" Problem</h2>
            <p className="text-muted-foreground leading-relaxed">
              A passing build is often treated as a certificate of correctness. In reality, it is a narrow contract. It doesn't prove your code is right; it proves that the assertions you wrote in the past, against behaviors you anticipated then, still hold true today.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              When you open a Pull Request, the unit tests ask: <em>"Does the system still behave the way it used to?"</em> The question you actually need to answer is: <strong>"Is the new behavior I just introduced safe?"</strong>
            </p>
            <p className="text-muted-foreground leading-relaxed">
              Tests are a snapshot of past understanding. The gap between "what we expected a year ago" and "what this diff actually does now" is exactly where production incidents live. And this is not a rare edge case; it's a pervasive, industry-wide pattern.
            </p>
          </section>

          <section className="space-y-6">
            <h2 className="text-3xl font-bold tracking-tight text-foreground">2. Evidence That the Gap Is Real and Widespread</h2>
            <p className="text-muted-foreground leading-relaxed">
              This isn't speculation. Multiple independent studies have documented that production code and its tests routinely drift apart, leaving behavioral changes unvalidated.
            </p>
            <ul className="space-y-4 text-muted-foreground">
              <li className="pl-6 border-l-2 border-blue-500">
                <strong className="text-foreground">Test Co-Evolution Studies:</strong> A 2025 study of 526 repositories across JavaScript, TypeScript, Java, Python, PHP, and C# found that asynchronous evolution of tests and code is pervasive, with five distinct patterns of divergence observed. High co-evolution correlated with smaller teams, suggesting larger organizations face a wider gap <sup><a href="#ref1">[1]</a></sup>. Earlier work on 975 Java projects reached similar conclusions: production code frequently changes without corresponding test updates <sup><a href="#ref2">[2]</a></sup>. This phenomenon has been recognized since at least 2010 <sup><a href="#ref3">[3]</a></sup>.
              </li>
              <li className="pl-6 border-l-2 border-blue-500">
                <strong className="text-foreground">CI Trust Issues:</strong> In the Chromium continuous integration system, researchers analyzed over 1.5 million test executions across 14,000 commits and found that even state-of-the-art flakiness detection, operating at 99.2% precision, could cause 76.2% of real regression faults to be missed <sup><a href="#ref4">[4]</a></sup>. This isn't about missing tests; it's about existing tests being silenced by tooling, masking behavioral regressions that are already theoretically covered.
              </li>
              <li className="pl-6 border-l-2 border-blue-500">
                <strong className="text-foreground">Real-World Example (Django 6.0):</strong> A refactor in the `querystring` template tag introduced a loop that mishandled `QueryDict` instances, keeping only the last value per key. Existing tests passed because they used standard dictionaries. The bug shipped and was later caught by a targeted rendered-output test <sup><a href="#ref5">[5]</a></sup>. The test suite didn't fail; it just never asked the right question.
              </li>
              <li className="pl-6 border-l-2 border-blue-500">
                <strong className="text-foreground">Residual Bugs in Python:</strong> A dataset of roughly 5,000 residual Python bugs from prominent open-source projects catalogs defects that went undetected during traditional testing and surfaced only in production <sup><a href="#ref6">[6]</a></sup>.
              </li>
              <li className="pl-6 border-l-2 border-blue-500">
                <strong className="text-foreground">Observations from the .NET Ecosystem:</strong> In an exploratory analysis of 610 pull requests across 61 open-source .NET repositories (including Polly, Dapper, Newtonsoft.Json, and dotnet/runtime), 133 of 178 PRs submitted without test file modifications (74.7%) contained at least one behavioral risk indicator <sup><a href="#ref7">[7]</a></sup>. This is product research, not a peer-reviewed study, but it is directionally consistent with the broader literature: when production code changes and tests don't, risk accumulates silently.
              </li>
            </ul>
            <p className="text-muted-foreground leading-relaxed pt-4">
              The problem is not limited to one language, one team size, or one maturity level. The evidence is clear: tests frequently fail to keep pace with code changes, and our CI systems often can't tell the difference between a safe change and a dangerous one.
            </p>
          </section>

          <section className="space-y-6">
            <h2 className="text-3xl font-bold tracking-tight text-foreground">3. The Time Machine and the Implicit Contract</h2>
            <p className="text-muted-foreground leading-relaxed">
              Think of every diff as a time machine moving in one direction. The assertions stay where they were written, while the code underneath them moves forward. This creates a dangerous blind spot: <strong>The Implicit Contract.</strong> Consider a guard clause that has existed for years. Because that guard was always there, no one ever felt the need to write an explicit test for the `null` case. The "contract" was implicit in the structure of the code. If a developer removes that guard, the test suite remains green. The suite isn't "broken"; it just never knew the guard was a requirement. It was a silent protector that the tests never bothered to verify.
            </p>
            <div className="bg-slate-100 dark:bg-slate-900 rounded-lg p-4 overflow-x-auto text-sm font-mono text-slate-900 dark:text-slate-100 space-y-2">
              <div className="text-gray-600 dark:text-gray-400">{"// Before diff: the implicit contract"}</div>
              <div>if (user == null) return;</div>
              <div>Process(user.Name);</div>
              <div className="pt-2"></div>
              <div className="text-gray-600 dark:text-gray-400">{"// After diff: guard removed, tests still pass."}</div>
              <div>Process(user.Name);</div>
            </div>
            <p className="text-muted-foreground leading-relaxed">
              No test explicitly covered the null path because the guard was the coverage. A diff scanner that sees a removed null-check can flag the behavioral delta even if the suite stays green. This is why coverage alone can be a mirage; it counts lines without checking whether the behavior behind those lines is actually validated.
            </p>
          </section>

          <section className="space-y-6">
            <h2 className="text-3xl font-bold tracking-tight text-foreground">4. The Human Context Window</h2>
            <p className="text-muted-foreground leading-relaxed">
              We rely on Code Review to catch these slips, but human reviewers have a "context window" just like an LLM. On a Tuesday afternoon, looking at a 400-line diff, a reviewer might see a refactor and miss that a crucial exception handler was swapped or a state transition was left unvalidated.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              We are asking humans to perform high-stakes pattern matching against a moving target. It is a process designed for fatigue.
            </p>
          </section>

          <section className="space-y-6">
            <h2 className="text-3xl font-bold tracking-tight text-foreground">5. A Layered Defense for the "Moment of Change"</h2>
            <p className="text-muted-foreground leading-relaxed">
              To close this gap, we need a defense-in-depth strategy that recognizes the strengths and limitations of our current tools:
            </p>
            <ul className="space-y-3 text-muted-foreground list-disc list-inside">
              <li><strong className="text-foreground">Unit Tests:</strong> Excellent for preventing regressions of <em>known</em> requirements.</li>
              <li><strong className="text-foreground">Mutation Testing:</strong> Great for finding holes in your safety net, but often too slow for the local "inner loop" of development.</li>
              <li><strong className="text-foreground">Property-Based Testing:</strong> Encodes invariants that hold across many inputs, catching unanticipated behavioral shifts (e.g., FsCheck, QuickCheck, Hypothesis).</li>
              <li><strong className="text-foreground">CI-Enforced Test Delta Policies:</strong> Require that production code changes are accompanied by test updates, preventing suites from silently falling behind.</li>
              <li><strong className="text-foreground">Code Review:</strong> Essential for intent and architecture, but prone to human exhaustion.</li>
              <li><strong className="text-foreground">Deterministic Diff-Scanning:</strong> The missing layer.</li>
            </ul>
            <p className="text-muted-foreground leading-relaxed pt-4">
              By using a deterministic, rules-based engine (like the Roslyn-powered core of GauntletCI), we can audit the diff at the moment of change. Before the code even reaches a reviewer, a machine can flag structural risks: the removed guard clause, the narrowed conditional, the unvalidated behavioral shift.
            </p>
          </section>

          <section className="space-y-6">
            <h2 className="text-3xl font-bold tracking-tight text-foreground">6. Determinism vs. Probability</h2>
            <p className="text-muted-foreground leading-relaxed">
              In building a solution for this, there is a temptation to reach for purely probabilistic AI. But for a security and quality gate, "maybe" isn't good enough.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              A "rules-first" approach ensures that the audit is consistent. Whether you run it at 9:00 AM or midnight, the same diff should produce the same findings. AI is not used to decide what is "risky," but to act as an optional narrator; translating deterministic structural failures into actionable engineering feedback.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              Of course, a rule engine has no understanding of intent. It will flag patterns that are entirely intentional; safe refactorings where the null guard became redundant, for example. The output is a focused checklist, not a verdict. Developers still decide what's a risk and what isn't.
            </p>
          </section>

          <section className="space-y-6">
            <h2 className="text-3xl font-bold tracking-tight text-foreground">7. What the Scanner Actually Flags</h2>
            <p className="text-muted-foreground leading-relaxed">
              So what does a diff scanner look for? The core rules are deliberately narrow and high-signal. Examples include:
            </p>
            <ul className="space-y-2 text-muted-foreground list-disc list-inside">
              <li>Removed null-guards or defensive conditions</li>
              <li>Narrowed catch blocks (e.g., <code className="bg-slate-200 dark:bg-slate-800 px-2 py-1 rounded">catch(Exception)</code> &rarr; <code className="bg-slate-200 dark:bg-slate-800 px-2 py-1 rounded">catch(ArgumentException)</code>)</li>
              <li>Removed validation steps in state transitions</li>
              <li>Swapped exception handlers that could change propagation</li>
              <li>Thread-blocking patterns introduced in async contexts (e.g., new <code className="bg-slate-200 dark:bg-slate-800 px-2 py-1 rounded">Thread.Sleep()</code>)</li>
              <li>Behavioral changes in a PR that touch no test files at all</li>
            </ul>
            <p className="text-muted-foreground leading-relaxed pt-4">
              Each of these is a pattern that has caused real production incidents, and each can slip past a green test suite.
            </p>
          </section>

          <section className="space-y-6">
            <h2 className="text-3xl font-bold tracking-tight text-foreground">8. Moving the "Uh-Oh" Moment</h2>
            <p className="text-muted-foreground leading-relaxed">
              The most expensive place to have an <strong>"uh-oh"</strong> moment is in a post-mortem. The second most expensive is in a failed staging build.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              The goal is to move that realization to the local terminal, the millisecond a developer <strong>hits save and before they even hit commit</strong>. By catching unvalidated behavioral changes while the logic is still fresh in the developer's mind, we don't just keep the build green; we ensure the build is actually correct. We stop the "Time Machine" before it ever leaves the station.
            </p>
          </section>
      </div>

      <section className="border-t border-border pt-12 space-y-6">
        <h2 className="text-2xl font-bold text-foreground">References</h2>
        <ol className="space-y-4 text-sm text-muted-foreground list-decimal list-inside">
          <li id="ref1" className="pl-2">
            Miranda, J. et al. (2025). Test Co-Evolution in Software Projects: A Large-Scale Empirical Study. <em>Journal of Software: Evolution and Process.</em> DOI: 10.1002/smr.70035
          </li>
          <li id="ref2" className="pl-2">
            Sun, W. et al. (2021). Understanding and Facilitating the Co-Evolution of Production and Test Code. <em>IEEE International Conference on Software Engineering (ICSE).</em>
          </li>
          <li id="ref3" className="pl-2">
            Gergely, T. et al. (2010). Studying the co-evolution of production and test code in open source and industrial developer test processes through repository mining. <em>Empirical Software Engineering.</em> DOI: 10.1007/s10664-010-9143-7
          </li>
          <li id="ref4" className="pl-2">
            Haben, G., Habchi, S., Papadakis, M., Cordy, M., & Le Traon, Y. (2023). The Importance of Discerning Flaky from Fault-triggering Test Failures: A Case Study on the Chromium CI. <em>arXiv:2302.10594.</em>
          </li>
          <li id="ref5" className="pl-2">
            Moreau, M. (2026). How a Single Test Revealed a Bug in Django 6.0. <em>Lincoln Loop.</em>
          </li>
          <li id="ref6" className="pl-2">
            Cotroneo, D., De Rosa, G., & Liguori, P. (2025). PyResBugs: A Dataset of Residual Python Bugs for Natural Language-Driven Fault Injection. <em>IEEE/ACM Forge 2025.</em> DOI: 10.1109/Forge66646.2025.00024
          </li>
          <li id="ref7" className="pl-2">
            Cogen, E. (2025). GauntletCI Corpus Analysis. 610 pull requests across 61 open-source .NET repositories. Data published at: <a href="https://github.com/EricCogen/GauntletCI/blob/main/data/corpus-fixtures.csv" className="text-blue-600 dark:text-blue-400 hover:underline">corpus-fixtures.csv</a>
          </li>
        </ol>
      </section>
    </ArticleLayout>
  );
}

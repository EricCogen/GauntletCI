import type { Metadata } from "next";
import Link from "next/link";
import { Header } from "@/components/header";
import { Footer } from "@/components/footer";

export const metadata: Metadata = {
  title: "What Is Diff-Based Analysis? | GauntletCI",
  description:
    "Diff-based analysis examines only the lines you changed, not the entire codebase. Learn why this approach is faster, more precise, and more actionable than full-codebase scanning.",
  alternates: { canonical: "/what-is-diff-based-analysis" },
  openGraph: { images: [{ url: '/og/what-is-diff-based-analysis.png', width: 1200, height: 630 }] },
};

const jsonLd = {
  "@context": "https://schema.org",
  "@type": "Article",
  "headline": "What Is Diff-Based Analysis?",
  "description": "Diff-based analysis examines only the lines you changed, not the entire codebase. Learn why this approach is faster, more precise, and more actionable than full-codebase scanning.",
  "url": "https://gauntletci.com/what-is-diff-based-analysis",
  "publisher": { "@type": "Organization", "name": "GauntletCI", "url": "https://gauntletci.com" },
};

export default function WhatIsDiffBasedAnalysisPage() {
  return (
    <>
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLd) }} />
      <Header />
      <main className="min-h-screen bg-background pt-24">

        <div className="mx-auto max-w-4xl px-4 sm:px-6 lg:px-8 py-16 sm:py-20 space-y-16">

          {/* Hero */}
          <div className="space-y-5 border-b border-border pb-12">
            <p className="text-sm font-semibold uppercase tracking-widest text-cyan-400">Concept</p>
            <h1 className="text-4xl sm:text-5xl font-bold tracking-tight text-balance">
              What is diff-based analysis?
            </h1>
            <p className="text-xl text-muted-foreground leading-relaxed text-pretty">
              Diff-based analysis examines only the lines you changed, not the entire codebase.
              It answers the question: &quot;What risk did this change introduce?&quot; rather than
              &quot;What risk exists in the whole project?&quot;
            </p>
            <p className="text-muted-foreground leading-relaxed">
              This distinction is not cosmetic. The scope of what a tool analyzes determines who
              uses it, how often, and whether its findings get acted on. Decades of research on
              static analysis adoption tell a consistent story: developers abandon tools that cry
              wolf, and they keep using tools that surface only what matters right now. Diff-based
              analysis is the structural answer to that adoption problem.
            </p>
            <div className="flex items-center gap-2 pt-1">
              <span className="text-sm font-medium text-foreground">Eric Cogen</span>
              <span className="text-muted-foreground/40 text-sm">·</span>
              <span className="text-sm text-muted-foreground">Founder, GauntletCI</span>
              <span className="text-muted-foreground/40 text-sm">·</span>
              <time className="text-sm text-muted-foreground" dateTime="2026-04-20">April 20, 2026</time>
            </div>
          </div>

          {/* How it works */}
          <section className="space-y-5">
            <h2 className="text-2xl font-bold tracking-tight">How diff-based analysis works</h2>
            <figure className="float-left mr-6 mb-4 w-[28%] rounded-lg overflow-hidden border border-border">
              <img
                src="/articles/what-is-diff-based-analysis-hero.png"
                alt="Two-panel diagram: full file scan shows 498 lines and 47 findings; diff-only view shows 6 lines and 1 finding, illustrating how scope narrows signal"
                width={480}
                height={280}
                className="w-full h-auto"
              />
            </figure>
            <p className="text-muted-foreground leading-relaxed">
              When you stage changes with{" "}
              <code className="text-xs font-mono bg-card border border-border rounded px-1.5 py-0.5">git add</code>,
              Git records a diff: the exact lines added, modified, and removed. A diff-based
              analysis engine operates on this diff as its sole input. It does
              not load, parse, or scan any file that was not touched by the current changeset.
              Every finding the tool produces is directly traceable to a line in the current diff.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              The engine reads each changed hunk, identifies the structural role of the modified
              lines (is this a guard clause? a public method signature? a serialization attribute?
              a dependency injection registration?), and evaluates a set of targeted rules against
              those structural properties. Critically, each rule fires on the delta (the change
              itself), not on the surrounding stable code that has been in production for months.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              The example below shows a single-line deletion. A guard clause that validated the
              method input has been removed. A full-codebase scanner may or may not surface this
              depending on whether it has a rule for missing input validation. A diff-based scanner
              surfaces it immediately because it can see the deletion (the removed line) and
              knows what was there before.
            </p>
            <div className="rounded-xl border border-border bg-card overflow-hidden">
              <div className="border-b border-border px-5 py-3 bg-card/80">
                <p className="text-xs font-mono text-muted-foreground/60">staged diff (simplified)</p>
              </div>
              <div className="p-5 font-mono text-xs leading-relaxed space-y-1">
                <p className="text-muted-foreground/50">@@ -42,7 +42,6 @@</p>
                <p className="text-muted-foreground/60">{"  "}public async Task&lt;User&gt; GetUserAsync(int id)</p>
                <p className="text-muted-foreground/60">{"  "}{"{"}</p>
                <p className="text-red-400">-{"     "}if (id &lt;= 0) throw new ArgumentException(nameof(id));</p>
                <p className="text-muted-foreground/60">{"      "}return await _repo.FindAsync(id);</p>
                <p className="text-muted-foreground/60">{"  "}{"}"}</p>
              </div>
              <div className="border-t border-border px-5 py-3 bg-red-500/5">
                <p className="text-xs text-red-400">
                  GCI0001: Removed guard clause at line 44 -- ArgumentException on invalid input is no longer thrown.
                </p>
              </div>
            </div>
          </section>

          {/* False positive problem */}
          <section className="space-y-5 border-t border-border pt-12">
            <h2 className="text-2xl font-bold tracking-tight">
              The false positive problem: why developers abandon full-codebase scanners
            </h2>
            <p className="text-muted-foreground leading-relaxed">
              A 2013 study published at the International Conference on Software Engineering
              examined why developers do not use static analysis tools even when those tools are
              available <a href="#cite-1" className="text-cyan-400 hover:text-cyan-300 text-xs align-super font-mono">[1]</a>. The top-ranked reason was false positives, ahead of
              performance, installation friction, and IDE integration gaps. Developers who encounter
              a tool that fires on pre-existing, known-benign patterns quickly learn to ignore it.
              Once a tool trains developers to ignore it, it has negative utility: it adds
              cognitive load with no corresponding benefit. Studies examining different tool types
              and organizational contexts identify performance overhead and integration friction as
              additional significant barriers, though false positives rank consistently near the top.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              Google observed the same dynamic at scale. In their 2018 Communications of the ACM
              paper on lessons from building static analysis tools across the full Google codebase,
              the authors describe how they deliberately ship only checks where confidence is very
              high <a href="#cite-2" className="text-cyan-400 hover:text-cyan-300 text-xs align-super font-mono">[2]</a>. Their internal tools were not rejected because they were slow or hard to
              install. They were rejected when the signal-to-noise ratio dropped below the
              threshold where a developer found it worth reading findings at all. The team
              eventually required that any new check demonstrate a low false positive rate on
              existing code before it could be turned on.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              Full-codebase scanning amplifies the false positive problem structurally. Every scan
              re-reports the same issues that were in the codebase before the developer touched
              anything. Technical debt accumulated years ago floods the results. A developer who
              changed two lines in one file sees hundreds of findings across the project, none of
              which are things they introduced and most of which their team has already decided to
              defer.The tool is technically correct, but it is not useful for the task the
              developer is actually doing.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              Beller et al. evaluated static analysis adoption in open source projects and found
              that even teams that had integrated static analysis into their CI pipelines frequently
              disabled or silenced large categories of warnings over time <a href="#cite-3" className="text-cyan-400 hover:text-cyan-300 text-xs align-super font-mono">[3]</a>. The trajectory is
              predictable: a team adopts a scanner, the finding count grows, developers start
              suppressing rules, and eventually the tool runs silently in CI producing output
              nobody reads. Teams that maintain sustained adoption typically roll out rule sets
              incrementally rather than enabling all checks at once; adoption patterns vary
              significantly by team size and codebase maturity.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              Diff-based analysis sidesteps this failure mode by construction. Because it only
              analyzes what changed, the maximum finding count for any given commit is bounded by
              the size of the diff. A two-line change produces at most a small number of findings,
              and every one of those findings is about the two lines the developer just wrote.
              There is no accumulated backlog of pre-existing issues competing for attention.
            </p>
          </section>

          {/* Signal-to-noise ratio */}
          <section className="space-y-5 border-t border-border pt-12">
            <h2 className="text-2xl font-bold tracking-tight">
              Signal-to-noise ratio: the metric that determines whether developers trust a tool
            </h2>
            <p className="text-muted-foreground leading-relaxed">
              The practical measure of a static analysis tool is not its recall rate on a test
              suite of known bugs. It is whether a developer, in the middle of shipping a feature,
              stops what they are doing and acts on a finding. That behavior requires trust, and
              trust requires a track record of findings that were worth acting on.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              Signal-to-noise ratio (SNR) is the fraction of tool findings that represent genuine,
              actionable risk versus total findings. A tool with high recall but low SNR produces
              many true positives alongside many false positives. Because developers cannot cheaply
              distinguish which is which (doing so would require the analysis they were hoping
              the tool would do for them), they treat all findings as suspect and eventually treat
              all findings as ignorable.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              Ayewah et al. studied false positive rates in FindBugs (now SpotBugs) deployments
              and found that false positive rates varied enormously by rule category <a href="#cite-4" className="text-cyan-400 hover:text-cyan-300 text-xs align-super font-mono">[4]</a>. Some rule
              categories were nearly always correct. Others fired on benign patterns more often than
              not. The practical lesson was not to tune the rules in isolation but to restrict which
              rules run in which contexts: a finding that is 40% likely to be a false positive
              in a full-codebase scan may be 90% likely to be a true positive when scoped to lines
              that were just modified. More recent tooling has reduced false positive rates in some
              categories; the core finding that context governs precision remains consistent.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              Rules scoped to the diff context fire only when a problem is introduced by the
              current change. A rule about removing a guard clause fires when a guard clause is
              removed in the current diff; it does not fire when a guard clause is absent from a
              stable file that has been in production for two years and whose absence was a
              deliberate design decision. The same rule logic, applied at different scope,
              produces radically different SNR. Scoping rules to the delta rather than the full
              file is the primary mechanism by which diff-based analysis achieves a higher SNR
              than equivalent rules running over a full codebase.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              High SNR has a compounding benefit. When a developer acts on every finding they
              receive, the tool becomes part of their normal workflow. They do not develop the
              habit of dismissing findings without reading them. This means that when a genuinely
              critical finding appears (a removed authentication check, a hardcoded secret, a
              broken serialization contract) it gets the same attention every other finding
              receives, rather than being lost in a backlog of noise.
            </p>
          </section>

          {/* How the diff parser works technically */}
          <section className="space-y-5 border-t border-border pt-12">
            <h2 className="text-2xl font-bold tracking-tight">
              How GauntletCI&apos;s diff parser works technically
            </h2>
            <p className="text-muted-foreground leading-relaxed">
              Understanding how a diff-based engine works requires understanding the structure of
              a unified diff, which is what{" "}
              <code className="text-xs font-mono bg-card border border-border rounded px-1.5 py-0.5">git diff</code>{" "}
              and{" "}
              <code className="text-xs font-mono bg-card border border-border rounded px-1.5 py-0.5">git diff --cached</code>{" "}
              produce. A unified diff is composed of file headers, hunk headers, and hunk bodies.
              The hunk header is the{" "}
              <code className="text-xs font-mono bg-card border border-border rounded px-1.5 py-0.5">@@</code>{" "}
              line. The hunk body contains context lines (prefixed with a space), added lines
              (prefixed with{" "}
              <code className="text-xs font-mono bg-card border border-border rounded px-1.5 py-0.5">+</code>),
              and removed lines (prefixed with{" "}
              <code className="text-xs font-mono bg-card border border-border rounded px-1.5 py-0.5">-</code>).
            </p>
            <p className="text-muted-foreground leading-relaxed">
              Here is a representative unified diff with multiple change types:
            </p>
            <div className="rounded-xl border border-border bg-card overflow-hidden">
              <div className="border-b border-border px-5 py-3 bg-card/80">
                <p className="text-xs font-mono text-muted-foreground/60">raw unified diff output -- git diff --cached</p>
              </div>
              <div className="p-5 font-mono text-xs leading-relaxed space-y-1">
                <p className="text-cyan-400/70">diff --git a/src/UserService.cs b/src/UserService.cs</p>
                <p className="text-muted-foreground/40">index 3a9f1d2..b7c840e 100644</p>
                <p className="text-muted-foreground/40">--- a/src/UserService.cs</p>
                <p className="text-muted-foreground/40">+++ b/src/UserService.cs</p>
                <p className="text-amber-400/80">@@ -38,12 +38,10 @@ public class UserService</p>
                <p className="text-muted-foreground/40">{"  "}/// &lt;summary&gt;Retrieves a user by identifier.&lt;/summary&gt;</p>
                <p className="text-muted-foreground/40">{"  "}public async Task&lt;User&gt; GetUserAsync(int id)</p>
                <p className="text-muted-foreground/40">{"  "}{"{"}</p>
                <p className="text-red-400">-{"     "}if (id &lt;= 0)</p>
                <p className="text-red-400">-{"         "}throw new ArgumentOutOfRangeException(nameof(id));</p>
                <p className="text-muted-foreground/40">{"      "}var cached = _cache.Get(id);</p>
                <p className="text-muted-foreground/40">{"      "}if (cached != null) return cached;</p>
                <p className="text-green-400">+{"      "}// TODO: restore validation</p>
                <p className="text-muted-foreground/40">{"      "}return await _repo.FindAsync(id);</p>
                <p className="text-muted-foreground/40">{"  "}{"}"}</p>
              </div>
            </div>
            <p className="text-muted-foreground leading-relaxed">
              GauntletCI processes this input in three passes. In the first pass it splits the raw
              text into file sections separated by the{" "}
              <code className="text-xs font-mono bg-card border border-border rounded px-1.5 py-0.5">diff --git</code>{" "}
              header, extracting the old path and new path for each changed file. In the second
              pass it splits each file section into hunks at each{" "}
              <code className="text-xs font-mono bg-card border border-border rounded px-1.5 py-0.5">@@</code>{" "}
              boundary. In the third pass it classifies each line within a hunk as a context line,
              an added line, or a removed line, and records the original and new line numbers using
              the hunk header coordinates.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              The{" "}
              <code className="text-xs font-mono bg-card border border-border rounded px-1.5 py-0.5">@@</code>{" "}
              header encodes four numbers: the starting line in the old file, the count of lines
              shown from the old file, the starting line in the new file, and the count of lines
              shown in the new file. The format is{" "}
              <code className="text-xs font-mono bg-card border border-border rounded px-1.5 py-0.5">@@ -old_start,old_count +new_start,new_count @@</code>.
              In the example above,{" "}
              <code className="text-xs font-mono bg-card border border-border rounded px-1.5 py-0.5">-38,12 +38,10</code>{" "}
              means the hunk shows 12 lines from the old file starting at line 38, and 10 lines
              from the new file starting at line 38. The two-line reduction is the deleted guard
              clause: two removed lines, no replaced lines.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              After parsing, each rule receives a structured representation: a list of changed
              hunks, each containing typed line objects with their content, their old and new line
              numbers, and their change type (added, removed, or context). Rules do not do any text
              parsing of their own. They pattern-match against the structured representation. A rule
              that looks for removed guard clauses queries: in any hunk, is there a sequence of
              removed lines that matches the pattern of a guard clause (a conditional followed by
              a throw or early return) at the top of a method body? If yes, fire. If no, move on.
              The rule never reads the surrounding file.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              Context lines are included in the hunk representation but are never the subject of a
              finding. Their purpose is to give rules enough surrounding code to make a structural
              judgment. A removed line that looks like a throw statement is more clearly a guard
              clause if the context lines confirm it was inside an if block near the top of a
              method. Context lines provide that framing without expanding the scope of what the
              engine analyzes to the full file. Typically three lines of context are provided on
              each side of a changed block, which is the default for unified diffs and is
              sufficient for most structural pattern matching.
            </p>
          </section>

          {/* Integration points */}
          <section className="space-y-5 border-t border-border pt-12">
            <h2 className="text-2xl font-bold tracking-tight">
              Integration points: where diff-based analysis fits in the development workflow
            </h2>
            <p className="text-muted-foreground leading-relaxed">
              Analysis tools can run at several points in the development lifecycle. Each
              integration point has different characteristics for feedback latency, developer
              context, and cost of remediation. Diff-based analysis is well-suited to the earliest
              integration points precisely because it operates on diffs; a diff is always
              available as long as there are staged or unstaged changes.
            </p>

            <h3 className="text-lg font-semibold mt-4">Pre-commit</h3>
            <p className="text-muted-foreground leading-relaxed">
              Pre-commit is the earliest integration point. The developer has just finished writing
              code and is about to record a snapshot. The diff is the exact set of changes they
              intend to commit. Running analysis here means the developer receives findings before
              the change becomes part of repository history. Fixing a finding is a file edit: no
              branch, no PR, no review cycle required. The cost is measured in seconds.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              Pre-commit hooks also run locally, which means no network round trip and no CI
              queue wait. GauntletCI is designed to complete analysis in under one second on a
              typical diff, making it fast enough to run on every commit without disrupting
              developer flow. The hook receives the staged diff directly from Git and returns
              structured findings before the commit object is created.
            </p>

            <h3 className="text-lg font-semibold mt-4">CI pipeline (pre-merge)</h3>
            <p className="text-muted-foreground leading-relaxed">
              Running diff-based analysis in CI, against the PR diff, is the second natural
              integration point. At this stage the developer has already committed and pushed.
              The diff is still well-defined (the branch diff against main), but the cost of
              fixing a finding is higher: a new commit, a push, and a re-run of CI. The developer
              may have context-switched to other work. Feedback latency is minutes to tens of
              minutes depending on queue depth.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              CI is a good fallback for teams that cannot or do not enforce pre-commit hooks
              uniformly. It also serves as a hard gate when team policy requires all findings to
              be clear before merge, regardless of whether the developer ran the hook locally.
              Running the same GauntletCI binary in CI that runs locally ensures consistent
              findings across both environments.
            </p>

            <h3 className="text-lg font-semibold mt-4">IDE and editor integration</h3>
            <p className="text-muted-foreground leading-relaxed">
              Some teams run analysis on save or on file change within the editor. This is the
              fastest feedback loop possible: sub-second latency, with findings surfaced inline
              while the code is still on screen. GauntletCI can be invoked with a diff piped from
              the editor change buffer, making real-time IDE integration achievable. The tradeoff
              is that partial changes (code that is syntactically incomplete or not yet compiling)
              can produce noisy results. Many teams use IDE integration for advisory findings
              and pre-commit integration for hard gates that block the commit.
            </p>

            <div className="flex items-stretch gap-2 mt-4">
              {[
                { label: "Pre-commit", sub: "Seconds to fix", color: "border-green-500/30 bg-green-500/5", text: "text-green-400" },
                { label: "CI (pre-merge)", sub: "Minutes to fix", color: "border-amber-500/30 bg-amber-500/5", text: "text-amber-400" },
                { label: "Post-deploy", sub: "Hours to days", color: "border-red-500/30 bg-red-500/5", text: "text-red-400" },
              ].map((stage) => (
                <div key={stage.label} className={`flex-1 rounded-lg border ${stage.color} p-4 text-center`}>
                  <p className={`text-sm font-semibold ${stage.text}`}>{stage.label}</p>
                  <p className="text-xs text-muted-foreground mt-1">{stage.sub}</p>
                </div>
              ))}
            </div>
            <p className="text-xs text-muted-foreground">
              Cost of fixing the same defect at different stages of the development lifecycle.
            </p>
          </section>

          {/* Full scan vs diff -- expanded table */}
          <section className="space-y-5 border-t border-border pt-12">
            <h2 className="text-2xl font-bold tracking-tight">How it compares to full-codebase scanning</h2>
            <p className="text-muted-foreground leading-relaxed">
              The table below captures the practical differences between a full-codebase scanner
              running on a schedule or in CI and a diff-based engine running at pre-commit. These
              are complementary approaches, not competing ones; understanding the tradeoffs
              helps teams decide when to rely on each and where to invest in tuning.
            </p>
            <div className="overflow-x-auto">
              <table className="w-full text-sm border-collapse">
                <thead>
                  <tr className="border-b border-border">
                    <th className="text-left py-3 pr-6 text-muted-foreground/60 font-medium text-xs uppercase tracking-wide w-1/3">Dimension</th>
                    <th className="text-left py-3 pr-6 text-muted-foreground/60 font-medium text-xs uppercase tracking-wide">Full-codebase scan</th>
                    <th className="text-left py-3 text-cyan-400 font-medium text-xs uppercase tracking-wide">Diff-based (GauntletCI)</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-border">
                  {[
                    ["Scope", "Every file in the project", "Only changed lines"],
                    ["Run time", "Minutes to hours on large codebases", "Under one second"],
                    ["When it runs", "Scheduled or CI pipeline", "Pre-commit, on every save"],
                    ["Signal type", "Existing issues in the full codebase", "Risk introduced by this change"],
                    ["Noise", "High: existing issues reappear every run", "Low: only new delta is analyzed"],
                    ["Actionability", "Requires triage across the full backlog", "Directly actionable: one change, one finding"],
                    ["False positive rate", "Higher: rules fire on any matching pattern", "Lower: rules scoped to changed lines only"],
                    ["Developer interrupt cost", "Findings arrive minutes later in CI or on a schedule", "Findings arrive before the commit is recorded"],
                    ["Trust trajectory", "Declines as finding count grows and backlog accumulates", "Stable: findings are always about current work"],
                    ["CI feedback latency", "Full scan blocks CI for minutes to hours", "Pre-commit prevents the issue from reaching CI at all"],
                  ].map(([dim, full, diff]) => (
                    <tr key={dim as string}>
                      <td className="py-3 pr-6 text-xs font-medium text-muted-foreground/70 align-top">{dim}</td>
                      <td className="py-3 pr-6 text-xs text-muted-foreground align-top">{full}</td>
                      <td className="py-3 text-xs text-foreground align-top">{diff}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </section>

          {/* The economics of early detection */}
          <section className="space-y-5 border-t border-border pt-12">
            <h2 className="text-2xl font-bold tracking-tight">The economics of early detection</h2>
            <p className="text-muted-foreground leading-relaxed">
              The cost of a defect is not fixed. It scales with the distance between when the
              defect was introduced and when it is detected. A guard clause removed in a
              pre-commit change is a two-second fix: restore the line, stage it again, re-run the
              hook. The same guard clause removal found in code review requires a PR comment
              thread, a context switch back to the code, a new commit, a push, and a re-review
              pass. Found after deploy, it is a production incident with a rollback, an on-call
              page, and a postmortem.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              This is not a theoretical claim. Research on defect cost amplification going back to
              the 1970s consistently finds that the cost ratio between detecting a defect early in
              development versus in production can be 10:1 to 100:1 or higher depending on the
              severity and the system involved. Pre-commit detection is not quite design time, but
              it is the closest practical analog in a modern commit-based workflow. The developer
              still has the change in working memory, the editor is open, and no other person time
              has been spent yet.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              Diff-based analysis is uniquely positioned to deliver pre-commit feedback because
              its input is the diff: exactly what the pre-commit hook has available. There
              is no need to check out the full repository, run a build, or wait for a CI
              environment. The staged diff is available immediately, and the analysis completes
              before the commit is finalized.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              Code review, even when thorough, is a probabilistic gate. Reviewers miss things,
              especially behavioral regressions introduced by deletions rather than additions.
              See{" "}
              <Link href="/why-code-review-misses-bugs" className="text-cyan-400 underline underline-offset-2 hover:text-cyan-300 transition-colors">
                why code review misses bugs
              </Link>{" "}
              for a detailed treatment of the systematic blind spots in human review. Diff-based
              analysis fills those gaps deterministically: it will always apply the same rules
              to the same diff and produce the same findings, regardless of reviewer fatigue,
              diff size, or social pressure to approve quickly.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              For teams concerned specifically about API contract breaks, serialization regressions,
              and removed overloads, see{" "}
              <Link href="/detect-breaking-changes-before-merge" className="text-cyan-400 underline underline-offset-2 hover:text-cyan-300 transition-colors">
                detecting breaking changes before merge
              </Link>.
              That article covers how diff-based rules identify breaking changes in public
              interfaces, REST contracts, and binary-compatibility-sensitive types, categories
              of change that are almost invisible in code review but trivially detectable in a
              structured diff.
            </p>
          </section>

          {/* Complementary */}
          <section className="space-y-4 rounded-xl border border-cyan-500/20 bg-cyan-500/5 p-6">
            <h3 className="font-semibold text-cyan-300">Diff-based analysis is complementary, not competitive</h3>
            <p className="text-sm text-muted-foreground leading-relaxed">
              Full-codebase scanning tools like SonarQube, Semgrep, and CodeQL serve a different
              purpose: finding existing issues across the full codebase on a schedule. A diff-based
              pre-commit tool does not replace them; it adds a pre-commit gate that flags the risk
              introduced by the current change before that risk becomes part of the baseline the
              scanner has to manage. Running both approaches provides defense in depth: new risk is
              caught at the earliest possible moment, and existing risk is tracked over time with a
              dedicated tool suited to that task.
            </p>
            <p className="text-sm text-muted-foreground leading-relaxed">
              For teams migrating to a scanner from a codebase with no prior analysis, diff-based
              tooling also offers a practical migration path. Rather than being confronted with
              thousands of pre-existing findings on day one, the team can freeze the existing
              baseline and use diff-based analysis to ensure no new issues are introduced going
              forward. Technical debt is addressed incrementally without blocking current work.
            </p>
          </section>

          {/* Related topics */}
          <section className="space-y-4 border-t border-border pt-12">
            <h2 className="text-2xl font-bold tracking-tight">Related topics</h2>
            <div className="grid sm:grid-cols-2 gap-4">
              <Link
                href="/why-code-review-misses-bugs"
                className="group rounded-xl border border-border bg-card p-5 hover:border-cyan-500/40 transition-colors"
              >
                <p className="text-sm font-semibold group-hover:text-cyan-400 transition-colors">
                  Why code review misses bugs
                </p>
                <p className="text-xs text-muted-foreground mt-1 leading-relaxed">
                  The systematic blind spots in human review that diff-based analysis fills
                  deterministically, including deleted validations, implicit contracts, and
                  concurrency anti-patterns.
                </p>
              </Link>
              <Link
                href="/detect-breaking-changes-before-merge"
                className="group rounded-xl border border-border bg-card p-5 hover:border-cyan-500/40 transition-colors"
              >
                <p className="text-sm font-semibold group-hover:text-cyan-400 transition-colors">
                  Detect breaking changes before merge
                </p>
                <p className="text-xs text-muted-foreground mt-1 leading-relaxed">
                  How GauntletCI uses diff rules to surface API contract violations, removed
                  overloads, and serialization breaks before they reach main.
                </p>
              </Link>
            </div>
          </section>

          {/* References */}
          <section className="space-y-4 border-t border-border pt-12">
            <h2 className="text-2xl font-bold tracking-tight">References</h2>
            <ol className="space-y-4 list-none">
              <li id="cite-1" className="flex gap-3">
                <span className="text-muted-foreground/50 font-mono text-xs mt-0.5 shrink-0">[1]</span>
                <span className="text-sm text-muted-foreground leading-relaxed">
                  B. Johnson, Y. Song, E. Murphy-Hill, and R. Bowdidge, &quot;Why Don&apos;t Software
                  Developers Use Static Analysis Tools to Find Bugs?&quot; in{" "}
                  <em>Proc. ICSE 2013</em>, pp. 672-681.{" "}
                  <a
                    href="https://dl.acm.org/doi/10.1109/ICSE.2013.6606613"
                    className="text-cyan-400 underline underline-offset-2 hover:text-cyan-300 transition-colors"
                    target="_blank"
                    rel="noopener noreferrer"
                  >
                    https://dl.acm.org/doi/10.1109/ICSE.2013.6606613
                  </a>
                </span>
              </li>
              <li id="cite-2" className="flex gap-3">
                <span className="text-muted-foreground/50 font-mono text-xs mt-0.5 shrink-0">[2]</span>
                <span className="text-sm text-muted-foreground leading-relaxed">
                  C. Sadowski, J. van Gogh, C. Jaspan, E. Soderberg, and C. Winter, &quot;Lessons from
                  Building Static Analysis Tools at Google,&quot;{" "}
                  <em>Commun. ACM</em>, vol. 61, no. 4, pp. 58-66, 2018.{" "}
                  <a
                    href="https://dl.acm.org/doi/10.1145/3188720"
                    className="text-cyan-400 underline underline-offset-2 hover:text-cyan-300 transition-colors"
                    target="_blank"
                    rel="noopener noreferrer"
                  >
                    https://dl.acm.org/doi/10.1145/3188720
                  </a>
                </span>
              </li>
              <li id="cite-3" className="flex gap-3">
                <span className="text-muted-foreground/50 font-mono text-xs mt-0.5 shrink-0">[3]</span>
                <span className="text-sm text-muted-foreground leading-relaxed">
                  M. Beller, R. Bholanath, S. McIntosh, and A. Zaidman, &quot;Analyzing the State of
                  Static Analysis: A Large-Scale Evaluation in Open Source Software,&quot; in{" "}
                  <em>Proc. SANER 2016</em>.{" "}
                  <a
                    href="https://ieeexplore.ieee.org/document/7476770"
                    className="text-cyan-400 underline underline-offset-2 hover:text-cyan-300 transition-colors"
                    target="_blank"
                    rel="noopener noreferrer"
                  >
                    https://ieeexplore.ieee.org/document/7476770
                  </a>
                </span>
              </li>
              <li id="cite-4" className="flex gap-3">
                <span className="text-muted-foreground/50 font-mono text-xs mt-0.5 shrink-0">[4]</span>
                <span className="text-sm text-muted-foreground leading-relaxed">
                  N. Ayewah, W. Pugh, J. D. Morgenthaler, J. Penix, and Y. Zhou, &quot;Using Static
                  Analysis to Find Bugs,&quot;{" "}
                  <em>IEEE Software</em>, vol. 25, no. 5, pp. 22-29, 2008.
                </span>
              </li>
            </ol>
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
              href="/detect-breaking-changes-before-merge"
              className="inline-flex items-center gap-2 rounded-lg border border-border bg-card px-6 py-3 text-sm font-semibold hover:bg-card/80 transition-colors"
            >
              Detect breaking changes
            </Link>
            <Link
              href="/why-code-review-misses-bugs"
              className="inline-flex items-center gap-2 rounded-lg border border-border bg-card px-6 py-3 text-sm font-semibold hover:bg-card/80 transition-colors"
            >
              Why code review misses bugs
            </Link>
          </div>

        </div>
      </main>
      <Footer />
    </>
  );
}

import type { Metadata } from "next";
import Link from "next/link";
import { Header } from "@/components/header";
import { Footer } from "@/components/footer";

export const metadata: Metadata = {
  title: "What Is Diff-Based Analysis? | GauntletCI",
  description:
    "Diff-based analysis examines only the lines you changed, not the entire codebase. Learn why this approach is faster, more precise, and more actionable than full-codebase scanning.",
  alternates: { canonical: "/what-is-diff-based-analysis" },
};

export default function WhatIsDiffBasedAnalysisPage() {
  return (
    <>
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
              It answers the question: "What risk did this change introduce?" rather than
              "What risk exists in the whole project?"
            </p>
          </div>

          {/* How it works */}
          <section className="space-y-5">
            <h2 className="text-2xl font-bold tracking-tight">How it works</h2>
            <p className="text-muted-foreground leading-relaxed">
              When you stage changes with <code className="text-xs font-mono bg-card border border-border rounded px-1.5 py-0.5">git add</code>,
              Git records a diff: the exact lines added, modified, and removed. Diff-based analysis
              operates on this diff as the primary input. It does not load, parse, or scan files
              that were not touched.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              The analysis reads each changed hunk, identifies the structural role of the modified
              lines (is this a guard clause? a public method signature? a serialization attribute?),
              and evaluates a set of rules against those structural properties. Each rule is
              evaluated against the delta, not the full file.
            </p>
            <div className="rounded-xl border border-border bg-card overflow-hidden">
              <div className="border-b border-border px-5 py-3 bg-card/80">
                <p className="text-xs font-mono text-muted-foreground/60">staged diff (simplified)</p>
              </div>
              <div className="p-5 font-mono text-xs leading-relaxed space-y-1">
                <p className="text-muted-foreground/50">@@ -42,7 +42,6 @@</p>
                <p className="text-muted-foreground/60">  public async Task&lt;User&gt; GetUserAsync(int id)</p>
                <p className="text-muted-foreground/60">  {"{"}</p>
                <p className="text-red-400">-     if (id &lt;= 0) throw new ArgumentException(nameof(id));</p>
                <p className="text-muted-foreground/60">      return await _repo.FindAsync(id);</p>
                <p className="text-muted-foreground/60">  {"}"}</p>
              </div>
              <div className="border-t border-border px-5 py-3 bg-red-500/5">
                <p className="text-xs text-red-400">
                  GCI0001: Removed guard clause at line 44 -- ArgumentException on invalid input is no longer thrown.
                </p>
              </div>
            </div>
          </section>

          {/* Full scan vs diff */}
          <section className="space-y-5">
            <h2 className="text-2xl font-bold tracking-tight">How it compares to full-codebase scanning</h2>
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
                    ["Noise", "High -- existing issues reappear every run", "Low -- only new delta is analyzed"],
                    ["Actionability", "Requires triage across the full backlog", "Directly actionable -- one change, one finding"],
                    ["False positive rate", "Higher -- rules fire on any matching pattern", "Lower -- rules scoped to changed lines only"],
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

          {/* Why pre-commit matters */}
          <section className="space-y-5 border-t border-border pt-12">
            <h2 className="text-2xl font-bold tracking-tight">Why pre-commit is the right integration point</h2>
            <p className="text-muted-foreground leading-relaxed">
              The diff is freshest before the commit. The developer is still in context. Fixing
              a finding before creating the commit takes seconds. The same finding found in code
              review requires a PR comment, a context switch, a new commit, and a re-review cycle.
              Found in post-deploy monitoring, it requires a rollback and an incident response.
            </p>
            <div className="flex items-stretch gap-2">
              {[
                { label: "Pre-commit", sub: "Seconds to fix", color: "border-green-500/30 bg-green-500/5", text: "text-green-400" },
                { label: "Code review", sub: "Minutes to hours", color: "border-amber-500/30 bg-amber-500/5", text: "text-amber-400" },
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

          {/* Complementary */}
          <section className="space-y-4 rounded-xl border border-cyan-500/20 bg-cyan-500/5 p-6">
            <h3 className="font-semibold text-cyan-300">Diff-based analysis is complementary, not competitive</h3>
            <p className="text-sm text-muted-foreground leading-relaxed">
              Full-codebase scanning tools like SonarQube, Semgrep, and CodeQL serve a different
              purpose: finding existing issues across the full codebase on a schedule. GauntletCI
              does not replace them. It adds a pre-commit gate that flags the risk introduced by
              the current change - before that risk becomes part of the baseline the scanner has
              to manage.
            </p>
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
          </div>

        </div>
      </main>
      <Footer />
    </>
  );
}

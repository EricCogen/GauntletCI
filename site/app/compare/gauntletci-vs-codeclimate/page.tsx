import type { Metadata } from "next";
import Link from "next/link";
import { Header } from "@/components/header";
import { Footer } from "@/components/footer";

export const metadata: Metadata = {
  title: "GauntletCI vs Code Climate | Diff-Scoped Risk Detection vs Code Quality Metrics",
  description:
    "Code Climate measures maintainability and technical debt across your whole codebase. GauntletCI detects behavioral regressions in only the lines that changed -- pre-commit, 100% local.",
  alternates: { canonical: "/compare/gauntletci-vs-codeclimate" },
  openGraph: { images: [{ url: '/og/compare-codeclimate.png', width: 1200, height: 630 }] },
};

const table = [
  { feature: "Primary focus",              gauntlet: "Behavioral regressions in the diff",           cc: "Maintainability, duplication, and test coverage" },
  { feature: "Analysis scope",             gauntlet: "Changed diff lines only",                      cc: "Full repository scan" },
  { feature: "Data leaves the machine",    gauntlet: "Never -- 100% local execution",                cc: "Yes -- SaaS platform, repository connected to Code Climate" },
  { feature: "When it runs",               gauntlet: "Pre-commit, before the push",                  cc: "Post-push in CI pipeline" },
  { feature: "Pre-commit speed",           gauntlet: "Under 1 second",                               cc: "Not designed for pre-commit use" },
  { feature: "Account required",           gauntlet: "No",                                           cc: "Yes -- Code Climate account and repo authorization" },
  { feature: "Air-gap / data residency",   gauntlet: "Yes -- no network dependency",                 cc: "No -- SaaS-only" },
  { feature: "False positives on old code",gauntlet: "None -- diff-scoped by design",                cc: "Yes -- reports pre-existing issues on every run" },
  { feature: "Removed logic detection",    gauntlet: "Yes -- deleted guards, handlers flagged",      cc: "No -- tracks quality metrics, not behavioral changes" },
  { feature: "API contract change detection", gauntlet: "Yes",                                       cc: "No" },
  { feature: "Code duplication tracking",  gauntlet: "No",                                           cc: "Yes -- Code Climate core feature" },
  { feature: "Test coverage reporting",    gauntlet: "No",                                           cc: "Yes -- integrates with coverage reporters" },
  { feature: "Trend and velocity metrics", gauntlet: "No",                                           cc: "Yes -- GPA scoring over time" },
  { feature: "Local LLM enrichment",       gauntlet: "Built-in ONNX, fully offline",                 cc: "No" },
  { feature: "Baseline delta mode",        gauntlet: "Yes -- suppress pre-existing findings",        cc: "No" },
  { feature: "Free for open source",       gauntlet: "Yes, all rules",                               cc: "Yes (public repos only)" },
  { feature: "MCP server",                 gauntlet: "Yes -- AI assistants call GauntletCI directly",  cc: "No" },
  { feature: "Custom rules",               gauntlet: "Yes -- implement IRule in C#",                   cc: "Partial -- via engine plugins only" },
];

const jsonLd = {
  "@context": "https://schema.org",
  "@type": "Article",
  "headline": "GauntletCI vs Code Climate: Diff-Scoped Risk Detection vs Code Quality Metrics",
  "description": "Code Climate measures maintainability and technical debt across your whole codebase. GauntletCI detects behavioral regressions in only the lines that changed -- pre-commit, 100% local.",
  "url": "https://gauntletci.com/compare/gauntletci-vs-codeclimate",
  "publisher": { "@type": "Organization", "name": "GauntletCI", "url": "https://gauntletci.com" },
};

export default function CodeClimateComparePage() {
  return (
    <>
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLd) }} />
      <Header />
      <div className="min-h-screen bg-background">
      <div className="mx-auto max-w-5xl px-4 sm:px-6 lg:px-8 py-20 sm:py-28 space-y-20">

        {/* Hero */}
        <div className="text-center space-y-5">
          <p className="text-sm font-semibold uppercase tracking-widest text-cyan-400">Compare</p>
          <h1 className="text-4xl sm:text-5xl font-bold tracking-tight text-balance">
            GauntletCI vs Code Climate
          </h1>
          <p className="text-xl text-muted-foreground max-w-2xl mx-auto text-pretty">
            Code Climate tracks maintainability and technical debt across your whole
            codebase over time. GauntletCI catches behavioral regressions in the lines
            you changed right now. They operate at different points in the workflow.
          </p>
        </div>

        {/* Key difference */}
        <div className="grid sm:grid-cols-2 gap-6">
          <div className="rounded-xl border border-border bg-card p-7 space-y-3">
            <p className="text-xs font-semibold uppercase tracking-widest text-muted-foreground">Code Climate</p>
            <h2 className="text-lg font-bold">Code quality metrics over time</h2>
            <p className="text-sm text-muted-foreground leading-relaxed">
              Code Climate connects to your repository and tracks code quality signals across
              the entire codebase -- duplication, complexity, test coverage, and overall
              maintainability scores. It gives engineering leaders a dashboard of technical
              debt and quality trends over months and quarters.
            </p>
            <p className="text-sm text-muted-foreground leading-relaxed">
              It runs post-push in your CI pipeline. The findings it reports cover your whole
              codebase, including code that has not changed in years. It is a tool for tracking
              and managing the overall health of a repository at the macro level.
            </p>
          </div>
          <div className="rounded-xl border border-cyan-500/30 bg-cyan-500/5 p-7 space-y-3">
            <p className="text-xs font-semibold uppercase tracking-widest text-cyan-400">GauntletCI</p>
            <h2 className="text-lg font-bold">Behavioral risk in the diff right now</h2>
            <p className="text-sm text-muted-foreground leading-relaxed">
              GauntletCI runs on your machine, reads only the staged diff, and flags risk
              introduced by the current change -- before the commit is created. It never
              scans the full codebase. It never uploads code. Analysis completes in under
              one second with no account required.
            </p>
            <p className="text-sm text-muted-foreground leading-relaxed">
              It answers a more urgent question than code quality trends: "Does this specific
              change introduce a behavioral regression right now?" That question cannot be
              answered by a tool that runs after the push.
            </p>
          </div>
        </div>

        {/* Different questions */}
        <div className="space-y-6">
          <h2 className="text-2xl font-bold tracking-tight">They answer different questions</h2>
          <p className="text-muted-foreground">
            The distinction matters because the two tools have different latency profiles and
            different noise characteristics. Code Climate answers "how is our codebase doing
            overall?" GauntletCI answers "did I just break something?"
          </p>
          <div className="grid sm:grid-cols-2 gap-4">
            {[
              {
                title: "Complexity score went up",
                code: "Code Climate",
                body: "Code Climate tracks cyclomatic complexity trends. Useful for identifying files that are accumulating debt. Does not tell you whether the change you just made introduced a runtime regression.",
              },
              {
                title: "Null guard was removed",
                code: "GauntletCI",
                body: "GauntletCI detects that a defensive check was deleted in the diff. This change compiles, passes tests, and will not affect any complexity metric -- but it will cause a NullReferenceException in production.",
              },
              {
                title: "Duplication increased by 3%",
                code: "Code Climate",
                body: "Code Climate's duplication engine identifies repeated code blocks and trends them over time. Valuable for managing long-term maintainability. Not designed to catch behavioral changes in a pull request.",
              },
              {
                title: "Public method signature changed",
                code: "GauntletCI",
                body: "GauntletCI flags a changed method parameter type as a potential breaking change for external consumers. Code Climate does not model API contracts or cross-assembly compatibility.",
              },
            ].map((item) => (
              <div key={item.title} className="rounded-lg border border-border bg-card p-5">
                <div className="flex items-center gap-2 mb-2">
                  <h3 className="text-sm font-semibold">{item.title}</h3>
                  <span className={`text-[10px] font-semibold px-1.5 py-0.5 rounded ${item.code === "GauntletCI" ? "bg-cyan-500/20 text-cyan-400" : "bg-muted text-muted-foreground"}`}>
                    {item.code}
                  </span>
                </div>
                <p className="text-xs text-muted-foreground leading-relaxed">{item.body}</p>
              </div>
            ))}
          </div>
        </div>

        {/* Feature comparison table */}
        <div className="space-y-4">
          <h2 className="text-2xl font-bold tracking-tight">Feature comparison</h2>
          <div className="overflow-x-auto rounded-xl border border-border">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-border bg-card/80">
                  <th className="text-left px-5 py-3 font-semibold text-muted-foreground w-1/3">Feature</th>
                  <th className="text-left px-5 py-3 font-semibold text-cyan-400">GauntletCI</th>
                  <th className="text-left px-5 py-3 font-semibold text-muted-foreground">Code Climate</th>
                </tr>
              </thead>
              <tbody>
                {table.map((row, i) => (
                  <tr key={row.feature} className={`border-b border-border last:border-0 ${i % 2 === 0 ? "" : "bg-card/20"}`}>
                    <td className="px-5 py-3 text-muted-foreground">{row.feature}</td>
                    <td className="px-5 py-3 text-foreground font-medium">{row.gauntlet}</td>
                    <td className="px-5 py-3 text-muted-foreground">{row.cc}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>

        {/* When to use each */}
        <div className="grid sm:grid-cols-2 gap-6">
          <div className="rounded-xl border border-border bg-card p-7 space-y-4">
            <h2 className="text-lg font-bold">When Code Climate is the right choice</h2>
            <ul className="space-y-2 text-sm text-muted-foreground">
              {[
                "You need a long-term view of technical debt and code quality trends",
                "Engineering managers want a GPA-style quality score for the codebase",
                "You want test coverage enforcement as a CI gate",
                "Tracking duplication and complexity over time is a team priority",
              ].map((item) => (
                <li key={item} className="flex gap-2">
                  <span className="text-muted-foreground/40 shrink-0 mt-0.5">-</span>
                  {item}
                </li>
              ))}
            </ul>
          </div>
          <div className="rounded-xl border border-cyan-500/30 bg-cyan-500/5 p-7 space-y-4">
            <h2 className="text-lg font-bold">When GauntletCI is the right choice</h2>
            <ul className="space-y-2 text-sm text-muted-foreground">
              {[
                "You want to catch behavioral regressions before the commit is created",
                "Your team needs 100% local execution -- no code uploads, no SaaS account",
                "You want findings scoped to what changed, not the whole codebase",
                "You work in .NET / C# and want diff-aware behavioral detection",
                "Pre-commit speed is required -- under one second on every save",
              ].map((item) => (
                <li key={item} className="flex gap-2">
                  <span className="text-cyan-400 shrink-0 mt-0.5">-</span>
                  {item}
                </li>
              ))}
            </ul>
          </div>
        </div>

        {/* Complementary use */}
        <div className="rounded-xl border border-border bg-card/40 p-7 space-y-3">
          <h2 className="text-lg font-bold">Using GauntletCI and Code Climate together</h2>
          <p className="text-sm text-muted-foreground leading-relaxed">
            The two tools operate at different time horizons. Code Climate gives your team a
            quarterly view of codebase health and technical debt trends. GauntletCI gives each
            developer a sub-second view of the risk in their current change. Running them together
            covers both the macro (is our codebase healthy?) and the micro (did I just break
            something?).
          </p>
          <p className="text-sm text-muted-foreground leading-relaxed">
            A common setup: GauntletCI runs as a pre-commit hook to block behavioral regressions
            before push. Code Climate runs in CI to track maintainability trends and enforce
            coverage thresholds. Neither duplicates the other.
          </p>
        </div>

        {/* CTAs */}
        <div className="flex flex-col sm:flex-row gap-4 justify-center">
          <Link
            href="/docs"
            className="inline-flex items-center justify-center gap-2 rounded-lg bg-cyan-500 px-6 py-3 text-sm font-semibold text-background hover:bg-cyan-400 transition-colors"
          >
            Get started free
          </Link>
          <Link
            href="/docs/rules"
            className="inline-flex items-center gap-2 rounded-lg border border-border bg-card px-6 py-3 text-sm font-semibold hover:bg-card/80 transition-colors"
          >
            View the rule library
          </Link>
          <Link
            href="/compare/gauntletci-vs-sonarqube"
            className="inline-flex items-center gap-2 rounded-lg border border-border bg-card px-6 py-3 text-sm font-semibold hover:bg-card/80 transition-colors"
          >
            Compare vs SonarQube
          </Link>
        </div>

      </div>
    </div>
    <Footer />
    </>
  );
}

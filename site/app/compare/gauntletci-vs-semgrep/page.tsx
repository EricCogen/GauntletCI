import type { Metadata } from "next";
import Link from "next/link";
import { Header } from "@/components/header";
import { Footer } from "@/components/footer";

export const metadata: Metadata = {
  title: "GauntletCI vs Semgrep | Diff-Scoped Behavioral Rules vs Pattern Matching",
  description:
    "GauntletCI detects behavioral risks in only the lines that changed. Semgrep scans files with custom patterns. Compare scope, setup, privacy, and use cases.",
  alternates: { canonical: "/compare/gauntletci-vs-semgrep" },
  openGraph: { images: [{ url: '/og/compare-semgrep.png', width: 1200, height: 630 }] },
};

const table = [
  { feature: "Analysis scope",         gauntlet: "Changed diff lines only",           semgrep: "Full file or full repo scan" },
  { feature: "Rule authoring",         gauntlet: "20+ built-in, zero config",          semgrep: "Custom YAML patterns required" },
  { feature: "Behavioral drift detection", gauntlet: "Yes - removed logic, API contract changes", semgrep: "Limited - pattern matches only" },
  { feature: "Pre-commit speed",       gauntlet: "Under 1 second",                     semgrep: "Seconds to minutes (file-scoped)" },
  { feature: "False positives on unchanged code", gauntlet: "None - diff-scoped by design", semgrep: "Yes - scans pre-existing issues too" },
  { feature: "Local execution",        gauntlet: "100% local, no account",             semgrep: "CLI is local; rules registry is cloud" },
  { feature: "Air-gapped support",     gauntlet: "Yes - no network dependency",        semgrep: "Partial - rule sync needs network" },
  { feature: "No-code setup",          gauntlet: "Yes - works out of the box",         semgrep: "No - rules must be written or imported" },
  { feature: "LLM enrichment",         gauntlet: "Built-in ONNX, fully offline",       semgrep: "No" },
  { feature: "Baseline delta mode",    gauntlet: "Yes - suppress pre-existing issues", semgrep: "No" },
  { feature: "CI gate + inline comments", gauntlet: "Yes (Teams tier)",                semgrep: "Yes (paid)" },
  { feature: "Free tier",              gauntlet: "All 20+ rules, no account",          semgrep: "Limited free, account required" },
];

const jsonLd = {
  "@context": "https://schema.org",
  "@type": "Article",
  "headline": "GauntletCI vs Semgrep: Diff-Scoped Behavioral Rules vs Pattern Matching",
  "description": "GauntletCI detects behavioral risks in only the lines that changed. Semgrep scans files with custom patterns. Compare scope, setup, privacy, and use cases.",
  "url": "https://gauntletci.com/compare/gauntletci-vs-semgrep",
  "publisher": { "@type": "Organization", "name": "GauntletCI", "url": "https://gauntletci.com" },
};

export default function SemgrepComparePage() {
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
            GauntletCI vs Semgrep
          </h1>
          <p className="text-xl text-muted-foreground max-w-2xl mx-auto text-pretty">
            Semgrep matches patterns across your files. GauntletCI detects behavioral risk
            in the lines you actually changed. Different problems, different tools.
          </p>
        </div>

        {/* Key difference */}
        <div className="grid sm:grid-cols-2 gap-6">
          <div className="rounded-xl border border-border bg-card p-7 space-y-3">
            <p className="text-xs font-semibold uppercase tracking-widest text-muted-foreground">Semgrep</p>
            <h2 className="text-lg font-bold">Pattern matching across files</h2>
            <p className="text-sm text-muted-foreground leading-relaxed">
              You write YAML patterns or import community rules. Semgrep scans every matching
              file in your repository and reports anything that matches - whether it was
              introduced today or two years ago.
            </p>
            <p className="text-sm text-muted-foreground leading-relaxed">
              Powerful for enforcing custom coding standards and known vulnerability patterns.
              Requires ongoing rule maintenance. Produces findings on pre-existing code,
              so teams often see the same issues repeated run after run.
            </p>
          </div>
          <div className="rounded-xl border border-cyan-500/30 bg-cyan-500/5 p-7 space-y-3">
            <p className="text-xs font-semibold uppercase tracking-widest text-cyan-400">GauntletCI</p>
            <h2 className="text-lg font-bold">Behavioral risk in the diff only</h2>
            <p className="text-sm text-muted-foreground leading-relaxed">
              Rules are scoped to the lines you added or removed in the current change. A finding
              means the risk was introduced by this diff, not carried over from unchanged code.
              Zero false positives on pre-existing issues.
            </p>
            <p className="text-sm text-muted-foreground leading-relaxed">
              No YAML to write. 20+ built-in rules cover behavioral drift, security, async safety,
              data integrity, and architecture violations - all running in under one second with
              no account or network call required.
            </p>
          </div>
        </div>

        {/* The core gap Semgrep cannot close */}
        <div className="space-y-6">
          <h2 className="text-2xl font-bold tracking-tight">The gap Semgrep does not close</h2>
          <p className="text-muted-foreground">
            Semgrep excels at "does this code contain a known-bad pattern?" GauntletCI answers
            a different question: "did this change introduce a behavioral risk that was not there before?"
          </p>
          <div className="grid sm:grid-cols-2 gap-4">
            {[
              {
                title: "Removed logic without test coverage",
                body: "A null guard, a guard clause, or a fallback branch gets deleted. The code still compiles, no new bad pattern was added, so Semgrep reports nothing. GauntletCI flags the removal.",
              },
              {
                title: "Public API contract changes",
                body: "A method parameter type changes or a public member is removed. No pattern matches a breaking change. GauntletCI checks for API surface mutations on every diff.",
              },
              {
                title: "Scope drift vs ticket intent",
                body: "A database migration lands in a UI-only ticket branch. No pattern matches 'unexpected change'. GauntletCI attaches Jira/Linear ticket context and flags the mismatch.",
              },
              {
                title: "Behavioral drift that tests miss",
                body: "Added code changes a side effect without breaking any assertion. No pattern to match, green CI. GauntletCI detects the structural change and flags the missing test coverage.",
              },
            ].map((item) => (
              <div key={item.title} className="rounded-lg border border-border bg-card p-5">
                <h3 className="text-sm font-semibold mb-2">{item.title}</h3>
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
                  <th className="text-left px-5 py-3 font-semibold text-muted-foreground">Semgrep</th>
                </tr>
              </thead>
              <tbody>
                {table.map((row, i) => (
                  <tr key={row.feature} className={`border-b border-border last:border-0 ${i % 2 === 0 ? "" : "bg-card/20"}`}>
                    <td className="px-5 py-3 text-muted-foreground">{row.feature}</td>
                    <td className="px-5 py-3 text-foreground font-medium">{row.gauntlet}</td>
                    <td className="px-5 py-3 text-muted-foreground">{row.semgrep}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>

        {/* When to use each */}
        <div className="grid sm:grid-cols-2 gap-6">
          <div className="rounded-xl border border-border bg-card p-7 space-y-4">
            <h2 className="text-lg font-bold">When Semgrep is the right choice</h2>
            <ul className="space-y-2 text-sm text-muted-foreground">
              {[
                "You need to enforce custom internal coding standards with bespoke patterns",
                "You want to scan for known OWASP / CVE patterns across all files",
                "Your team has bandwidth to write and maintain YAML rules",
                "You need multi-language support beyond .NET and C#",
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
                "You want zero-config detection of behavioral and structural risk",
                "You need pre-commit speed - under one second on every commit",
                "You want findings only on what changed, not the entire codebase",
                "You need 100% local execution with no account or network dependency",
                "You work in a .NET / C# codebase and want diff-aware coverage from day one",
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
          <h2 className="text-lg font-bold">They solve different problems - use both</h2>
          <p className="text-sm text-muted-foreground leading-relaxed">
            Semgrep and GauntletCI complement each other. Run GauntletCI as a pre-commit hook for
            instant behavioral risk detection on every diff. Use Semgrep in CI for periodic
            codebase-wide pattern enforcement. Semgrep catches "this pattern exists somewhere";
            GauntletCI catches "this change introduced a new risk."
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

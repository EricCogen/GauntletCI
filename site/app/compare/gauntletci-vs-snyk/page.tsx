import type { Metadata } from "next";
import Link from "next/link";

export const metadata: Metadata = {
  title: "GauntletCI vs Snyk | Behavioral Risk Detection vs Vulnerability Scanning",
  description:
    "Snyk scans dependencies and code for known vulnerabilities. GauntletCI detects behavioral regressions in the lines you changed -- pre-commit, 100% local, zero false positives on pre-existing code.",
  alternates: { canonical: "/compare/gauntletci-vs-snyk" },
};

const table = [
  { feature: "Primary focus",              gauntlet: "Behavioral regressions in the diff",          snyk: "Known vulnerabilities in dependencies and code" },
  { feature: "Analysis scope",             gauntlet: "Changed diff lines only",                     snyk: "Full dependency tree + file scan" },
  { feature: "Data leaves the machine",    gauntlet: "Never -- 100% local execution",               snyk: "Yes -- SaaS platform, code sent to Snyk servers" },
  { feature: "Pre-commit speed",           gauntlet: "Under 1 second",                              snyk: "Seconds to minutes (network round-trip)" },
  { feature: "Account required",           gauntlet: "No -- works fully offline",                   snyk: "Yes -- Snyk account and authentication required" },
  { feature: "Air-gap / data residency",   gauntlet: "Yes -- no network dependency",                snyk: "No -- requires Snyk cloud for most features" },
  { feature: "False positives on old code",gauntlet: "None -- diff-scoped by design",               snyk: "Yes -- reports pre-existing issues on every run" },
  { feature: "Removed logic detection",    gauntlet: "Yes -- flags deleted null guards, handlers",  snyk: "No -- pattern-based, cannot detect removals" },
  { feature: "API contract change detection", gauntlet: "Yes -- public surface mutations flagged",  snyk: "No -- not in scope" },
  { feature: "Dependency vulnerability scan", gauntlet: "No -- not in scope",                       snyk: "Yes -- Snyk core strength" },
  { feature: "License compliance scanning",gauntlet: "No",                                          snyk: "Yes (paid tier)" },
  { feature: "Local LLM enrichment",       gauntlet: "Built-in ONNX, fully offline",                snyk: "No" },
  { feature: "Baseline delta mode",        gauntlet: "Yes -- suppress pre-existing findings",       snyk: "No" },
  { feature: "Free tier",                  gauntlet: "All rules, no account",                       snyk: "Limited -- account required, rate-limited" },
];

const jsonLd = {
  "@context": "https://schema.org",
  "@type": "Article",
  "headline": "GauntletCI vs Snyk: Behavioral Risk Detection vs Vulnerability Scanning",
  "description": "Snyk scans dependencies and code for known vulnerabilities. GauntletCI detects behavioral regressions in the lines you changed -- pre-commit, 100% local, zero false positives on pre-existing code.",
  "url": "https://gauntletci.com/compare/gauntletci-vs-snyk",
  "publisher": { "@type": "Organization", "name": "GauntletCI", "url": "https://gauntletci.com" },
};

export default function SnykComparePage() {
  return (
    <>
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLd) }} />
      <div className="min-h-screen bg-background">
      <div className="mx-auto max-w-5xl px-4 sm:px-6 lg:px-8 py-20 sm:py-28 space-y-20">

        {/* Hero */}
        <div className="text-center space-y-5">
          <p className="text-sm font-semibold uppercase tracking-widest text-cyan-400">Compare</p>
          <h1 className="text-4xl sm:text-5xl font-bold tracking-tight text-balance">
            GauntletCI vs Snyk
          </h1>
          <p className="text-xl text-muted-foreground max-w-2xl mx-auto text-pretty">
            Snyk finds known vulnerabilities in your dependencies and code patterns.
            GauntletCI finds behavioral regressions in the lines you actually changed.
            They answer entirely different questions.
          </p>
        </div>

        {/* Key difference */}
        <div className="grid sm:grid-cols-2 gap-6">
          <div className="rounded-xl border border-border bg-card p-7 space-y-3">
            <p className="text-xs font-semibold uppercase tracking-widest text-muted-foreground">Snyk</p>
            <h2 className="text-lg font-bold">Vulnerability and dependency scanning</h2>
            <p className="text-sm text-muted-foreground leading-relaxed">
              Snyk connects to your repository, scans your dependency manifest and source code
              against a continuously updated vulnerability database, and reports known CVEs,
              license risks, and code-level security patterns. It is primarily a SaaS platform --
              your code and dependency information is processed by Snyk servers.
            </p>
            <p className="text-sm text-muted-foreground leading-relaxed">
              Snyk excels at answering: "Does this codebase depend on a package with a known
              vulnerability?" and "Does this code match a known-bad security pattern?" It is
              the right tool for supply chain risk and CVE hygiene.
            </p>
          </div>
          <div className="rounded-xl border border-cyan-500/30 bg-cyan-500/5 p-7 space-y-3">
            <p className="text-xs font-semibold uppercase tracking-widest text-cyan-400">GauntletCI</p>
            <h2 className="text-lg font-bold">Behavioral risk in the diff only</h2>
            <p className="text-sm text-muted-foreground leading-relaxed">
              GauntletCI runs entirely on your machine. It reads the git diff, applies
              deterministic behavioral rules to the changed lines, and reports risk introduced
              by this specific change -- before the commit is created. No code leaves the
              machine. No account required. No network call.
            </p>
            <p className="text-sm text-muted-foreground leading-relaxed">
              GauntletCI answers: "Did this change remove a guard clause?", "Did it alter a
              public API contract?", "Did it introduce an async anti-pattern?" -- questions
              that vulnerability scanners are not designed to answer.
            </p>
          </div>
        </div>

        {/* The gap Snyk cannot close */}
        <div className="space-y-6">
          <h2 className="text-2xl font-bold tracking-tight">The gap Snyk does not close</h2>
          <p className="text-muted-foreground">
            Snyk's vulnerability database covers known-bad patterns. Most production incidents
            are not caused by known CVEs -- they are caused by behavioral drift introduced in
            ordinary code changes: removed validations, altered exception handling, changed
            concurrency patterns, dropped null checks. These are not in any vulnerability
            database because they are not vulnerabilities. They are behavioral regressions.
          </p>
          <div className="grid sm:grid-cols-2 gap-4">
            {[
              {
                title: "Removed null guard",
                body: "A developer deletes an early return that protected a null dereference. No CVE exists for this. No dependency changed. Snyk reports nothing. GauntletCI flags the removed guard clause.",
              },
              {
                title: "Changed exception handling",
                body: "A catch block that logged and re-threw is replaced with one that swallows the exception. Perfectly valid code. No vulnerability pattern. GauntletCI detects the behavioral change and flags it.",
              },
              {
                title: "Public API surface mutation",
                body: "A public method parameter type changes from string to int. All internal call sites compile. External consumers and serialized payloads break at runtime. Snyk does not model runtime API contracts.",
              },
              {
                title: "Async anti-patterns",
                body: "An async void event handler is introduced, or a Task is blocked with .Result in a synchronous context. Correct C# code. No known vulnerability. GauntletCI catches the structural anti-pattern.",
              },
            ].map((item) => (
              <div key={item.title} className="rounded-lg border border-border bg-card p-5">
                <h3 className="text-sm font-semibold mb-2">{item.title}</h3>
                <p className="text-xs text-muted-foreground leading-relaxed">{item.body}</p>
              </div>
            ))}
          </div>
        </div>

        {/* Privacy and data residency */}
        <div className="space-y-5">
          <h2 className="text-2xl font-bold tracking-tight">Privacy and data residency</h2>
          <p className="text-muted-foreground">
            This is often the deciding factor for teams operating under strict data handling
            requirements. The two tools take fundamentally different positions.
          </p>
          <div className="grid sm:grid-cols-2 gap-4">
            <div className="rounded-lg border border-border bg-card/50 p-5 space-y-2">
              <p className="text-sm font-semibold text-muted-foreground">Snyk</p>
              <ul className="space-y-1.5 text-xs text-muted-foreground">
                <li className="flex gap-2"><span className="shrink-0 mt-0.5">--</span>Requires an authenticated Snyk account</li>
                <li className="flex gap-2"><span className="shrink-0 mt-0.5">--</span>Code and dependency metadata sent to Snyk cloud</li>
                <li className="flex gap-2"><span className="shrink-0 mt-0.5">--</span>Not suitable for air-gapped or strict residency environments without an enterprise plan</li>
                <li className="flex gap-2"><span className="shrink-0 mt-0.5">--</span>Snyk Enterprise offers self-hosted options at additional cost</li>
              </ul>
            </div>
            <div className="rounded-lg border border-cyan-500/20 bg-cyan-500/5 p-5 space-y-2">
              <p className="text-sm font-semibold text-cyan-400">GauntletCI</p>
              <ul className="space-y-1.5 text-xs text-muted-foreground">
                <li className="flex gap-2"><span className="text-cyan-400 shrink-0 mt-0.5">--</span>No account required. No registration.</li>
                <li className="flex gap-2"><span className="text-cyan-400 shrink-0 mt-0.5">--</span>Zero network calls -- all analysis runs in-process</li>
                <li className="flex gap-2"><span className="text-cyan-400 shrink-0 mt-0.5">--</span>Fully air-gap compatible from the free tier</li>
                <li className="flex gap-2"><span className="text-cyan-400 shrink-0 mt-0.5">--</span>Optional local LLM enrichment via ONNX -- no API key, no internet</li>
              </ul>
            </div>
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
                  <th className="text-left px-5 py-3 font-semibold text-muted-foreground">Snyk</th>
                </tr>
              </thead>
              <tbody>
                {table.map((row, i) => (
                  <tr key={row.feature} className={`border-b border-border last:border-0 ${i % 2 === 0 ? "" : "bg-card/20"}`}>
                    <td className="px-5 py-3 text-muted-foreground">{row.feature}</td>
                    <td className="px-5 py-3 text-foreground font-medium">{row.gauntlet}</td>
                    <td className="px-5 py-3 text-muted-foreground">{row.snyk}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>

        {/* When to use each */}
        <div className="grid sm:grid-cols-2 gap-6">
          <div className="rounded-xl border border-border bg-card p-7 space-y-4">
            <h2 className="text-lg font-bold">When Snyk is the right choice</h2>
            <ul className="space-y-2 text-sm text-muted-foreground">
              {[
                "You need to track CVEs in your open source dependencies",
                "Your team requires license compliance scanning across the dependency tree",
                "You want continuous monitoring with Snyk's vulnerability feed",
                "Security and compliance reporting is a top priority for your organization",
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
                "You want pre-commit detection of behavioral regressions before code review",
                "Your team needs 100% local execution with no data leaving the machine",
                "You work in a .NET / C# codebase and want diff-aware behavioral rules",
                "You operate in an air-gapped or strict data residency environment",
                "You want findings scoped only to what changed -- zero noise from pre-existing issues",
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
          <h2 className="text-lg font-bold">Using GauntletCI and Snyk together</h2>
          <p className="text-sm text-muted-foreground leading-relaxed">
            The tools operate at different layers of risk and complement each other well. Run
            Snyk in your CI pipeline to block merges when new CVEs are introduced in
            dependencies. Run GauntletCI as a pre-commit hook to block commits when a
            behavioral regression is introduced in the diff. Snyk guards the supply chain.
            GauntletCI guards the behavior of the code you write.
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
    </>
  );
}

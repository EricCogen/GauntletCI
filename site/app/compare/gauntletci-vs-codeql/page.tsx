import type { Metadata } from "next";
import Link from "next/link";
import { Check, Minus, AlertCircle } from "lucide-react";

export const metadata: Metadata = {
  title: "GauntletCI vs. CodeQL: Behavioral Regression Detection vs. Security Variant Analysis",
  description:
    "Compare GauntletCI and CodeQL. CodeQL finds security vulnerabilities across an entire codebase using semantic queries. GauntletCI catches behavioral regressions and breaking changes in the diff, pre-commit, with no cloud required.",
  alternates: { canonical: "/compare/gauntletci-vs-codeql" },
};

const tableRows = [
  {
    scenario: "Finding security vulnerabilities (CVEs, CWEs) across a codebase",
    winner: "codeql",
    note: "CodeQL's variant analysis is purpose-built for security research.",
  },
  {
    scenario: "Catching behavioral regressions in a pull request diff",
    winner: "gauntlet",
    note: "GauntletCI analyzes only the changed lines for breaking changes.",
  },
  {
    scenario: "Pre-commit feedback before a push",
    winner: "gauntlet",
    note: "CodeQL runs post-push in CI and takes minutes. GauntletCI runs in milliseconds.",
  },
  {
    scenario: "Air-gapped or strict data-residency environments",
    winner: "gauntlet",
    note: "No code leaves the machine. CodeQL uploads results to GitHub Security.",
  },
  {
    scenario: "Writing custom semantic queries for a specific vulnerability class",
    winner: "codeql",
    note: "CodeQL's QL language lets you write precise, reusable security queries.",
  },
  {
    scenario: "GitHub Security tab and code scanning alerts integration",
    winner: "codeql",
    note: "CodeQL is deeply integrated with GitHub's native security tooling.",
  },
  {
    scenario: "Generating compliance audit trails per diff",
    winner: "gauntlet",
    note: "Every GauntletCI run produces a structured JSON report tied to the diff.",
  },
  {
    scenario: "Optional offline AI explanations for findings",
    winner: "gauntlet",
    note: "Built-in ONNX engine. No API key or cloud call needed.",
  },
];

const featureRows = [
  { label: "Diff-scoped analysis (changed lines only)", gauntlet: "yes" as const, codeql: "no" as const },
  { label: "100% local execution, no code upload", gauntlet: "yes" as const, codeql: "partial" as const },
  { label: "Pre-commit (before push) feedback", gauntlet: "yes" as const, codeql: "no" as const },
  { label: "Air-gap / data residency friendly", gauntlet: "yes" as const, codeql: "no" as const },
  { label: "Sub-second analysis time", gauntlet: "yes" as const, codeql: "no" as const },
  { label: "Security variant / CVE detection", gauntlet: "no" as const, codeql: "yes" as const },
  { label: "Custom query language (QL)", gauntlet: "no" as const, codeql: "yes" as const },
  { label: "GitHub Security tab integration", gauntlet: "partial" as const, codeql: "yes" as const },
  { label: "GitHub inline PR comments", gauntlet: "yes" as const, codeql: "yes" as const },
  { label: "Behavioral regression rule set (C#/.NET)", gauntlet: "yes" as const, codeql: "partial" as const },
  { label: "Baseline delta mode (suppress existing findings)", gauntlet: "yes" as const, codeql: "no" as const },
  { label: "Local LLM enrichment (no API key)", gauntlet: "yes" as const, codeql: "no" as const },
  { label: "Free for public repos", gauntlet: "yes" as const, codeql: "yes" as const },
];

type CellVal = "yes" | "no" | "partial";

function Cell({ value, cyan }: { value: CellVal; cyan?: boolean }) {
  if (value === "yes")
    return <Check className={`h-5 w-5 mx-auto ${cyan ? "text-cyan-400" : "text-emerald-500/70"}`} />;
  if (value === "partial")
    return <AlertCircle className="h-4 w-4 mx-auto text-amber-400/60" />;
  return <Minus className="h-4 w-4 mx-auto text-muted-foreground/30" />;
}

const jsonLd = {
  "@context": "https://schema.org",
  "@type": "Article",
  "headline": "GauntletCI vs. CodeQL: Behavioral Regression Detection vs. Security Variant Analysis",
  "description": "Compare GauntletCI and CodeQL. CodeQL finds security vulnerabilities across an entire codebase. GauntletCI catches behavioral regressions in the diff, pre-commit, with no cloud required.",
  "url": "https://gauntletci.com/compare/gauntletci-vs-codeql",
  "publisher": { "@type": "Organization", "name": "GauntletCI", "url": "https://gauntletci.com" },
};

export default function VsCodeQLPage() {
  return (
    <>
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLd) }} />
      <div className="min-h-screen bg-background text-foreground">
      {/* Hero */}
      <section className="border-b border-border py-16 sm:py-20">
        <div className="mx-auto max-w-4xl px-4 sm:px-6 lg:px-8 text-center">
          <p className="text-sm font-semibold text-cyan-400 uppercase tracking-widest mb-3">
            Tool Comparison
          </p>
          <h1 className="text-4xl sm:text-5xl font-bold tracking-tight mb-5">
            GauntletCI vs. CodeQL
          </h1>
          <p className="text-lg text-muted-foreground max-w-2xl mx-auto">
            CodeQL performs deep semantic analysis of an entire codebase to find security
            vulnerabilities. GauntletCI analyzes only the lines that changed in a pull request
            to catch behavioral regressions before they merge. They operate at different points
            in the development cycle and solve different problems.
          </p>
        </div>
      </section>

      <div className="mx-auto max-w-4xl px-4 sm:px-6 lg:px-8 py-16 space-y-20">

        {/* What CodeQL does best */}
        <section>
          <h2 className="text-2xl font-semibold mb-4">What CodeQL does best</h2>
          <p className="text-muted-foreground mb-4">
            CodeQL is GitHub's semantic code analysis engine. It builds a queryable database
            from your source code and lets security researchers write precise QL queries to
            find vulnerability classes across an entire codebase. It is the gold standard for
            security variant analysis in open source and enterprise projects.
          </p>
          <ul className="space-y-2 text-sm text-muted-foreground">
            {[
              "Whole-codebase semantic analysis - finds vulnerabilities regardless of where they live",
              "QL query language - write reusable, composable security queries for any vulnerability class",
              "Variant analysis - find all instances of a vulnerability pattern across a codebase or organization",
              "GitHub Security tab integration - results surface as code scanning alerts on pull requests",
              "CVE and CWE categorization built into the default query packs",
              "Supports C#, Java, JavaScript, Python, Go, Ruby, Swift, and C/C++",
              "Free for public repositories via GitHub Actions",
            ].map((item) => (
              <li key={item} className="flex items-start gap-2">
                <Check className="h-4 w-4 text-emerald-500/60 mt-0.5 shrink-0" />
                <span>{item}</span>
              </li>
            ))}
          </ul>
        </section>

        {/* Where GauntletCI differs */}
        <section>
          <h2 className="text-2xl font-semibold mb-4">Where GauntletCI differs</h2>
          <p className="text-muted-foreground mb-6">
            CodeQL and GauntletCI have almost no overlap in what they catch. CodeQL finds
            security vulnerabilities that already exist anywhere in the code. GauntletCI finds
            behavioral regressions introduced by a specific change. If CodeQL is your security
            scanner, GauntletCI is your regression guard.
          </p>
          <div className="grid sm:grid-cols-3 gap-4">
            {[
              {
                title: "Change-time analysis",
                body: "GauntletCI only looks at the diff. It runs in milliseconds before a commit and tells you what your change broke - not what was already broken in the codebase before you touched it.",
              },
              {
                title: "No QL required",
                body: "CodeQL's power comes with complexity. GauntletCI's 30 built-in C#/.NET rules cover the most common behavioral regression classes with zero configuration required to start.",
              },
              {
                title: "100% local, always",
                body: "CodeQL results are uploaded to GitHub. GauntletCI never sends code anywhere. No GitHub Advanced Security subscription required. Works in air-gapped environments and private networks.",
              },
            ].map((card) => (
              <div key={card.title} className="rounded-lg border border-border bg-card/50 p-5">
                <p className="font-semibold text-cyan-400 mb-2">{card.title}</p>
                <p className="text-sm text-muted-foreground">{card.body}</p>
              </div>
            ))}
          </div>
        </section>

        {/* Feature comparison table */}
        <section>
          <h2 className="text-2xl font-semibold mb-4">Feature comparison</h2>
          <div className="rounded-xl border border-border overflow-x-auto">
            <div className="grid grid-cols-[2fr_1fr_1fr] min-w-[480px] bg-card/80 border-b border-border">
              <div className="px-5 py-3 text-sm font-semibold text-muted-foreground">Capability</div>
              <div className="px-3 py-3 text-center text-sm font-semibold text-cyan-400 bg-cyan-500/5">GauntletCI</div>
              <div className="px-3 py-3 text-center text-sm font-semibold text-foreground/70">CodeQL</div>
            </div>
            {featureRows.map((row, i) => (
              <div
                key={row.label}
                className={`grid grid-cols-[2fr_1fr_1fr] min-w-[480px] border-b border-border last:border-0 ${i % 2 === 0 ? "bg-transparent" : "bg-card/20"}`}
              >
                <div className="px-5 py-3 text-sm text-foreground/90 flex items-center">{row.label}</div>
                <div className="px-3 py-3 flex items-center justify-center bg-cyan-500/5">
                  <Cell value={row.gauntlet} cyan />
                </div>
                <div className="px-3 py-3 flex items-center justify-center">
                  <Cell value={row.codeql} />
                </div>
              </div>
            ))}
          </div>
          <div className="mt-4 flex flex-wrap gap-6 text-sm text-muted-foreground">
            <span className="flex items-center gap-2"><Check className="h-4 w-4 text-cyan-400" /> Yes</span>
            <span className="flex items-center gap-2"><AlertCircle className="h-4 w-4 text-amber-400/60" /> Partial / requires GitHub Advanced Security</span>
            <span className="flex items-center gap-2"><Minus className="h-4 w-4 text-muted-foreground/30" /> No</span>
          </div>
        </section>

        {/* When to use each */}
        <section>
          <h2 className="text-2xl font-semibold mb-4">When to use each</h2>
          <div className="rounded-xl border border-border overflow-x-auto">
            <div className="grid grid-cols-[2fr_1fr] min-w-[480px] bg-card/80 border-b border-border">
              <div className="px-5 py-3 text-sm font-semibold text-muted-foreground">Scenario</div>
              <div className="px-5 py-3 text-sm font-semibold text-muted-foreground">Recommended tool</div>
            </div>
            {tableRows.map((row, i) => (
              <div
                key={row.scenario}
                className={`grid grid-cols-[2fr_1fr] min-w-[480px] border-b border-border last:border-0 ${i % 2 === 0 ? "bg-transparent" : "bg-card/20"}`}
              >
                <div className="px-5 py-4">
                  <p className="text-sm text-foreground/90">{row.scenario}</p>
                  <p className="text-xs text-muted-foreground/60 mt-1">{row.note}</p>
                </div>
                <div className="px-5 py-4 flex items-center">
                  <span className={`text-sm font-semibold ${row.winner === "gauntlet" ? "text-cyan-400" : "text-foreground/70"}`}>
                    {row.winner === "gauntlet" ? "GauntletCI" : "CodeQL"}
                  </span>
                </div>
              </div>
            ))}
          </div>
        </section>

        {/* Timing explains the gap */}
        <section>
          <h2 className="text-2xl font-semibold mb-4">Timing explains the gap</h2>
          <p className="text-muted-foreground mb-4">
            CodeQL runs after a push, typically taking 5 to 30 minutes depending on codebase size.
            It is designed for thoroughness, not speed. That is the right tradeoff for finding
            vulnerabilities across millions of lines of code.
          </p>
          <p className="text-muted-foreground mb-4">
            GauntletCI runs in milliseconds before a commit. It does not scan the whole codebase.
            It asks one focused question: "Did this change introduce a behavioral regression?"
            The speed is not a shortcut; it is the point. Pre-commit feedback changes developer
            behavior in a way that post-push alerts do not.
          </p>
          <p className="text-muted-foreground">
            A CodeQL alert that surfaces 20 minutes after a push competes with the next task
            already in flight. A GauntletCI block that fires before the commit gets fixed
            immediately, in context, at zero switching cost.
          </p>
        </section>

        {/* Using both */}
        <section className="rounded-lg border border-cyan-500/30 bg-cyan-500/5 p-6">
          <h2 className="text-xl font-semibold mb-3">Using GauntletCI and CodeQL together</h2>
          <p className="text-sm text-muted-foreground mb-3">
            The two tools do not overlap in what they catch. A common setup: GauntletCI runs
            pre-commit to block behavioral regressions at the developer workstation. CodeQL runs
            on push to scan the full codebase for security vulnerabilities. Each catches what
            the other cannot.
          </p>
          <p className="text-sm text-muted-foreground">
            If CodeQL finds a vulnerability class in a PR, GauntletCI can be configured with
            a custom rule to block similar patterns in future diffs. The two tools complement
            each other across different layers of the development lifecycle.
          </p>
        </section>

        {/* CTA */}
        <section className="text-center border-t border-border pt-12">
          <h2 className="text-2xl font-semibold mb-3">Try GauntletCI</h2>
          <p className="text-muted-foreground mb-6">
            Install in under two minutes. No account, no cloud, no configuration required to start.
          </p>
          <div className="flex flex-wrap justify-center gap-4">
            <Link
              href="/docs"
              className="inline-flex items-center gap-2 rounded-lg bg-cyan-500 text-black font-semibold px-6 py-3 text-sm hover:bg-cyan-400 transition-colors"
            >
              Get started
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
              GauntletCI vs. SonarQube
            </Link>
          </div>
        </section>

      </div>
    </>
  );
}
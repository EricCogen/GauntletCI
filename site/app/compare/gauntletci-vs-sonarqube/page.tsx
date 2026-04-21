import type { Metadata } from "next";
import Link from "next/link";
import { Check, Minus, AlertCircle } from "lucide-react";

export const metadata: Metadata = {
  title: "GauntletCI vs. SonarQube: Diff Analysis vs. Whole-Repo Static Analysis",
  description:
    "Compare GauntletCI and SonarQube. GauntletCI analyzes only what changed, runs 100% locally, and catches behavioral regressions in pre-commit. SonarQube scans the whole repo in CI.",
  alternates: { canonical: "/compare/gauntletci-vs-sonarqube" },
  openGraph: { images: [{ url: '/og/compare-sonarqube.png', width: 1200, height: 630 }] },
};

const tableRows = [
  {
    scenario: "Finding vulnerabilities across an entire existing codebase",
    winner: "sonar",
    note: "Whole-repo scanning is SonarQube's core strength.",
  },
  {
    scenario: "Catching behavioral regressions in a pull request diff",
    winner: "gauntlet",
    note: "GauntletCI analyzes only the changed lines, not the full repo.",
  },
  {
    scenario: "Pre-commit feedback before a push",
    winner: "gauntlet",
    note: "Sub-second local analysis. SonarQube runs post-push in CI.",
  },
  {
    scenario: "Air-gapped or strict data-residency environments",
    winner: "gauntlet",
    note: "No code leaves the machine. No SaaS account required.",
  },
  {
    scenario: "Generating compliance audit trails",
    winner: "gauntlet",
    note: "Every run produces a structured JSON report tied to the diff.",
  },
  {
    scenario: "Multi-language projects (Java, Python, JS, Go, etc.)",
    winner: "sonar",
    note: "SonarQube supports 30+ languages. GauntletCI is C#/.NET focused.",
  },
  {
    scenario: "Enforcing organization-wide quality gates in CI",
    winner: "sonar",
    note: "SonarQube quality gates are mature and widely adopted.",
  },
  {
    scenario: "Optional offline AI explanations for findings",
    winner: "gauntlet",
    note: "Built-in ONNX engine. No API key or cloud call needed.",
  },
];

const featureRows = [
  { label: "Diff-scoped analysis (changed lines only)", gauntlet: "yes" as const, sonar: "no" as const },
  { label: "100% local execution, no code upload", gauntlet: "yes" as const, sonar: "partial" as const },
  { label: "Pre-commit (before push) feedback", gauntlet: "yes" as const, sonar: "no" as const },
  { label: "Air-gap / data residency friendly", gauntlet: "yes" as const, sonar: "partial" as const },
  { label: "C#/.NET behavioral rule set", gauntlet: "yes" as const, sonar: "partial" as const },
  { label: "Whole-repo scan across all languages", gauntlet: "no" as const, sonar: "yes" as const },
  { label: "Quality gate enforcement in CI", gauntlet: "partial" as const, sonar: "yes" as const },
  { label: "GitHub inline PR comments", gauntlet: "yes" as const, sonar: "yes" as const },
  { label: "Baseline delta mode (suppress existing findings)", gauntlet: "yes" as const, sonar: "partial" as const },
  { label: "Local LLM enrichment (no API key)", gauntlet: "yes" as const, sonar: "no" as const },
  { label: "Free tier with full rule set", gauntlet: "yes" as const, sonar: "partial" as const },
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
  "headline": "GauntletCI vs. SonarQube: Diff Analysis vs. Whole-Repo Static Analysis",
  "description": "Compare GauntletCI and SonarQube. GauntletCI analyzes only what changed, runs 100% locally, and catches behavioral regressions in pre-commit.",
  "url": "https://gauntletci.com/compare/gauntletci-vs-sonarqube",
  "publisher": { "@type": "Organization", "name": "GauntletCI", "url": "https://gauntletci.com" },
};

export default function VsSonarQubePage() {
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
            GauntletCI vs. SonarQube
          </h1>
          <p className="text-lg text-muted-foreground max-w-2xl mx-auto">
            SonarQube scans your whole repository to find code smells and vulnerabilities.
            GauntletCI analyzes only the lines that changed in a pull request to catch
            behavioral regressions before they merge. They solve different problems.
          </p>
        </div>
      </section>

      <div className="mx-auto max-w-4xl px-4 sm:px-6 lg:px-8 py-16 space-y-20">

        {/* What SonarQube does best */}
        <section>
          <h2 className="text-2xl font-semibold mb-4">What SonarQube does best</h2>
          <p className="text-muted-foreground mb-4">
            SonarQube is a mature, battle-tested platform for continuous code quality across an
            entire codebase. It supports over 30 languages, integrates deeply with enterprise
            CI/CD pipelines, and gives teams a centralized quality gate that enforces standards
            across every commit from every contributor.
          </p>
          <ul className="space-y-2 text-sm text-muted-foreground">
            {[
              "Whole-repository static analysis - catches issues anywhere in the code, not just in changed lines",
              "Wide language coverage - Java, Python, JavaScript, TypeScript, Go, C#, and more",
              "Centralized quality gate dashboard with trend tracking over time",
              "Security hotspot detection and OWASP/CWE categorization",
              "Deep IDE integration via SonarLint for real-time feedback",
              "Mature enterprise features: LDAP, SSO, branch analysis, portfolio views",
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
            GauntletCI is not a replacement for SonarQube. It solves a different, more
            targeted problem: catching the behavioral regressions and breaking changes that
            live in the diff itself, before the code is pushed.
          </p>
          <div className="grid sm:grid-cols-3 gap-4">
            {[
              {
                title: "Diff-only analysis",
                body: "GauntletCI only looks at the lines that changed. It never scans your whole repo. This makes it fast enough to run pre-commit and precise enough to avoid alert fatigue from pre-existing issues.",
              },
              {
                title: "100% local execution",
                body: "No code is uploaded anywhere. No SaaS account, no cloud tenant, no API key required. Runs entirely in-process on the developer machine or CI runner.",
              },
              {
                title: "Behavioral risk focus",
                body: "GauntletCI rules target behavioral drift: removed null guards, changed return types, dropped exception handling, altered concurrency patterns. Things that compile fine but break production.",
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
              <div className="px-3 py-3 text-center text-sm font-semibold text-foreground/70">SonarQube</div>
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
                  <Cell value={row.sonar} />
                </div>
              </div>
            ))}
          </div>
          <div className="mt-4 flex flex-wrap gap-6 text-sm text-muted-foreground">
            <span className="flex items-center gap-2"><Check className="h-4 w-4 text-cyan-400" /> Yes</span>
            <span className="flex items-center gap-2"><AlertCircle className="h-4 w-4 text-amber-400/60" /> Partial / paid tier only</span>
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
                    {row.winner === "gauntlet" ? "GauntletCI" : "SonarQube"}
                  </span>
                </div>
              </div>
            ))}
          </div>
        </section>

        {/* Why diff-based analysis matters */}
        <section>
          <h2 className="text-2xl font-semibold mb-4">Why diff-based analysis matters</h2>
          <p className="text-muted-foreground mb-4">
            Most static analysis tools examine the state of the codebase. They answer: "Does this
            code have problems?" GauntletCI answers a different question: "Did this change introduce
            a problem?"
          </p>
          <p className="text-muted-foreground mb-4">
            The distinction matters because engineers already ignore whole-repo findings. A report
            showing 847 existing issues is background noise. A report showing that your pull request
            removed a null guard that protected a hot path is signal.
          </p>
          <p className="text-muted-foreground">
            Diff-based analysis also means GauntletCI can run in milliseconds before a commit, not
            minutes after a push. Finding a regression pre-commit costs nothing. Finding it in
            production costs everything.
          </p>
        </section>

        {/* Using both */}
        <section className="rounded-lg border border-cyan-500/30 bg-cyan-500/5 p-6">
          <h2 className="text-xl font-semibold mb-3">Using GauntletCI and SonarQube together</h2>
          <p className="text-sm text-muted-foreground mb-3">
            Many teams run both. SonarQube enforces quality gates on the full codebase in CI.
            GauntletCI runs pre-commit on the diff to catch behavioral regressions before they
            reach the pipeline. The two tools complement each other rather than compete.
          </p>
          <p className="text-sm text-muted-foreground">
            A common setup: GauntletCI blocks the commit if a breaking change is detected,
            SonarQube blocks the merge if code coverage or duplication thresholds are violated.
            Together they cover both change-time and codebase-level risk.
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
              href="/#comparison"
              className="inline-flex items-center gap-2 rounded-lg border border-border bg-card px-6 py-3 text-sm font-semibold hover:bg-card/80 transition-colors"
            >
              Full comparison matrix
            </Link>
          </div>
        </section>

      </div>
    </div>
    </>
  );
}
import type { Metadata } from "next";
import Link from "next/link";
import { Check, Minus, AlertCircle } from "lucide-react";
import { Header } from "@/components/header";
import { Footer } from "@/components/footer";

export const metadata: Metadata = {
  title: "GauntletCI vs. NDepend: Diff Analysis vs. Metric-Based Architecture Review",
  description:
    "Compare GauntletCI and NDepend. GauntletCI catches behavioral regressions in the diff before commit. NDepend measures code quality metrics and enforces architecture rules across the full assembly.",
  alternates: { canonical: "/compare/gauntletci-vs-ndepend" },
  openGraph: { images: [{ url: "/og/compare-sonarqube.png", width: 1200, height: 630 }] },
};

const tableRows = [
  {
    scenario: "Enforcing layered architecture and forbidden dependency rules",
    winner: "ndepend",
    note: "NDepend's dependency matrix and CQLinq rules are purpose-built for architecture governance.",
  },
  {
    scenario: "Catching behavioral regressions in a pull request diff",
    winner: "gauntlet",
    note: "GauntletCI analyzes only the changed lines for removed guards, broken APIs, and altered patterns.",
  },
  {
    scenario: "Pre-commit feedback before a push",
    winner: "gauntlet",
    note: "Sub-second local diff analysis. NDepend runs a full assembly scan post-build.",
  },
  {
    scenario: "Tracking code quality metrics and technical debt over time",
    winner: "ndepend",
    note: "NDepend trend charts, debt estimates, and rule dashboards are designed for this.",
  },
  {
    scenario: "Air-gapped or strict data-residency environments",
    winner: "gauntlet",
    note: "Both run locally, but GauntletCI requires no license server or activation.",
  },
  {
    scenario: "Zero-cost adoption with full rule set",
    winner: "gauntlet",
    note: "GauntletCI is free. NDepend is a commercial license starting at several hundred dollars per developer.",
  },
  {
    scenario: "Custom metric-based rules using query language",
    winner: "ndepend",
    note: "CQLinq lets teams write LINQ queries over the codebase graph for custom quality gates.",
  },
  {
    scenario: "Optional offline AI explanations for findings",
    winner: "gauntlet",
    note: "Built-in ONNX engine. No API key or cloud call needed.",
  },
];

const featureRows = [
  { label: "Diff-scoped analysis (changed lines only)", gauntlet: "yes" as const, ndepend: "no" as const },
  { label: "100% local execution, no code upload", gauntlet: "yes" as const, ndepend: "yes" as const },
  { label: "Pre-commit (before push) feedback", gauntlet: "yes" as const, ndepend: "no" as const },
  { label: "Air-gap / data residency friendly", gauntlet: "yes" as const, ndepend: "yes" as const },
  { label: "Free tier with full rule set", gauntlet: "yes" as const, ndepend: "no" as const },
  { label: "C#/.NET behavioral rule set", gauntlet: "yes" as const, ndepend: "partial" as const },
  { label: "Architecture policy enforcement", gauntlet: "yes" as const, ndepend: "yes" as const },
  { label: "Metric-based quality gates (cyclomatic complexity, coupling, etc.)", gauntlet: "no" as const, ndepend: "yes" as const },
  { label: "Technical debt time estimates", gauntlet: "no" as const, ndepend: "yes" as const },
  { label: "GitHub inline PR comments", gauntlet: "yes" as const, ndepend: "partial" as const },
  { label: "Baseline delta mode (suppress existing findings)", gauntlet: "yes" as const, ndepend: "partial" as const },
  { label: "Local LLM enrichment (no API key)", gauntlet: "yes" as const, ndepend: "no" as const },
  { label: "MCP server (AI assistant integration)", gauntlet: "yes" as const, ndepend: "no" as const },
  { label: "Custom rules via code (no GUI or query language required)", gauntlet: "yes" as const, ndepend: "yes" as const },
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
  "headline": "GauntletCI vs. NDepend: Diff Analysis vs. Metric-Based Architecture Review",
  "description": "Compare GauntletCI and NDepend. GauntletCI catches behavioral regressions in the diff before commit. NDepend measures code quality metrics and enforces architecture rules across the full assembly.",
  "url": "https://gauntletci.com/compare/gauntletci-vs-ndepend",
  "publisher": { "@type": "Organization", "name": "GauntletCI", "url": "https://gauntletci.com" },
};

export default function VsNDependPage() {
  return (
    <>
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLd) }} />
      <Header />
      <div className="min-h-screen bg-background text-foreground">

        {/* Hero */}
        <section className="border-b border-border py-16 sm:py-20">
          <div className="mx-auto max-w-4xl px-4 sm:px-6 lg:px-8 text-center">
            <p className="text-sm font-semibold text-cyan-400 uppercase tracking-widest mb-3">
              Tool Comparison
            </p>
            <h1 className="text-4xl sm:text-5xl font-bold tracking-tight mb-5">
              GauntletCI vs. NDepend
            </h1>
            <p className="text-lg text-muted-foreground max-w-2xl mx-auto">
              NDepend analyzes your full .NET assembly to enforce architecture rules and track
              quality metrics over time. GauntletCI analyzes only the lines that changed in a
              pull request to catch behavioral regressions before they merge. They address
              different points in the development process.
            </p>
          </div>
        </section>

        <div className="mx-auto max-w-4xl px-4 sm:px-6 lg:px-8 py-16 space-y-20">

          {/* At a glance */}
          <div className="overflow-x-auto rounded-xl border border-border">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-border bg-card/80">
                  <th className="text-left px-5 py-3 font-semibold text-muted-foreground w-1/4">Tool</th>
                  <th className="text-left px-5 py-3 font-semibold text-muted-foreground">What it checks</th>
                  <th className="text-left px-5 py-3 font-semibold text-muted-foreground">What it misses</th>
                </tr>
              </thead>
              <tbody>
                <tr className="border-b border-border bg-card/20">
                  <td className="px-5 py-3 text-foreground/80 font-semibold">NDepend</td>
                  <td className="px-5 py-3 text-muted-foreground">Architecture rules, code quality metrics, technical debt tracking</td>
                  <td className="px-5 py-3 text-amber-400/80">Behavioral change risk, diff-scoped pre-commit detection</td>
                </tr>
                <tr className="bg-cyan-500/5">
                  <td className="px-5 py-3 text-cyan-400 font-semibold">GauntletCI</td>
                  <td className="px-5 py-3 text-muted-foreground">Change safety, Behavioral Change Risk in the diff</td>
                  <td className="px-5 py-3 text-muted-foreground/40">--</td>
                </tr>
              </tbody>
            </table>
          </div>

          {/* What NDepend does best */}
          <section>
            <h2 className="text-2xl font-semibold mb-4">What NDepend does best</h2>
            <p className="text-muted-foreground mb-4">
              NDepend is a mature .NET-specific analysis tool focused on architecture governance
              and code quality metrics. Its dependency matrix and CQLinq query language give teams
              precise control over layered architecture rules, coupling thresholds, and technical
              debt tracking across the full assembled codebase.
            </p>
            <ul className="space-y-2 text-sm text-muted-foreground">
              {[
                "Dependency matrix for visualizing and enforcing layered architecture",
                "CQLinq - a LINQ-based query language for writing custom metric-based rules",
                "Technical debt time estimates with trend charts across builds",
                "Cyclomatic complexity, coupling, cohesion, and other structural metrics",
                "Runs 100% locally - no code upload, no SaaS dependency",
                "Deep Visual Studio and CI/CD integration for .NET teams",
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
              GauntletCI does not replace NDepend. It solves a narrower, earlier problem: catching
              the behavioral regressions and breaking API changes that live in the diff itself,
              before the code is pushed. It does not scan assemblies or track metrics over time.
            </p>
            <div className="grid sm:grid-cols-3 gap-4">
              {[
                {
                  title: "Diff-only analysis",
                  body: "GauntletCI only looks at the lines that changed. It never compiles or scans assemblies. This keeps it fast enough to run pre-commit and precise enough to avoid noise from pre-existing issues.",
                },
                {
                  title: "Behavioral risk focus",
                  body: "Rules target behavioral drift: removed null guards, altered exception handling, changed concurrency patterns, dropped public API members. These compile fine but break production behavior.",
                },
                {
                  title: "Free, no license server",
                  body: "GauntletCI is open-source and free. No per-developer license, no activation server, no renewal cycle. Install with a single dotnet tool install command.",
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
                <div className="px-3 py-3 text-center text-sm font-semibold text-foreground/70">NDepend</div>
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
                    <Cell value={row.ndepend} />
                  </div>
                </div>
              ))}
            </div>
            <div className="mt-4 flex flex-wrap gap-6 text-sm text-muted-foreground">
              <span className="flex items-center gap-2"><Check className="h-4 w-4 text-cyan-400" /> Yes</span>
              <span className="flex items-center gap-2"><AlertCircle className="h-4 w-4 text-amber-400/60" /> Partial / requires configuration</span>
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
                      {row.winner === "gauntlet" ? "GauntletCI" : "NDepend"}
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
              NDepend answers: "What is the state of the codebase and how has it trended?"
              GauntletCI answers: "Did this specific change introduce a behavioral risk?"
            </p>
            <p className="text-muted-foreground mb-4">
              The distinction matters at the moment of commit. A developer who just added a
              CancellationToken parameter to a public interface method does not need a full
              dependency matrix report - they need to know that the change breaks all existing
              callers. GauntletCI surfaces that finding in under a second, before the push.
            </p>
            <p className="text-muted-foreground">
              NDepend's strength is longitudinal: tracking how architectural health evolves across
              dozens of builds and hundreds of contributors. GauntletCI's strength is surgical:
              catching the single regression in the 20 lines you just wrote.
            </p>
          </section>

          {/* Using both */}
          <section className="rounded-lg border border-cyan-500/30 bg-cyan-500/5 p-6">
            <h2 className="text-xl font-semibold mb-3">Using GauntletCI and NDepend together</h2>
            <p className="text-sm text-muted-foreground mb-3">
              The tools are complementary. NDepend enforces architecture rules and tracks metric
              baselines across the full build. GauntletCI enforces behavioral integrity on every
              diff before it is pushed.
            </p>
            <p className="text-sm text-muted-foreground">
              A common setup: GauntletCI runs as a pre-commit hook to catch breaking changes and
              removed error handling in the diff. NDepend runs in CI to block merges that violate
              coupling thresholds or introduce architecture violations in the assembled output.
              Together they provide coverage at both the change level and the codebase level.
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
      <Footer />
    </>
  );
}

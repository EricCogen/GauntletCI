import type { Metadata } from "next";
import Link from "next/link";
import { softwareApplicationSchema, buildFaqSchema } from "@/lib/schemas";

export const metadata: Metadata = {
  title: "Getting Started | GauntletCI Docs",
  description: "Install GauntletCI and run your first diff analysis in under two minutes.",
  alternates: { canonical: "/docs" },
  openGraph: { images: [{ url: '/og/docs.png', width: 1200, height: 630 }] },
};

const jsonLd = {
  "@context": "https://schema.org",
  "@type": "TechArticle",
  "headline": "Getting Started with GauntletCI",
  "description": "Install GauntletCI and run your first diff analysis in under two minutes.",
  "url": "https://gauntletci.com/docs",
  "publisher": { "@type": "Organization", "name": "GauntletCI", "url": "https://gauntletci.com" },
};

const faqSchema = buildFaqSchema([
  {
    q: "How do I install GauntletCI?",
    a: "Install GauntletCI as a .NET global tool: dotnet tool install -g GauntletCI. Requires .NET 8 or later.",
  },
  {
    q: "How do I run my first analysis?",
    a: "Run gauntletci analyze --staged to analyze staged changes before committing. You can also pipe a diff from stdin with git diff HEAD | gauntletci analyze, or point at a saved diff file with gauntletci analyze --diff changes.diff.",
  },
  {
    q: "How do I install GauntletCI as a pre-commit hook?",
    a: "Run gauntletci init inside your repository. The hook runs gauntletci analyze --staged automatically before every commit and blocks the commit with exit code 1 if blocking findings are detected.",
  },
  {
    q: "Does GauntletCI require a cloud connection?",
    a: "No. GauntletCI runs entirely on your local machine. All analysis is local and deterministic. No code or diff content is sent to any external service.",
  },
  {
    q: "What does GauntletCI analyze?",
    a: "GauntletCI reads the exact lines added and removed in your diff and evaluates them against 30+ deterministic rules. It flags behavior changes without test updates, breaking API changes, new exception paths, removed null guards, and hardcoded secrets.",
  },
]);

export default function DocsPage() {
  return (
    <>
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLd) }} />
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(softwareApplicationSchema) }} />
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(faqSchema) }} />
      <div className="space-y-10">
      <div>
        <p className="text-sm font-semibold text-cyan-400 uppercase tracking-widest mb-2">Documentation</p>
        <h1 className="text-4xl font-bold tracking-tight mb-4">Getting Started</h1>
        <p className="text-lg text-muted-foreground">
          GauntletCI is a local-first change risk engine for C# and .NET. It analyzes pull request diffs to catch
          breaking changes and regressions before they merge, with no cloud connection required.
        </p>
      </div>

      <section>
        <h2 className="text-2xl font-semibold mb-4">Install</h2>

        <p className="text-sm font-semibold mb-2">.NET global tool (all platforms)</p>
        <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm mb-4">
          <span className="text-cyan-400">$</span>{" "}
          <span className="text-foreground">dotnet tool install -g GauntletCI</span>
        </div>
        <p className="text-sm text-muted-foreground mb-5">Requires .NET 8 or later. Updates with <code className="bg-muted px-1 rounded text-xs">dotnet tool update -g GauntletCI</code>.</p>

        <p className="text-sm font-semibold mb-2">Windows (winget)</p>
        <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm mb-4">
          <span className="text-cyan-400">PS&gt;</span>{" "}
          <span className="text-foreground">winget install EricCogen.GauntletCI</span>
        </div>

        <p className="text-sm font-semibold mb-2">macOS / Linux (Homebrew)</p>
        <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm space-y-1 mb-4">
          <p><span className="text-cyan-400">$</span> <span className="text-foreground">brew tap EricCogen/gauntletci</span></p>
          <p><span className="text-cyan-400">$</span> <span className="text-foreground">brew install gauntletci</span></p>
        </div>

        <p className="text-sm font-semibold mb-2">Manual (self-contained binary)</p>
        <p className="text-sm text-muted-foreground mb-3">
          Download a self-contained binary from the{" "}
          <a href="https://github.com/EricCogen/GauntletCI/releases/latest" className="text-cyan-400 hover:underline" target="_blank" rel="noopener noreferrer">latest GitHub release</a>.
          No .NET installation required. Available for win-x64, win-arm64, osx-x64, osx-arm64, linux-x64, and linux-arm64.
        </p>
      </section>

      <section>
        <h2 className="text-2xl font-semibold mb-4">Run your first analysis</h2>
        <div className="space-y-3">
          <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm space-y-1">
            <p><span className="text-cyan-400">$</span> <span className="text-foreground">gauntletci analyze --staged</span></p>
            <p className="text-muted-foreground pl-4"># Analyze your staged changes before committing</p>
            <p className="mt-2"><span className="text-cyan-400">$</span> <span className="text-foreground">gauntletci analyze --diff pr.diff</span></p>
            <p className="text-muted-foreground pl-4"># Analyze a saved diff file</p>
            <p className="mt-2"><span className="text-cyan-400">$</span> <span className="text-foreground">git diff HEAD | gauntletci analyze</span></p>
            <p className="text-muted-foreground pl-4"># Pipe a diff from stdin</p>
          </div>
        </div>
      </section>

      <section>
        <h2 className="text-2xl font-semibold mb-4">Install as a pre-commit hook</h2>
        <p className="text-muted-foreground mb-3">
          Run this once inside your repository. GauntletCI will analyze your staged diff automatically before every commit.
        </p>
        <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm">
          <span className="text-cyan-400">$</span>{" "}
          <span className="text-foreground">gauntletci init</span>
        </div>
        <p className="mt-3 text-sm text-muted-foreground">
          The hook runs <code className="bg-muted px-1 rounded text-xs">gauntletci analyze --staged</code> and exits with code 1 if findings are detected, blocking the commit.
        </p>
      </section>

      <section>
        <h2 className="text-2xl font-semibold mb-4">What it analyzes</h2>
        <p className="text-muted-foreground mb-4">
          GauntletCI reads the exact lines added and removed in your diff and evaluates them against 30+ deterministic rules. It flags:
        </p>
        <ul className="space-y-2 text-sm text-muted-foreground list-none">
          {[
            "Behavior changes without corresponding test updates",
            "Breaking public API or method signature changes",
            "New exception paths with no callers prepared to handle them",
            "Removed null guards or defensive checks",
            "Implicit dependency behavior shifts",
            "Hardcoded secrets and SQL injection risks",
          ].map((item) => (
            <li key={item} className="flex items-start gap-2">
              <span className="text-cyan-400 mt-0.5">+</span>
              <span>{item}</span>
            </li>
          ))}
        </ul>
      </section>

      <section>
        <h2 className="text-2xl font-semibold mb-4">What it is not</h2>
        <p className="text-muted-foreground mb-3">
          GauntletCI is not a linter, formatter, test runner, or full-codebase static analysis replacement.
          It focuses on one question: did this diff introduce behavior that is no longer properly validated?
        </p>
        <p className="text-muted-foreground">
          It runs alongside your existing tools; it does not replace them.
        </p>
      </section>

      <section>
        <h2 className="text-2xl font-semibold mb-4">Next steps</h2>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          {[
            { href: "/docs/cli-reference", label: "CLI Reference", desc: "All commands and flags" },
            { href: "/docs/rules", label: "Rule Library", desc: "All detection rules" },
            { href: "/docs/configuration", label: "Configuration", desc: ".gauntletci.json reference" },
            { href: "/docs/integrations", label: "CI/CD Integrations", desc: "GitHub Actions and more" },
            { href: "/pricing", label: "Pricing", desc: "Free during beta - see licensing details" },
          ].map((link) => (
            <Link
              key={link.href}
              href={link.href}
              className="rounded-lg border border-border bg-card p-4 hover:border-cyan-500/50 transition-colors block"
            >
              <p className="font-medium text-sm">{link.label}</p>
              <p className="text-xs text-muted-foreground mt-1">{link.desc}</p>
            </Link>
          ))}
        </div>
      </section>
    </div>
    </>
  );
}

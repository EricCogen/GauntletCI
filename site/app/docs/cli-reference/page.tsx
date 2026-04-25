import type { Metadata } from "next";
import Link from "next/link";
import { softwareApplicationSchema, buildFaqSchema } from "@/lib/schemas";

export const metadata: Metadata = {
  title: "CLI Reference | GauntletCI Docs",
  description: "Complete reference for all GauntletCI CLI commands and options.",
  alternates: { canonical: "/docs/cli-reference" },
};

const analyzeFlags = [
  { flag: "--diff <path>", type: "file", desc: "Path to a .diff file" },
  { flag: "--commit <sha>", type: "string", desc: "Commit SHA to analyze" },
  { flag: "--staged", type: "bool", desc: "Analyze staged changes (git diff --cached)" },
  { flag: "--unstaged", type: "bool", desc: "Analyze unstaged changes (git diff)" },
  { flag: "--all-changes", type: "bool", desc: "Analyze all local changes: staged and unstaged" },
  { flag: "--repo <path>", type: "directory", desc: "Repository root for config loading and git operations. Defaults to CWD." },
  { flag: "--output <format>", type: "string", desc: "Output format: text (default) or json" },
  { flag: "--with-llm", type: "bool", desc: "Enable local LLM enrichment for High-confidence findings" },
  { flag: "--github-annotations", type: "bool", desc: "Emit GitHub Actions workflow commands for inline PR annotations" },
  { flag: "--no-banner", type: "bool", desc: "Suppress the ASCII banner" },
];

const exitCodes = [
  { code: "0", meaning: "Success; no findings detected" },
  { code: "1", meaning: "Findings detected, or invalid input" },
  { code: "2", meaning: "Unhandled error or exception" },
];

const jsonLd = {
  "@context": "https://schema.org",
  "@type": "TechArticle",
  "headline": "GauntletCI CLI Reference",
  "description": "Complete reference for all GauntletCI CLI commands and options.",
  "url": "https://gauntletci.com/docs/cli-reference",
  "publisher": { "@type": "Organization", "name": "GauntletCI", "url": "https://gauntletci.com" },
};

const faqSchema = buildFaqSchema([
  {
    q: "How do I analyze staged changes with GauntletCI?",
    a: "Run gauntletci analyze --staged to analyze your staged changes. The tool exits with code 1 if blocking findings are detected, which will block the commit if used as a pre-commit hook.",
  },
  {
    q: "What output formats does GauntletCI support?",
    a: "GauntletCI supports text (default, human-readable terminal output) and json (machine-readable) output formats, controlled with the --output flag. Use --output json to pipe findings to scripts, dashboards, or custom integrations.",
  },
  {
    q: "What exit codes does GauntletCI return?",
    a: "Exit code 0 means no findings were detected. Exit code 1 means findings were detected or input was invalid. Exit code 2 means an unhandled error occurred.",
  },
  {
    q: "How do I emit inline GitHub Actions annotations?",
    a: "Pass the --github-annotations flag to gauntletci analyze. This emits GitHub Actions workflow commands that create inline diff comments on the exact lines where findings occur.",
  },
  {
    q: "How do I suppress the GauntletCI banner?",
    a: "Pass --no-banner to any command, or set the GAUNTLETCI_NO_BANNER environment variable to any non-empty value. The banner is automatically suppressed when CI, GITHUB_ACTIONS, or TF_BUILD environment variables are set.",
  },
]);

export default function CliReferencePage() {
  return (
    <>
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLd) }} />
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(softwareApplicationSchema) }} />
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(faqSchema) }} />
      <div className="space-y-10">
      <div>
        <p className="text-sm font-semibold text-cyan-400 uppercase tracking-widest mb-2">CLI Reference</p>
        <h1 className="text-4xl font-bold tracking-tight mb-4">Command Reference</h1>
        <p className="text-lg text-muted-foreground">
          All commands, flags, and exit codes for the <code className="bg-muted px-1 rounded text-sm">gauntletci</code> CLI tool.
        </p>
      </div>

      <section>
        <h2 className="text-2xl font-semibold mb-4">Exit codes</h2>
        <div className="rounded-lg border border-border overflow-hidden">
          <table className="w-full text-sm">
            <thead className="bg-muted/50">
              <tr>
                <th className="text-left px-4 py-2 font-medium text-muted-foreground w-16">Code</th>
                <th className="text-left px-4 py-2 font-medium text-muted-foreground">Meaning</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border">
              {exitCodes.map((row) => (
                <tr key={row.code}>
                  <td className="px-4 py-2 font-mono text-cyan-400">{row.code}</td>
                  <td className="px-4 py-2 text-muted-foreground">{row.meaning}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>

      <section>
        <h2 className="text-2xl font-semibold mb-2">gauntletci analyze</h2>
        <p className="text-muted-foreground mb-4">
          Analyze a git diff for change-risk. Runs all enabled rules and reports findings.
          Exactly one diff source should be specified; if none is provided, diff content is read from stdin.
        </p>
        <div className="rounded-lg border border-border overflow-hidden mb-6">
          <table className="w-full text-sm">
            <thead className="bg-muted/50">
              <tr>
                <th className="text-left px-4 py-2 font-medium text-muted-foreground">Flag</th>
                <th className="text-left px-4 py-2 font-medium text-muted-foreground hidden sm:table-cell">Type</th>
                <th className="text-left px-4 py-2 font-medium text-muted-foreground">Description</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border">
              {analyzeFlags.map((row) => (
                <tr key={row.flag}>
                  <td className="px-4 py-2 font-mono text-xs text-cyan-400 whitespace-nowrap">{row.flag}</td>
                  <td className="px-4 py-2 text-xs text-muted-foreground hidden sm:table-cell">{row.type}</td>
                  <td className="px-4 py-2 text-sm text-muted-foreground">{row.desc}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        <h3 className="text-lg font-semibold mb-3">Examples</h3>
        <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm space-y-2">
          {[
            ["# Analyze staged changes before a commit", "gauntletci analyze --staged"],
            ["# Analyze all local changes", "gauntletci analyze --all-changes"],
            ["# Analyze a specific commit", "gauntletci analyze --commit abc1234"],
            ["# Analyze from a saved diff file", "gauntletci analyze --diff changes.diff"],
            ["# Pipe from stdin", "git diff HEAD | gauntletci analyze"],
            ["# Output JSON for downstream tooling", "gauntletci analyze --staged --output json"],
            ["# Enable local LLM enrichment", "gauntletci analyze --staged --with-llm"],
            ["# Emit GitHub Actions inline annotations", "gauntletci analyze --staged --github-annotations"],
          ].map(([comment, cmd]) => (
            <div key={cmd}>
              <p className="text-muted-foreground">{comment}</p>
              <p><span className="text-cyan-400">$</span> <span className="text-foreground">{cmd}</span></p>
            </div>
          ))}
        </div>
      </section>

      <section>
        <h2 className="text-2xl font-semibold mb-2">gauntletci init</h2>
        <p className="text-muted-foreground mb-3">
          Install a pre-commit hook in the current repository. The hook runs{" "}
          <code className="bg-muted px-1 rounded text-xs">gauntletci analyze --staged</code> before every commit
          and blocks the commit if findings are detected.
        </p>
        <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm">
          <span className="text-cyan-400">$</span>{" "}
          <span className="text-foreground">gauntletci init</span>
        </div>
      </section>

      <section>
        <h2 className="text-2xl font-semibold mb-2">gauntletci baseline</h2>
        <p className="text-muted-foreground mb-3">
          Manage finding baselines. Capture a snapshot of current findings to suppress them from future runs;
          only net-new risks introduced after the baseline will be reported.
        </p>
        <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm space-y-2">
          <p><span className="text-cyan-400">$</span> <span className="text-foreground">gauntletci baseline capture --staged</span></p>
          <p className="text-muted-foreground pl-4"># Save current findings to .gauntletci-baseline.json</p>
        </div>
      </section>

      <section>
        <h2 className="text-2xl font-semibold mb-2">gauntletci doctor</h2>
        <p className="text-muted-foreground mb-3">
          Validate your installation and configuration. Checks that git, the .NET runtime, and
          optional dependencies like Ollama are correctly configured.
        </p>
        <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm">
          <span className="text-cyan-400">$</span>{" "}
          <span className="text-foreground">gauntletci doctor</span>
        </div>
      </section>

      <section>
        <h2 className="text-2xl font-semibold mb-4">Environment variables</h2>
        <div className="rounded-lg border border-border overflow-hidden">
          <table className="w-full text-sm">
            <thead className="bg-muted/50">
              <tr>
                <th className="text-left px-4 py-2 font-medium text-muted-foreground">Variable</th>
                <th className="text-left px-4 py-2 font-medium text-muted-foreground">Description</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border">
              {[
                { name: "GITHUB_TOKEN", desc: "Personal access token for GitHub API. Required for corpus commands on private repos." },
                { name: "GAUNTLETCI_NO_BANNER", desc: "Set to any non-empty value to suppress the ASCII banner." },
                { name: "CI", desc: "Suppresses banner and telemetry prompt automatically." },
                { name: "GITHUB_ACTIONS", desc: "Suppresses banner and telemetry prompt (set automatically by GitHub Actions runners)." },
                { name: "TF_BUILD", desc: "Suppresses banner and telemetry prompt (Azure Pipelines)." },
              ].map((row) => (
                <tr key={row.name}>
                  <td className="px-4 py-2 font-mono text-xs text-cyan-400 whitespace-nowrap">{row.name}</td>
                  <td className="px-4 py-2 text-sm text-muted-foreground">{row.desc}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>

      <section>
        <h2 className="text-2xl font-semibold mb-4">Next steps</h2>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          {[
            { href: "/docs/configuration", label: "Configuration", desc: "Customize rules and severity in .gauntletci.json" },
            { href: "/docs/integrations", label: "CI/CD Integrations", desc: "Use GauntletCI in GitHub Actions and Azure Pipelines" },
            { href: "/docs/local-llm", label: "Local LLM Setup", desc: "Enable offline AI-powered finding explanations" },
            { href: "/docs/rules", label: "Rule Library", desc: "Browse all 30+ detection rules" },
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

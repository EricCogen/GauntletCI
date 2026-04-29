import type { Metadata } from "next";
import Link from "next/link";
import { buildFaqSchema, softwareApplicationSchema } from "@/lib/schemas";

export const metadata: Metadata = {
  title: "Pre-commit Hooks | GauntletCI Docs",
  description:
    "Wire GauntletCI into your pre-commit workflow using husky, dotnet-husky, or Lefthook. Block commits with high-risk .NET changes before they reach CI.",
  alternates: { canonical: "/docs/integrations/pre-commit" },
};

const jsonLd = {
  "@context": "https://schema.org",
  "@type": "TechArticle",
  headline: "GauntletCI Pre-commit Hook Setup",
  description: "Wire GauntletCI into pre-commit workflows using husky, dotnet-husky, or Lefthook.",
  url: "https://gauntletci.com/docs/integrations/pre-commit",
  publisher: { "@type": "Organization", name: "GauntletCI", url: "https://gauntletci.com" },
};

const faqSchema = buildFaqSchema([
  {
    q: "Which pre-commit tool should I use with GauntletCI?",
    a: "For pure .NET repos, use dotnet-husky or Lefthook - both have no Node.js dependency. For full-stack or Node.js repos that already use husky, add GauntletCI to the existing hook. Lefthook is the fastest option and supports parallel hook execution.",
  },
  {
    q: "How does GauntletCI block a commit from a pre-commit hook?",
    a: "GauntletCI exits with code 1 when Block-severity findings are detected. All git hook managers (husky, dotnet-husky, Lefthook) abort the commit when the hook exits non-zero.",
  },
  {
    q: "How do I skip the GauntletCI hook for a single commit?",
    a: "With husky: HUSKY=0 git commit. With dotnet-husky: HUSKY=0 git commit or git commit --no-verify. With Lefthook: LEFTHOOK=0 git commit or git commit --no-verify.",
  },
  {
    q: "Can I use GauntletCI as a pre-commit hook without husky or Lefthook?",
    a: "Yes. Run gauntletci init inside your repository. This installs a .git/hooks/pre-commit script directly, with no dependency on any hook manager.",
  },
]);

export default function PreCommitPage() {
  return (
    <>
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLd) }} />
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(softwareApplicationSchema) }} />
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(faqSchema) }} />
      <div className="space-y-10">
        <div>
          <p className="text-sm font-semibold text-cyan-400 uppercase tracking-widest mb-2">Pre-commit</p>
          <h1 className="text-4xl font-bold tracking-tight mb-4">Pre-commit Hooks</h1>
          <p className="text-lg text-muted-foreground">
            Run GauntletCI before every commit to catch risky changes before they reach CI.
            Choose the hook manager that fits your toolchain - or skip them all with{" "}
            <code className="bg-muted px-1 rounded text-xs">gauntletci init</code>.
          </p>
        </div>

        <section>
          <h2 className="text-2xl font-semibold mb-3">No hook manager required</h2>
          <p className="text-muted-foreground mb-3">
            The fastest setup. Run this once inside your repository to install a native git hook:
          </p>
          <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm space-y-1 mb-3">
            <p><span className="text-cyan-400">$</span> <span className="text-foreground">cd your-repo</span></p>
            <p><span className="text-cyan-400">$</span> <span className="text-foreground">gauntletci init</span></p>
          </div>
          <p className="text-sm text-muted-foreground">
            This writes a <code className="bg-muted px-1 rounded text-xs">.git/hooks/pre-commit</code> script that runs
            <code className="bg-muted px-1 rounded text-xs"> gauntletci analyze --staged</code> before every commit.
            No Node.js or Gradle required.
          </p>
        </section>

        <section>
          <h2 className="text-2xl font-semibold mb-4">dotnet-husky</h2>
          <p className="text-muted-foreground mb-3">
            <a href="https://alirezanet.github.io/Husky.Net/" className="text-cyan-400 hover:underline" target="_blank" rel="noopener noreferrer">dotnet-husky</a>{" "}
            is a .NET-native hook manager with no Node.js dependency. Best for pure .NET repositories.
          </p>

          <p className="text-sm font-semibold mb-2">1. Install dotnet-husky</p>
          <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm mb-4">
            <p><span className="text-cyan-400">$</span> <span className="text-foreground">dotnet tool install -g Husky</span></p>
            <p><span className="text-cyan-400">$</span> <span className="text-foreground">dotnet husky install</span></p>
          </div>

          <p className="text-sm font-semibold mb-2">2. Create <code className="bg-muted px-1 rounded text-xs">.husky/task-runner.json</code></p>
          <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm overflow-x-auto mb-4">
            <pre className="text-foreground whitespace-pre">{`{
  "tasks": [
    {
      "name": "GauntletCI",
      "command": "gauntletci",
      "args": ["analyze", "--sensitivity", "balanced"],
      "pathFilter": ["**/*.cs", "**/*.csproj", "**/*.sln", "**/*.slnx"],
      "output": "always"
    }
  ]
}`}</pre>
          </div>

          <p className="text-sm font-semibold mb-2">3. Create <code className="bg-muted px-1 rounded text-xs">.husky/pre-commit</code></p>
          <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm overflow-x-auto mb-4">
            <pre className="text-foreground whitespace-pre">{`#!/bin/sh
. "$(dirname "$0")/_/husky.sh"
dotnet husky run --name GauntletCI`}</pre>
          </div>

          <p className="text-sm font-semibold mb-2">4. Automate for the team</p>
          <p className="text-sm text-muted-foreground mb-3">
            Add to your CI setup step so every developer gets the hook after cloning:
          </p>
          <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm overflow-x-auto">
            <pre className="text-foreground whitespace-pre">{`- run: dotnet tool restore
- run: dotnet husky install`}</pre>
          </div>
        </section>

        <section>
          <h2 className="text-2xl font-semibold mb-4">husky</h2>
          <p className="text-muted-foreground mb-3">
            <a href="https://typicode.github.io/husky/" className="text-cyan-400 hover:underline" target="_blank" rel="noopener noreferrer">husky</a>{" "}
            is the most popular git hooks manager for JavaScript and TypeScript projects. Use it
            for monorepos or full-stack apps that already have a Node.js toolchain.
          </p>

          <p className="text-sm font-semibold mb-2">1. Install and initialize</p>
          <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm mb-4 space-y-1">
            <p><span className="text-cyan-400">$</span> <span className="text-foreground">npm install --save-dev husky</span></p>
            <p><span className="text-cyan-400">$</span> <span className="text-foreground">npx husky init</span></p>
          </div>

          <p className="text-sm font-semibold mb-2">2. Replace <code className="bg-muted px-1 rounded text-xs">.husky/pre-commit</code></p>
          <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm overflow-x-auto mb-4">
            <pre className="text-foreground whitespace-pre">{`#!/bin/sh
SENSITIVITY="\${GAUNTLETCI_SENSITIVITY:-balanced}"
gauntletci analyze --sensitivity "$SENSITIVITY"`}</pre>
          </div>

          <p className="text-sm font-semibold mb-2">3. Share with the team via <code className="bg-muted px-1 rounded text-xs">package.json</code></p>
          <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm overflow-x-auto mb-4">
            <pre className="text-foreground whitespace-pre">{`{
  "scripts": {
    "prepare": "husky"
  }
}`}</pre>
          </div>

          <p className="text-sm font-semibold mb-2">Skip the hook for one commit</p>
          <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm">
            <span className="text-cyan-400">$</span>{" "}
            <span className="text-foreground">HUSKY=0 git commit -m "..."</span>
          </div>
        </section>

        <section>
          <h2 className="text-2xl font-semibold mb-4">Lefthook</h2>
          <p className="text-muted-foreground mb-3">
            <a href="https://github.com/evilmartians/lefthook" className="text-cyan-400 hover:underline" target="_blank" rel="noopener noreferrer">Lefthook</a>{" "}
            is a fast, cross-platform hook manager with no Node.js dependency and native support
            for parallel hook execution. Ideal for pure .NET repositories.
          </p>

          <p className="text-sm font-semibold mb-2">Install</p>
          <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm mb-4 space-y-1">
            <p><span className="text-cyan-400 text-xs"># macOS/Linux</span></p>
            <p><span className="text-cyan-400">$</span> <span className="text-foreground">brew install lefthook</span></p>
            <p className="mt-1"><span className="text-cyan-400 text-xs"># Windows</span></p>
            <p><span className="text-cyan-400">PS&gt;</span> <span className="text-foreground">scoop install lefthook</span></p>
            <p className="mt-1"><span className="text-cyan-400 text-xs"># npm</span></p>
            <p><span className="text-cyan-400">$</span> <span className="text-foreground">npm install --save-dev lefthook</span></p>
          </div>

          <p className="text-sm font-semibold mb-2">Configure <code className="bg-muted px-1 rounded text-xs">lefthook.yml</code></p>
          <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm overflow-x-auto mb-4">
            <pre className="text-foreground whitespace-pre">{`pre-commit:
  commands:
    gauntletci:
      run: gauntletci analyze --sensitivity balanced
      glob: "*.cs"`}</pre>
          </div>
          <p className="text-sm text-muted-foreground mb-4">
            The <code className="bg-muted px-1 rounded text-xs">glob</code> filter only triggers GauntletCI when
            <code className="bg-muted px-1 rounded text-xs"> .cs</code> files are staged.
          </p>

          <p className="text-sm font-semibold mb-2">Parallel hooks</p>
          <p className="text-muted-foreground text-sm mb-3">
            Run GauntletCI alongside formatters without blocking on each other:
          </p>
          <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm overflow-x-auto mb-4">
            <pre className="text-foreground whitespace-pre">{`pre-commit:
  parallel: true
  commands:
    gauntletci:
      run: gauntletci analyze --sensitivity balanced
      glob: "*.cs"
    dotnet-format:
      run: dotnet format --verify-no-changes
      glob: "*.cs"`}</pre>
          </div>

          <p className="text-sm font-semibold mb-2">Install hooks and verify</p>
          <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm space-y-1">
            <p><span className="text-cyan-400">$</span> <span className="text-foreground">lefthook install</span></p>
            <p><span className="text-cyan-400">$</span> <span className="text-foreground">lefthook run pre-commit</span></p>
          </div>
        </section>

        <section>
          <h2 className="text-2xl font-semibold mb-4">Choosing a tool</h2>
          <div className="rounded-lg border border-border overflow-hidden">
            <table className="w-full text-sm">
              <thead className="bg-muted/50">
                <tr>
                  <th className="text-left px-4 py-2 font-medium text-muted-foreground">Tool</th>
                  <th className="text-left px-4 py-2 font-medium text-muted-foreground">Requires</th>
                  <th className="text-left px-4 py-2 font-medium text-muted-foreground">Best for</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                <tr>
                  <td className="px-4 py-2 font-mono text-xs">gauntletci init</td>
                  <td className="px-4 py-2 text-sm text-muted-foreground">Nothing extra</td>
                  <td className="px-4 py-2 text-sm text-muted-foreground">Solo devs, quick setup</td>
                </tr>
                <tr>
                  <td className="px-4 py-2 font-mono text-xs">dotnet-husky</td>
                  <td className="px-4 py-2 text-sm text-muted-foreground">.NET SDK</td>
                  <td className="px-4 py-2 text-sm text-muted-foreground">Pure .NET teams, task runner</td>
                </tr>
                <tr>
                  <td className="px-4 py-2 font-mono text-xs">husky</td>
                  <td className="px-4 py-2 text-sm text-muted-foreground">Node.js</td>
                  <td className="px-4 py-2 text-sm text-muted-foreground">Monorepos already using npm</td>
                </tr>
                <tr>
                  <td className="px-4 py-2 font-mono text-xs">Lefthook</td>
                  <td className="px-4 py-2 text-sm text-muted-foreground">Nothing extra</td>
                  <td className="px-4 py-2 text-sm text-muted-foreground">Fast parallel hooks, any stack</td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>

        <section>
          <p className="text-sm text-muted-foreground">
            Also see:{" "}
            <Link href="/docs/integrations/github-action" className="text-cyan-400 hover:underline">
              GitHub Action
            </Link>{" "}
            and{" "}
            <Link href="/docs/integrations/azure-devops" className="text-cyan-400 hover:underline">
              Azure DevOps
            </Link>{" "}
            for CI/CD pipeline integration.
          </p>
        </section>
      </div>
    </>
  );
}

import type { Metadata } from "next";

export const metadata: Metadata = {
  title: "CI/CD Integrations | GauntletCI Docs",
  description: "Integrate GauntletCI with GitHub Actions, Azure Pipelines, and other CI/CD systems.",
  alternates: { canonical: "/docs/integrations" },
};

const jsonLd = {
  "@context": "https://schema.org",
  "@type": "TechArticle",
  "headline": "GauntletCI CI/CD Integrations",
  "description": "Integrate GauntletCI with GitHub Actions, Azure Pipelines, and other CI/CD systems.",
  "url": "https://gauntletci.com/docs/integrations",
  "publisher": { "@type": "Organization", "name": "GauntletCI", "url": "https://gauntletci.com" },
};

export default function IntegrationsPage() {
  return (
    <>
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLd) }} />
      <div className="space-y-10">
      <div>
        <p className="text-sm font-semibold text-cyan-400 uppercase tracking-widest mb-2">CI/CD Integrations</p>
        <h1 className="text-4xl font-bold tracking-tight mb-4">CI/CD Integrations</h1>
        <p className="text-lg text-muted-foreground">
          GauntletCI runs anywhere that can execute a .NET tool. The most common setup is as a
          pre-commit hook on developer machines, with an optional CI gate for pull request enforcement.
        </p>
      </div>

      <section>
        <h2 className="text-2xl font-semibold mb-3">GitHub Actions</h2>
        <p className="text-muted-foreground mb-4">
          Add this workflow to analyze every pull request. The <code className="bg-muted px-1 rounded text-xs">--github-annotations</code> flag
          emits inline comments directly on the diff lines where findings occur.
        </p>
        <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm overflow-x-auto">
          <pre className="text-foreground whitespace-pre">{`name: GauntletCI Analysis

on:
  pull_request:
    branches: [main]

jobs:
  analyze:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Install GauntletCI
        run: dotnet tool install -g GauntletCI

      - name: Analyze PR diff
        run: |
          git diff origin/\${{ github.base_ref }}...HEAD > pr.diff
          gauntletci analyze --diff pr.diff --github-annotations
        env:
          GITHUB_TOKEN: \${{ secrets.GITHUB_TOKEN }}`}</pre>
        </div>
        <p className="mt-3 text-sm text-muted-foreground">
          The workflow exits with code 1 if blocking findings are detected, failing the check and
          blocking the merge until findings are addressed or accepted.
        </p>
      </section>

      <section>
        <h2 className="text-2xl font-semibold mb-3">Azure Pipelines</h2>
        <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm overflow-x-auto">
          <pre className="text-foreground whitespace-pre">{`trigger: none
pr:
  branches:
    include: [main]

pool:
  vmImage: ubuntu-latest

steps:
  - task: UseDotNet@2
    inputs:
      version: '8.0.x'

  - script: dotnet tool install -g GauntletCI
    displayName: Install GauntletCI

  - script: |
      git diff origin/$(System.PullRequest.TargetBranch)...HEAD > pr.diff
      gauntletci analyze --diff pr.diff --no-banner
    displayName: Analyze PR diff`}</pre>
        </div>
      </section>

      <section>
        <h2 className="text-2xl font-semibold mb-3">Pre-commit hook (local)</h2>
        <p className="text-muted-foreground mb-3">
          The fastest setup. Runs automatically before every commit; no CI required.
        </p>
        <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm space-y-1">
          <p><span className="text-cyan-400">$</span> <span className="text-foreground">cd your-repo</span></p>
          <p><span className="text-cyan-400">$</span> <span className="text-foreground">gauntletci init</span></p>
        </div>
        <p className="mt-3 text-sm text-muted-foreground">
          This installs a <code className="bg-muted px-1 rounded text-xs">.git/hooks/pre-commit</code> script that
          runs <code className="bg-muted px-1 rounded text-xs">gauntletci analyze --staged</code> on every commit attempt.
          The commit is blocked if exit code 1 is returned.
        </p>
      </section>

      <section>
        <h2 className="text-2xl font-semibold mb-3">Exit code behavior</h2>
        <div className="rounded-lg border border-border overflow-hidden">
          <table className="w-full text-sm">
            <thead className="bg-muted/50">
              <tr>
                <th className="text-left px-4 py-2 font-medium text-muted-foreground">Exit code</th>
                <th className="text-left px-4 py-2 font-medium text-muted-foreground">Meaning</th>
                <th className="text-left px-4 py-2 font-medium text-muted-foreground">CI result</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border">
              <tr>
                <td className="px-4 py-2 font-mono text-green-400">0</td>
                <td className="px-4 py-2 text-sm text-muted-foreground">No findings</td>
                <td className="px-4 py-2 text-sm text-green-400">Pass</td>
              </tr>
              <tr>
                <td className="px-4 py-2 font-mono text-destructive">1</td>
                <td className="px-4 py-2 text-sm text-muted-foreground">Findings detected</td>
                <td className="px-4 py-2 text-sm text-destructive">Fail / block merge</td>
              </tr>
              <tr>
                <td className="px-4 py-2 font-mono text-yellow-400">2</td>
                <td className="px-4 py-2 text-sm text-muted-foreground">Unhandled error</td>
                <td className="px-4 py-2 text-sm text-yellow-400">Fail</td>
              </tr>
            </tbody>
          </table>
        </div>
        <p className="mt-3 text-sm text-muted-foreground">
          Control which severity triggers a failure via <code className="bg-muted px-1 rounded text-xs">exitOn</code> in
          your <code className="bg-muted px-1 rounded text-xs">.gauntletci.json</code>.
        </p>
      </section>

      <section>
        <h2 className="text-2xl font-semibold mb-3">JSON output for downstream tooling</h2>
        <p className="text-muted-foreground mb-4">
          Use <code className="bg-muted px-1 rounded text-xs">--output json</code> to consume findings in scripts,
          dashboards, or custom integrations.
        </p>

        <p className="text-sm font-semibold mb-2">With jq</p>
        <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm mb-5">
          <span className="text-cyan-400">$</span>{" "}
          <span className="text-foreground">gauntletci analyze --staged --output json | jq .findings</span>
        </div>

        <p className="text-sm font-semibold mb-2">With PowerShell (no jq required)</p>
        <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm mb-5">
          <span className="text-cyan-400">PS&gt;</span>{" "}
          <span className="text-foreground">gauntletci analyze --staged --output json | ConvertFrom-Json | Select-Object -ExpandProperty findings</span>
        </div>

        <p className="text-sm font-semibold mb-2">With Python (no jq required)</p>
        <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm mb-5">
          <p><span className="text-cyan-400">$</span>{" "}
          <span className="text-foreground">{`gauntletci analyze --staged --output json | python -c "import sys,json; data=json.load(sys.stdin); print(json.dumps(data['findings'], indent=2))"`}</span></p>
        </div>

        <p className="text-sm font-semibold mb-2">Save to file</p>
        <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm mb-3">
          <span className="text-cyan-400">$</span>{" "}
          <span className="text-foreground">gauntletci analyze --staged --output json &gt; report.json</span>
        </div>
        <p className="text-sm text-muted-foreground">
          Writing to a file works with any downstream tool: upload to S3, attach to a Slack
          notification, parse in a build script, or archive as a CI artifact.
        </p>
      </section>

      <section>
        <h2 className="text-2xl font-semibold mb-3">LLM enrichment in CI/CD</h2>
        <p className="text-muted-foreground mb-4">
          The built-in ONNX engine (Option 1 in the{" "}
          <a href="/docs/local-llm" className="text-cyan-400 hover:underline">Local LLM Setup docs</a>)
          is not available in CI. Loading a 2 GB model in an ephemeral runner is impractical.
          To use <code className="bg-muted px-1 rounded text-xs">--with-llm</code> in CI, configure
          a remote OpenAI-compatible endpoint:
        </p>
        <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm mb-4">
          <pre className="text-foreground whitespace-pre">{`# In your CI environment secrets
GAUNTLETCI_LLM_API_KEY=sk-...

# In .gauntletci.json
{
  "llm": {
    "ciEndpoint": "https://api.openai.com/v1",
    "ciModel": "gpt-4o-mini"
  }
}`}</pre>
        </div>
        <p className="text-sm text-muted-foreground">
          If <code className="bg-muted px-1 rounded text-xs">--with-llm</code> is passed in CI
          with no endpoint configured, GauntletCI prints a loud warning to stderr and skips
          enrichment. Detection findings are still reported normally.
        </p>
      </section>
    </div>
    </>
  );
}

import type { Metadata } from "next";
import Link from "next/link";
import { softwareApplicationSchema, buildFaqSchema } from "@/lib/schemas";

export const metadata: Metadata = {
  title: "CI/CD Integrations | GauntletCI Docs",
  description: "Integrate GauntletCI with GitHub Actions, GitLab CI, Azure Pipelines, Bitbucket Pipelines, VS Code, and other CI/CD systems. Install as a .NET global tool on any runner.",
  alternates: { canonical: "/docs/integrations" },
};

const jsonLd = {
  "@context": "https://schema.org",
  "@type": "TechArticle",
  "headline": "GauntletCI CI/CD Integrations",
  "description": "Integrate GauntletCI with GitHub Actions, GitLab CI, Azure Pipelines, Bitbucket Pipelines, and other CI/CD systems.",
  "url": "https://gauntletci.com/docs/integrations",
  "publisher": { "@type": "Organization", "name": "GauntletCI", "url": "https://gauntletci.com" },
};

const faqSchema = buildFaqSchema([
  {
    q: "How do I integrate GauntletCI with GitLab CI?",
    a: "Add a job with the mcr.microsoft.com/dotnet/sdk:8.0 image. Install the tool with dotnet tool install -g GauntletCI (then export PATH to include $HOME/.dotnet/tools), fetch the target branch, generate a diff with git diff origin/$CI_MERGE_REQUEST_TARGET_BRANCH_NAME...HEAD > pr.diff, and run gauntletci analyze --diff pr.diff --no-banner.",
  },
  {
    q: "How do I integrate GauntletCI with Bitbucket Pipelines?",
    a: "Use the mcr.microsoft.com/dotnet/sdk:8.0 image. Install with dotnet tool install -g GauntletCI, fetch the destination branch, generate the diff with git diff origin/$BITBUCKET_PR_DESTINATION_BRANCH...HEAD > pr.diff, and run gauntletci analyze --diff pr.diff --no-banner.",
  },
  {
    q: "How do I integrate GauntletCI with GitHub Actions?",
    a: "Add a workflow that checks out the repo with fetch-depth: 0, installs the .NET 8 tool, then runs: git diff origin/${{ github.base_ref }}...HEAD > pr.diff and gauntletci analyze --diff pr.diff --github-annotations. The step exits with code 1 if blocking findings are detected, failing the check.",
  },
  {
    q: "Does GauntletCI work with Azure Pipelines?",
    a: "Yes. Add a pipeline step that installs GauntletCI with dotnet tool install -g GauntletCI, then runs git diff to generate a diff file and passes it to gauntletci analyze --diff pr.diff --no-banner.",
  },
  {
    q: "Can GauntletCI run as a pre-commit hook instead of in CI?",
    a: "Yes, and this is the fastest setup. Run gauntletci init in your repository to install a .git/hooks/pre-commit script. It runs gauntletci analyze --staged before every commit with no CI configuration required.",
  },
  {
    q: "What does GauntletCI exit code 1 mean in a CI pipeline?",
    a: "Exit code 1 means blocking findings were detected. This fails the CI check and blocks the PR merge. Developers must address the findings or update the baseline before the check passes.",
  },
  {
    q: "Can I use GauntletCI with LLM enrichment in CI?",
    a: "The built-in ONNX engine is not available in CI because loading a 2 GB model in an ephemeral runner is impractical. Configure a remote OpenAI-compatible endpoint via llm.ciEndpoint in .gauntletci.json to use LLM enrichment in CI.",
  },
]);

export default function IntegrationsPage() {
  return (
    <>
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLd) }} />
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(softwareApplicationSchema) }} />
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(faqSchema) }} />
      <div className="space-y-10">
      <div>
        <p className="text-sm font-semibold text-cyan-400 uppercase tracking-widest mb-2">Extensions</p>
        <h1 className="text-4xl font-bold tracking-tight mb-4">Integrations</h1>
        <p className="text-lg text-muted-foreground">
          GauntletCI runs anywhere that can execute a .NET tool - in CI/CD pipelines, your editor,
          your AI assistant, or as a pre-commit hook. Install it with{" "}
          <code className="bg-muted px-1 rounded text-xs">dotnet tool install -g GauntletCI</code>{" "}
          and the extension for wherever you work.
        </p>
      </div>

      <section>
        <h2 className="text-2xl font-semibold mb-4">Extensions</h2>
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 mb-8">
          {[
            {
              href: "/docs/integrations/github-action",
              label: "GitHub Action",
              desc: "Block merges on high-risk changes. Post inline PR review comments.",
              tag: "CI/CD",
            },
            {
              href: "/docs/integrations/vscode",
              label: "VS Code Extension",
              desc: "Inline diagnostic squiggles, status bar, and analyze-on-save.",
              tag: "Editor",
            },
            {
              href: "/docs/integrations/azure-devops",
              label: "Azure DevOps Task",
              desc: "Pipeline task with inline ##vso annotations and branch policy enforcement.",
              tag: "CI/CD",
            },
            {
              href: "/docs/integrations/mcp",
              label: "MCP Server",
              desc: "Let Claude, Copilot, and Cursor analyze commits and explain findings.",
              tag: "AI",
            },
          ].map((card) => (
            <Link
              key={card.href}
              href={card.href}
              className="rounded-lg border border-border bg-card p-4 hover:border-cyan-500/50 transition-colors block"
            >
              <div className="flex items-start justify-between gap-2 mb-1">
                <p className="font-medium text-sm">{card.label}</p>
                <span className="text-xs text-cyan-400 border border-cyan-400/30 rounded px-1.5 py-0.5 shrink-0">
                  {card.tag}
                </span>
              </div>
              <p className="text-xs text-muted-foreground mt-1">{card.desc}</p>
            </Link>
          ))}
        </div>
      </section>

      <section>
        <h2 className="text-2xl font-semibold mb-3">GitHub Actions</h2>
        <p className="text-muted-foreground mb-2">
          The simplest setup uses the published Marketplace action. Add this workflow to analyze every pull request:
        </p>
        <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm overflow-x-auto mb-4">
          <pre className="text-foreground whitespace-pre">{`name: GauntletCI Analysis

on:
  pull_request:
    branches: [main]

permissions:
  pull-requests: write   # required for inline-comments: 'true'

jobs:
  analyze:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0

      - uses: EricCogen/GauntletCI@v2.1.1
        with:
          sensitivity: 'balanced'
          inline-comments: 'true'
          fail-on-findings: 'true'`}</pre>
        </div>
        <p className="text-sm text-muted-foreground mb-3">
          The action installs .NET, installs GauntletCI, runs analysis against the PR commit, and optionally posts findings as inline review comments.
          See the <Link href="/docs/integrations/github-action" className="text-cyan-400 hover:underline">GitHub Action docs</Link> for the full input reference.
        </p>

        <p className="text-sm font-semibold mb-2">Manual install (without the Marketplace action)</p>
        <p className="text-muted-foreground text-sm mb-3">
          Use this approach if you need full control over the workflow steps or are pinning to a specific tool version.
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
        <h2 className="text-2xl font-semibold mb-3">GitLab CI</h2>
        <p className="text-muted-foreground mb-4">
          Add this job to your <code className="bg-muted px-1 rounded text-xs">.gitlab-ci.yml</code>. The job runs only on merge request pipelines
          and uses the official Microsoft .NET SDK Docker image.
        </p>
        <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm overflow-x-auto">
          <pre className="text-foreground whitespace-pre">{`gauntletci-analysis:
  image: mcr.microsoft.com/dotnet/sdk:8.0
  rules:
    - if: $CI_PIPELINE_SOURCE == "merge_request_event"
  script:
    - export PATH="$PATH:$HOME/.dotnet/tools"
    - dotnet tool install -g GauntletCI
    - git fetch origin $CI_MERGE_REQUEST_TARGET_BRANCH_NAME
    - git diff origin/$CI_MERGE_REQUEST_TARGET_BRANCH_NAME...HEAD > pr.diff
    - gauntletci analyze --diff pr.diff --no-banner --ascii
  allow_failure: false`}</pre>
        </div>
        <p className="mt-3 text-sm text-muted-foreground">
          GitLab CI exposes <code className="bg-muted px-1 rounded text-xs">$CI_MERGE_REQUEST_TARGET_BRANCH_NAME</code> automatically
          on merge request pipelines. The <code className="bg-muted px-1 rounded text-xs">allow_failure: false</code> line blocks the merge
          if findings are detected. Set it to <code className="bg-muted px-1 rounded text-xs">true</code> to make the job advisory only.
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
        <p className="mt-3 text-sm text-muted-foreground">
          For inline pipeline annotations, use the{" "}
          <Link href="/docs/integrations/azure-devops" className="text-cyan-400 hover:underline">
            Azure DevOps Marketplace task
          </Link>{" "}
          instead, which emits <code className="bg-muted px-1 rounded text-xs">##vso</code> logging commands automatically.
        </p>
      </section>

      <section>
        <h2 className="text-2xl font-semibold mb-3">Bitbucket Pipelines</h2>
        <p className="text-muted-foreground mb-4">
          Add this to your <code className="bg-muted px-1 rounded text-xs">bitbucket-pipelines.yml</code>. The job runs on all pull request branches
          using the Microsoft .NET SDK image.
        </p>
        <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm overflow-x-auto">
          <pre className="text-foreground whitespace-pre">{`image: mcr.microsoft.com/dotnet/sdk:8.0

pipelines:
  pull-requests:
    '**':
      - step:
          name: GauntletCI Analysis
          script:
            - export PATH="$PATH:$HOME/.dotnet/tools"
            - dotnet tool install -g GauntletCI
            - git fetch origin $BITBUCKET_PR_DESTINATION_BRANCH
            - git diff origin/$BITBUCKET_PR_DESTINATION_BRANCH...HEAD > pr.diff
            - gauntletci analyze --diff pr.diff --no-banner --ascii`}</pre>
        </div>
        <p className="mt-3 text-sm text-muted-foreground">
          Bitbucket exposes <code className="bg-muted px-1 rounded text-xs">$BITBUCKET_PR_DESTINATION_BRANCH</code> automatically on pull
          request pipelines. The step fails (blocking merge) if GauntletCI exits with code 1.
        </p>
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

        <p className="text-sm font-semibold mb-2">Save to file</p>
        <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm mb-3">
          <span className="text-cyan-400">$</span>{" "}
          <span className="text-foreground">gauntletci analyze --staged --output json &gt; report.json</span>
        </div>
      </section>

      <section>
        <h2 className="text-2xl font-semibold mb-3">LLM enrichment in CI/CD</h2>
        <p className="text-muted-foreground mb-4">
          The built-in ONNX engine (Option 1 in the{" "}
          <Link href="/docs/local-llm" className="text-cyan-400 hover:underline">Local LLM Setup docs</Link>)
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


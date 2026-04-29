import type { Metadata } from "next";
import { buildFaqSchema, softwareApplicationSchema } from "@/lib/schemas";

export const metadata: Metadata = {
  title: "Bitbucket Pipelines Integration | GauntletCI Docs",
  description:
    "Integrate GauntletCI with Bitbucket Pipelines. Block PR merges on high-risk .NET changes using the official .NET SDK Docker image.",
  alternates: { canonical: "/docs/integrations/bitbucket" },
};

const jsonLd = {
  "@context": "https://schema.org",
  "@type": "TechArticle",
  headline: "GauntletCI Bitbucket Pipelines Integration",
  description: "Integrate GauntletCI with Bitbucket Pipelines pull request workflows.",
  url: "https://gauntletci.com/docs/integrations/bitbucket",
  publisher: { "@type": "Organization", name: "GauntletCI", url: "https://gauntletci.com" },
};

const faqSchema = buildFaqSchema([
  {
    q: "How do I integrate GauntletCI with Bitbucket Pipelines?",
    a: "Add a pull-requests pipeline step using the mcr.microsoft.com/dotnet/sdk:8.0 image. Install GauntletCI, export PATH, fetch the destination branch, generate a diff, and run gauntletci analyze --diff pr.diff.",
  },
  {
    q: "How does GauntletCI block a PR merge in Bitbucket?",
    a: "GauntletCI exits with code 1 when Block-severity findings are detected. A non-zero exit from a Bitbucket pipeline step marks the step as failed. Configure a merge check (Build) in repository settings to block the merge when the pipeline fails.",
  },
  {
    q: "What variable holds the PR destination branch in Bitbucket?",
    a: "BITBUCKET_PR_DESTINATION_BRANCH is provided automatically by Bitbucket Pipelines on all pull-request pipeline runs.",
  },
  {
    q: "Does GauntletCI work with Bitbucket Cloud and Bitbucket Data Center?",
    a: "Yes. Both support Docker-based pipeline steps. The same bitbucket-pipelines.yml configuration works for both.",
  },
]);

const BASIC_CONFIG = `image: mcr.microsoft.com/dotnet/sdk:8.0

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
            - gauntletci analyze --diff pr.diff --no-banner --ascii`;

const JSON_ARTIFACT = `image: mcr.microsoft.com/dotnet/sdk:8.0

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
            - gauntletci analyze --diff pr.diff --output json --no-banner > gauntletci-report.json
          artifacts:
            - gauntletci-report.json`;

const SENSITIVITY_CONFIG = `            - gauntletci analyze --diff pr.diff \\
                --sensitivity \${GAUNTLETCI_SENSITIVITY:-balanced} \\
                --no-banner --ascii`;

export default function BitbucketPage() {
  return (
    <>
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLd) }} />
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(softwareApplicationSchema) }} />
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(faqSchema) }} />
      <div className="space-y-10">
        <div>
          <p className="text-sm font-semibold text-cyan-400 uppercase tracking-widest mb-2">CI/CD</p>
          <h1 className="text-4xl font-bold tracking-tight mb-4">Bitbucket Pipelines Integration</h1>
          <p className="text-lg text-muted-foreground">
            Add a GauntletCI step to your <code className="bg-muted px-1 rounded text-xs">bitbucket-pipelines.yml</code> to
            analyze every pull request diff and block high-risk changes from merging.
          </p>
        </div>

        <section>
          <h2 className="text-2xl font-semibold mb-4">Basic setup</h2>
          <p className="text-muted-foreground mb-3">
            Add this to your <code className="bg-muted px-1 rounded text-xs">bitbucket-pipelines.yml</code>. The step runs
            on all pull request branches using the official Microsoft .NET SDK image:
          </p>
          <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm overflow-x-auto">
            <pre className="text-foreground whitespace-pre">{BASIC_CONFIG}</pre>
          </div>
          <ul className="mt-3 space-y-1 text-sm text-muted-foreground list-disc list-inside">
            <li>
              <code className="bg-muted px-1 rounded text-xs">$BITBUCKET_PR_DESTINATION_BRANCH</code> is set automatically on pull request pipelines.
            </li>
            <li>
              The step fails (exit code 1) if Block-severity findings are detected, blocking the merge.
            </li>
            <li>
              The <code className="bg-muted px-1 rounded text-xs">--ascii</code> flag prevents Unicode box-drawing characters from corrupting the pipeline log.
            </li>
          </ul>
        </section>

        <section>
          <h2 className="text-2xl font-semibold mb-4">Enable merge checks</h2>
          <p className="text-muted-foreground mb-3">
            To enforce the pipeline result as a merge gate:
          </p>
          <ol className="space-y-2 text-sm text-muted-foreground list-decimal list-inside">
            <li>Go to <strong>Repository Settings &gt; Merge checks</strong>.</li>
            <li>Enable <strong>Require passing builds</strong>.</li>
            <li>Optionally restrict it to the <strong>GauntletCI Analysis</strong> step only.</li>
          </ol>
          <p className="mt-3 text-sm text-muted-foreground">
            The merge button stays disabled until the pipeline passes.
          </p>
        </section>

        <section>
          <h2 className="text-2xl font-semibold mb-4">Save findings as an artifact</h2>
          <p className="text-muted-foreground mb-3">
            Use <code className="bg-muted px-1 rounded text-xs">--output json</code> and Bitbucket artifact paths to
            retain the report for 14 days (default):
          </p>
          <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm overflow-x-auto">
            <pre className="text-foreground whitespace-pre">{JSON_ARTIFACT}</pre>
          </div>
          <p className="mt-3 text-sm text-muted-foreground">
            Artifacts are downloadable from the pipeline step summary page.
          </p>
        </section>

        <section>
          <h2 className="text-2xl font-semibold mb-4">Sensitivity via repository variable</h2>
          <p className="text-muted-foreground mb-3">
            Set <code className="bg-muted px-1 rounded text-xs">GAUNTLETCI_SENSITIVITY</code> as a repository variable
            under <strong>Repository Settings &gt; Repository variables</strong> to control sensitivity without
            editing the YAML:
          </p>
          <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm overflow-x-auto">
            <pre className="text-foreground whitespace-pre">{SENSITIVITY_CONFIG}</pre>
          </div>
          <p className="mt-3 text-sm text-muted-foreground">
            Defaults to <code className="bg-muted px-1 rounded text-xs">balanced</code> if the variable is not set.
            Valid values: <code className="bg-muted px-1 rounded text-xs">strict</code>,{" "}
            <code className="bg-muted px-1 rounded text-xs">balanced</code>,{" "}
            <code className="bg-muted px-1 rounded text-xs">permissive</code>.
          </p>
        </section>

        <section>
          <h2 className="text-2xl font-semibold mb-4">Pipeline step output</h2>
          <div className="rounded-lg border border-border bg-zinc-950 overflow-hidden mb-3">
            <div className="bg-zinc-900 px-4 py-2 text-xs text-muted-foreground border-b border-border flex items-center gap-2">
              <span className="font-medium text-foreground">GauntletCI Analysis</span>
              <span className="ml-auto text-red-400">FAILED</span>
            </div>
            <div className="p-4 font-mono text-xs space-y-1">
              <p className="text-muted-foreground">+ gauntletci analyze --diff pr.diff --no-banner --ascii</p>
              <p className="text-foreground mt-2">GauntletCI v2.1.1</p>
              <p className="text-foreground">Analyzed 3 files, 47 changed lines</p>
              <p className="text-foreground mt-1">
                <span className="text-red-400">[BLOCK]</span>{" "}
                OrderService.cs:42 GCI0001 Logic change without test coverage
              </p>
              <p className="text-foreground">
                <span className="text-red-400">[BLOCK]</span>{" "}
                IOrderService.cs:18 GCI0003 Public API breaking change
              </p>
              <p className="text-foreground mt-1">2 block, 0 warn, 0 advisory</p>
              <p className="text-red-400 mt-1">error: process exited with code 1</p>
            </div>
          </div>
        </section>
      </div>
    </>
  );
}

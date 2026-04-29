import type { Metadata } from "next";
import { buildFaqSchema, softwareApplicationSchema } from "@/lib/schemas";

export const metadata: Metadata = {
  title: "GitLab CI Integration | GauntletCI Docs",
  description:
    "Integrate GauntletCI with GitLab CI/CD pipelines. Block MR merges on high-risk .NET changes using the official .NET SDK Docker image.",
  alternates: { canonical: "/docs/integrations/gitlab" },
};

const jsonLd = {
  "@context": "https://schema.org",
  "@type": "TechArticle",
  headline: "GauntletCI GitLab CI Integration",
  description: "Integrate GauntletCI with GitLab CI/CD merge request pipelines.",
  url: "https://gauntletci.com/docs/integrations/gitlab",
  publisher: { "@type": "Organization", name: "GauntletCI", url: "https://gauntletci.com" },
};

const faqSchema = buildFaqSchema([
  {
    q: "How do I integrate GauntletCI with GitLab CI?",
    a: "Add a job using the mcr.microsoft.com/dotnet/sdk:8.0 image with the rule: if: $CI_PIPELINE_SOURCE == 'merge_request_event'. Install GauntletCI, export PATH, fetch the target branch, generate a diff, and run gauntletci analyze --diff pr.diff.",
  },
  {
    q: "How do I block an MR merge in GitLab if GauntletCI finds issues?",
    a: "Set allow_failure: false on the job (the default). GauntletCI exits with code 1 on Block-severity findings, which marks the job as failed and prevents the MR from being merged if a required approval or pipeline rule is configured.",
  },
  {
    q: "How do I make the GauntletCI job advisory only in GitLab?",
    a: "Set allow_failure: true on the job. Findings will be reported in the pipeline but will not block the merge.",
  },
  {
    q: "Does GauntletCI work with GitLab shared runners?",
    a: "Yes. The job uses the mcr.microsoft.com/dotnet/sdk:8.0 Docker image, which is available on any GitLab runner with Docker executor. No custom runner configuration is needed.",
  },
]);

const BASIC_JOB = `gauntletci-analysis:
  image: mcr.microsoft.com/dotnet/sdk:8.0
  rules:
    - if: $CI_PIPELINE_SOURCE == "merge_request_event"
  script:
    - export PATH="$PATH:$HOME/.dotnet/tools"
    - dotnet tool install -g GauntletCI
    - git fetch origin $CI_MERGE_REQUEST_TARGET_BRANCH_NAME
    - git diff origin/$CI_MERGE_REQUEST_TARGET_BRANCH_NAME...HEAD > pr.diff
    - gauntletci analyze --diff pr.diff --no-banner --ascii
  allow_failure: false`;

const ADVISORY_JOB = `gauntletci-analysis:
  image: mcr.microsoft.com/dotnet/sdk:8.0
  rules:
    - if: $CI_PIPELINE_SOURCE == "merge_request_event"
  script:
    - export PATH="$PATH:$HOME/.dotnet/tools"
    - dotnet tool install -g GauntletCI
    - git fetch origin $CI_MERGE_REQUEST_TARGET_BRANCH_NAME
    - git diff origin/$CI_MERGE_REQUEST_TARGET_BRANCH_NAME...HEAD > pr.diff
    - gauntletci analyze --diff pr.diff --no-banner --ascii
  allow_failure: true   # findings reported but MR not blocked`;

const JSON_ARTIFACT = `gauntletci-analysis:
  image: mcr.microsoft.com/dotnet/sdk:8.0
  rules:
    - if: $CI_PIPELINE_SOURCE == "merge_request_event"
  script:
    - export PATH="$PATH:$HOME/.dotnet/tools"
    - dotnet tool install -g GauntletCI
    - git fetch origin $CI_MERGE_REQUEST_TARGET_BRANCH_NAME
    - git diff origin/$CI_MERGE_REQUEST_TARGET_BRANCH_NAME...HEAD > pr.diff
    - gauntletci analyze --diff pr.diff --output json --no-banner > gauntletci-report.json
  artifacts:
    paths:
      - gauntletci-report.json
    expire_in: 7 days
  allow_failure: false`;

export default function GitLabPage() {
  return (
    <>
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLd) }} />
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(softwareApplicationSchema) }} />
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(faqSchema) }} />
      <div className="space-y-10">
        <div>
          <p className="text-sm font-semibold text-cyan-400 uppercase tracking-widest mb-2">CI/CD</p>
          <h1 className="text-4xl font-bold tracking-tight mb-4">GitLab CI Integration</h1>
          <p className="text-lg text-muted-foreground">
            Add a GauntletCI job to your <code className="bg-muted px-1 rounded text-xs">.gitlab-ci.yml</code> to
            analyze every merge request diff and block high-risk changes from merging.
          </p>
        </div>

        <section>
          <h2 className="text-2xl font-semibold mb-4">Basic setup</h2>
          <p className="text-muted-foreground mb-3">
            Add this job to your <code className="bg-muted px-1 rounded text-xs">.gitlab-ci.yml</code>. It runs only
            on merge request pipelines using the official Microsoft .NET SDK Docker image:
          </p>
          <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm overflow-x-auto">
            <pre className="text-foreground whitespace-pre">{BASIC_JOB}</pre>
          </div>
          <ul className="mt-3 space-y-1 text-sm text-muted-foreground list-disc list-inside">
            <li>
              <code className="bg-muted px-1 rounded text-xs">$CI_MERGE_REQUEST_TARGET_BRANCH_NAME</code> is set automatically by GitLab on MR pipelines.
            </li>
            <li>
              <code className="bg-muted px-1 rounded text-xs">allow_failure: false</code> marks the MR pipeline as failed if findings are detected.
            </li>
            <li>
              The <code className="bg-muted px-1 rounded text-xs">--ascii</code> flag prevents Unicode box-drawing characters from corrupting the job log.
            </li>
          </ul>
        </section>

        <section>
          <h2 className="text-2xl font-semibold mb-4">Advisory mode</h2>
          <p className="text-muted-foreground mb-3">
            To report findings without blocking the merge, set <code className="bg-muted px-1 rounded text-xs">allow_failure: true</code>:
          </p>
          <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm overflow-x-auto">
            <pre className="text-foreground whitespace-pre">{ADVISORY_JOB}</pre>
          </div>
        </section>

        <section>
          <h2 className="text-2xl font-semibold mb-4">Save findings as a pipeline artifact</h2>
          <p className="text-muted-foreground mb-3">
            Use <code className="bg-muted px-1 rounded text-xs">--output json</code> to write a structured report and upload it as a GitLab artifact:
          </p>
          <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm overflow-x-auto">
            <pre className="text-foreground whitespace-pre">{JSON_ARTIFACT}</pre>
          </div>
          <p className="mt-3 text-sm text-muted-foreground">
            The artifact is available under <strong>Browse artifacts</strong> on the pipeline job page and
            can be downloaded or used by downstream jobs.
          </p>
        </section>

        <section>
          <h2 className="text-2xl font-semibold mb-4">Pipeline mockup</h2>
          <div className="rounded-lg border border-border bg-zinc-950 overflow-hidden mb-3">
            <div className="bg-zinc-900 px-4 py-2 text-xs text-muted-foreground border-b border-border flex items-center gap-2">
              <span className="w-2 h-2 rounded-full bg-red-500 shrink-0" />
              <span className="font-medium text-foreground">Pipeline #1042</span>
              <span className="ml-auto text-muted-foreground">MR !18: fix order total calculation</span>
            </div>
            <div className="p-4 space-y-2 text-xs font-mono">
              <div className="flex items-center gap-3">
                <span className="w-3 h-3 rounded-full bg-green-500 shrink-0" />
                <span className="text-foreground">build</span>
                <span className="ml-auto text-muted-foreground">0:42</span>
              </div>
              <div className="flex items-center gap-3">
                <span className="w-3 h-3 rounded-full bg-green-500 shrink-0" />
                <span className="text-foreground">test</span>
                <span className="ml-auto text-muted-foreground">1:14</span>
              </div>
              <div className="flex items-center gap-3">
                <span className="w-3 h-3 rounded-full bg-red-500 shrink-0" />
                <span className="text-foreground">gauntletci-analysis</span>
                <span className="ml-2 text-red-400">2 block findings</span>
                <span className="ml-auto text-muted-foreground">0:08</span>
              </div>
            </div>
          </div>
          <p className="text-sm text-muted-foreground">
            A failed <code className="bg-muted px-1 rounded text-xs">gauntletci-analysis</code> job blocks the merge button
            when GitLab is configured with a required pipeline status rule.
          </p>
        </section>

        <section>
          <h2 className="text-2xl font-semibold mb-4">Sensitivity configuration</h2>
          <p className="text-muted-foreground mb-3">
            Pass <code className="bg-muted px-1 rounded text-xs">--sensitivity</code> to control which findings are
            reported. You can also set it via a CI/CD variable so different branches use different levels:
          </p>
          <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm overflow-x-auto">
            <pre className="text-foreground whitespace-pre">{`    - gauntletci analyze --diff pr.diff \\
        --sensitivity \${GAUNTLETCI_SENSITIVITY:-balanced} \\
        --no-banner --ascii`}</pre>
          </div>
          <p className="mt-3 text-sm text-muted-foreground">
            Set <code className="bg-muted px-1 rounded text-xs">GAUNTLETCI_SENSITIVITY</code> as a GitLab CI/CD variable
            under <strong>Settings &gt; CI/CD &gt; Variables</strong>. Defaults to <code className="bg-muted px-1 rounded text-xs">balanced</code> if unset.
          </p>
        </section>
      </div>
    </>
  );
}

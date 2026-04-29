import type { Metadata } from "next";
import { buildFaqSchema, softwareApplicationSchema } from "@/lib/schemas";

export const metadata: Metadata = {
  title: "Azure DevOps Task | GauntletCI Docs",
  description:
    "Add GauntletCI to Azure Pipelines. Get inline ##vso error and warning annotations, block-level pipeline failures, and structured JSON findings in your .NET PR builds.",
  alternates: { canonical: "/docs/integrations/azure-devops" },
};

const jsonLd = {
  "@context": "https://schema.org",
  "@type": "TechArticle",
  headline: "GauntletCI Azure DevOps Task",
  description:
    "Add GauntletCI to Azure Pipelines for inline annotations and pipeline failure on high-risk .NET changes.",
  url: "https://gauntletci.com/docs/integrations/azure-devops",
  publisher: { "@type": "Organization", name: "GauntletCI", url: "https://gauntletci.com" },
};

const faqSchema = buildFaqSchema([
  {
    q: "How do I install the GauntletCI Azure DevOps extension?",
    a: "Search for 'GauntletCI' in the Azure DevOps Marketplace and click Get. You can install it organization-wide or for a specific project. Once installed, the GauntletCI Analyze task appears in the task library.",
  },
  {
    q: "Does the GauntletCI ADO task need .NET installed on the agent?",
    a: "Yes. The task installs GauntletCI via dotnet tool install -g GauntletCI, so .NET 8 must be available on the agent. Microsoft-hosted ubuntu-latest and windows-latest agents include .NET 8.",
  },
  {
    q: "What does failOnBlock do in the Azure Pipelines task?",
    a: "When failOnBlock is true (default), the task sets its result to Failed when one or more Block-severity findings are produced. The pipeline step fails, which blocks PR completion if the branch policy requires a passing build.",
  },
  {
    q: "How do GauntletCI annotations appear in Azure Pipelines?",
    a: "Block findings emit ##vso[task.logissue type=error] commands. Warn findings emit ##vso[task.logissue type=warning] commands. These appear as inline error and warning annotations in the pipeline run summary and build log.",
  },
]);

const YAML_PIPELINE = `trigger: none
pr:
  branches:
    include: [main]

pool:
  vmImage: ubuntu-latest

steps:
  - task: UseDotNet@2
    inputs:
      version: '8.0.x'

  - task: GauntletCI@0
    displayName: 'GauntletCI - Analyze PR'
    inputs:
      sensitivity: 'balanced'
      failOnBlock: true
      workingDirectory: '$(Build.SourcesDirectory)'
      gauntletciVersion: 'latest'`;

const CLASSIC_YAML = `# Equivalent manual steps if not using the Marketplace task
- script: dotnet tool install -g GauntletCI
  displayName: 'Install GauntletCI'

- script: |
    export PATH="$PATH:$HOME/.dotnet/tools"
    git diff origin/$(System.PullRequest.TargetBranch)...HEAD > pr.diff
    gauntletci analyze --diff pr.diff --no-banner --ascii
  displayName: 'Run GauntletCI'
  failOnStderr: false`;

export default function AzureDevOpsPage() {
  return (
    <>
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLd) }} />
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(softwareApplicationSchema) }} />
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(faqSchema) }} />
      <div className="space-y-10">
        <div>
          <p className="text-sm font-semibold text-cyan-400 uppercase tracking-widest mb-2">
            Extensions - Azure DevOps
          </p>
          <h1 className="text-4xl font-bold tracking-tight mb-4">Azure DevOps Task</h1>
          <p className="text-lg text-muted-foreground">
            The GauntletCI Azure Pipelines task installs the CLI, analyzes the current commit, and
            emits inline annotations in the pipeline run summary - the same red error and yellow
            warning markers you see from build tasks and test runners.
          </p>
        </div>

        <section>
          <h2 className="text-2xl font-semibold mb-3">Install the extension</h2>
          <ol className="text-sm text-muted-foreground space-y-2 list-none mb-4">
            {[
              "Go to the Azure DevOps Marketplace",
              'Search for "GauntletCI"',
              "Click Get and choose your organization",
              "Confirm the installation - no restart required",
            ].map((step, i) => (
              <li key={i} className="flex items-start gap-2">
                <span className="text-cyan-400 shrink-0 w-4 text-right">{i + 1}.</span>
                <span>{step}</span>
              </li>
            ))}
          </ol>
          <p className="text-sm text-muted-foreground">
            Once installed, the{" "}
            <code className="bg-muted px-1 rounded text-xs">GauntletCI@0</code> task is available
            in the YAML task library and in the Classic editor task picker.
          </p>
        </section>

        <section>
          <h2 className="text-2xl font-semibold mb-3">YAML pipeline</h2>
          <p className="text-muted-foreground mb-3 text-sm">
            Add this to a new pipeline file in your repository. The pipeline runs on every pull
            request targeting <code className="bg-muted px-1 rounded text-xs">main</code>.
          </p>
          <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm overflow-x-auto">
            <pre className="text-foreground whitespace-pre">{YAML_PIPELINE}</pre>
          </div>
        </section>

        <section>
          <h2 className="text-2xl font-semibold mb-3">How annotations look in Azure Pipelines</h2>
          <p className="text-muted-foreground mb-4 text-sm">
            Block findings appear as red inline errors in the pipeline run summary. Warn findings
            appear as yellow warnings. Both link to the source file and line number when a path is
            available.
          </p>

          {/* ADO pipeline annotations mockup */}
          <div className="rounded-lg border border-border overflow-hidden">
            <div className="bg-muted/30 px-4 py-2 text-xs text-muted-foreground border-b border-border">
              Pipeline - Build - GauntletCI Analyze
            </div>
            <div className="p-4 font-mono text-sm space-y-2">
              <div className="flex items-start gap-2">
                <span className="text-muted-foreground/50 text-xs w-5 shrink-0">00:01</span>
                <span className="text-foreground">
                  GauntletCI: 3 finding(s) from 42 rules evaluated.
                </span>
              </div>
              <div className="flex items-start gap-2">
                <span className="text-muted-foreground/50 text-xs w-5 shrink-0">00:01</span>
                <div>
                  <span className="inline-flex items-center gap-1 bg-red-500/10 border border-red-500/20 rounded px-2 py-0.5 text-xs text-red-400">
                    error
                  </span>{" "}
                  <span className="text-foreground">OrderService.cs(44): [GCI0001] Behavior change without test coverage.</span>
                </div>
              </div>
              <div className="flex items-start gap-2">
                <span className="text-muted-foreground/50 text-xs w-5 shrink-0">00:01</span>
                <div>
                  <span className="inline-flex items-center gap-1 bg-red-500/10 border border-red-500/20 rounded px-2 py-0.5 text-xs text-red-400">
                    error
                  </span>{" "}
                  <span className="text-foreground">PaymentService.cs(112): [GCI0003] Exception path added with no callers updated.</span>
                </div>
              </div>
              <div className="flex items-start gap-2">
                <span className="text-muted-foreground/50 text-xs w-5 shrink-0">00:01</span>
                <div>
                  <span className="inline-flex items-center gap-1 bg-yellow-500/10 border border-yellow-500/20 rounded px-2 py-0.5 text-xs text-yellow-400">
                    warning
                  </span>{" "}
                  <span className="text-foreground">Models/Order.cs(23): [GCI0004] Return type semantics changed.</span>
                </div>
              </div>
              <div className="flex items-start gap-2 pt-1 border-t border-border">
                <span className="text-muted-foreground/50 text-xs w-5 shrink-0">00:01</span>
                <span className="text-red-400">
                  ##[error]GauntletCI: 2 block-level finding(s) detected. See annotations above.
                </span>
              </div>
            </div>
          </div>
        </section>

        <section>
          <h2 className="text-2xl font-semibold mb-3">Task inputs</h2>
          <div className="rounded-lg border border-border overflow-hidden">
            <table className="w-full text-sm">
              <thead className="bg-muted/50">
                <tr>
                  <th className="text-left px-4 py-2 font-medium text-muted-foreground">Input</th>
                  <th className="text-left px-4 py-2 font-medium text-muted-foreground">Default</th>
                  <th className="text-left px-4 py-2 font-medium text-muted-foreground">Description</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {[
                  ["sensitivity", "balanced", "strict | balanced | permissive. Controls the confidence threshold for findings."],
                  ["failOnBlock", "true", "Set the task result to Failed when any Block-severity finding is produced."],
                  ["workingDirectory", "$(Build.SourcesDirectory)", "Root of the .NET repository. Passed as the working directory for the GauntletCI process."],
                  ["gauntletciVersion", "latest", "NuGet version to install. Use 'latest' or pin to a specific version such as '2.1.1'."],
                ].map(([name, def, desc]) => (
                  <tr key={name}>
                    <td className="px-4 py-2 font-mono text-xs text-cyan-400">{name}</td>
                    <td className="px-4 py-2 font-mono text-xs text-foreground">{def}</td>
                    <td className="px-4 py-2 text-muted-foreground text-sm">{desc}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>

        <section>
          <h2 className="text-2xl font-semibold mb-3">Manual install (without the Marketplace task)</h2>
          <p className="text-muted-foreground mb-3 text-sm">
            If you cannot install from the Marketplace or prefer full control, use the raw script
            steps below. These work identically to the task.
          </p>
          <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm overflow-x-auto">
            <pre className="text-foreground whitespace-pre">{CLASSIC_YAML}</pre>
          </div>
        </section>

        <section>
          <h2 className="text-2xl font-semibold mb-3">Branch policy enforcement</h2>
          <p className="text-muted-foreground text-sm">
            To block PRs from completing when GauntletCI finds blocking issues, add the pipeline as
            a required build policy on your target branch. In the Azure DevOps project settings,
            go to{" "}
            <strong className="text-foreground">Repos &gt; Branches &gt; Branch policies</strong>{" "}
            for <code className="bg-muted px-1 rounded text-xs">main</code>, add a Build validation
            policy, and select your GauntletCI pipeline. Set it to Required.
          </p>
        </section>
      </div>
    </>
  );
}

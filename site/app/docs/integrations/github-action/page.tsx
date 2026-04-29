import type { Metadata } from "next";
import { buildFaqSchema, softwareApplicationSchema } from "@/lib/schemas";

export const metadata: Metadata = {
  title: "GitHub Action | GauntletCI Docs",
  description:
    "Use GauntletCI in GitHub Actions. Block merges on high-risk changes, post inline PR review comments, and surface findings as GitHub Checks annotations.",
  alternates: { canonical: "/docs/integrations/github-action" },
};

const jsonLd = {
  "@context": "https://schema.org",
  "@type": "TechArticle",
  headline: "GauntletCI GitHub Action",
  description:
    "Use the GauntletCI GitHub Action to analyze pull requests and post findings as inline PR review comments.",
  url: "https://gauntletci.com/docs/integrations/github-action",
  publisher: { "@type": "Organization", name: "GauntletCI", url: "https://gauntletci.com" },
};

const faqSchema = buildFaqSchema([
  {
    q: "Does the GauntletCI GitHub Action need a token to post inline comments?",
    a: "Yes. Set pull-requests: write in the permissions block of your calling workflow. The action uses the default GITHUB_TOKEN - no personal access token is required.",
  },
  {
    q: "How do I prevent the GauntletCI action from failing the build?",
    a: "Set fail-on-findings: 'false'. The action will still run and report findings but will exit with code 0 regardless of results.",
  },
  {
    q: "What does the findings-count output contain?",
    a: "findings-count is a string containing the number of findings GauntletCI produced. It counts lines in the output that match a GCI rule ID pattern. Use it in subsequent steps to conditionally post Slack alerts or upload artifacts.",
  },
  {
    q: "Can I pin to a specific GauntletCI tool version?",
    a: "Yes. Set gauntletci-version to the exact NuGet version you want, such as '2.1.0'. The default is the latest published version.",
  },
]);

const MINIMAL_WORKFLOW = `name: GauntletCI

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

      - uses: EricCogen/GauntletCI@v2.1.1`;

const FULL_WORKFLOW = `name: GauntletCI

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
        id: gauntlet
        with:
          sensitivity: 'balanced'
          inline-comments: 'true'
          fail-on-findings: 'true'

      - name: Upload findings artifact
        if: always()
        run: |
          echo "Findings count: \${{ steps.gauntlet.outputs.findings-count }}"`;

export default function GithubActionPage() {
  return (
    <>
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLd) }} />
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(softwareApplicationSchema) }} />
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(faqSchema) }} />
      <div className="space-y-10">
        <div>
          <p className="text-sm font-semibold text-cyan-400 uppercase tracking-widest mb-2">
            Extensions - GitHub Action
          </p>
          <h1 className="text-4xl font-bold tracking-tight mb-4">GitHub Action</h1>
          <p className="text-lg text-muted-foreground">
            The GauntletCI GitHub Action analyzes every pull request against your repository rules
            and optionally posts findings as inline review comments - without any runner setup or
            credentials beyond the default{" "}
            <code className="bg-muted px-1 rounded text-xs">GITHUB_TOKEN</code>.
          </p>
        </div>

        <section>
          <h2 className="text-2xl font-semibold mb-3">Quickstart</h2>
          <p className="text-muted-foreground mb-3 text-sm">
            Add this workflow to{" "}
            <code className="bg-muted px-1 rounded text-xs">.github/workflows/gauntletci.yml</code>{" "}
            in your repository. No additional configuration is required.
          </p>
          <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm overflow-x-auto">
            <pre className="text-foreground whitespace-pre">{MINIMAL_WORKFLOW}</pre>
          </div>
          <p className="text-sm text-muted-foreground mt-3">
            This runs with all defaults: balanced sensitivity, findings fail the check, no inline
            comments. To post findings as inline PR review comments, add{" "}
            <code className="bg-muted px-1 rounded text-xs">pull-requests: write</code> and set{" "}
            <code className="bg-muted px-1 rounded text-xs">inline-comments: &apos;true&apos;</code>.
          </p>
        </section>

        <section>
          <h2 className="text-2xl font-semibold mb-3">Full example with inline comments</h2>
          <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm overflow-x-auto">
            <pre className="text-foreground whitespace-pre">{FULL_WORKFLOW}</pre>
          </div>
        </section>

        <section>
          <h2 className="text-2xl font-semibold mb-3">How inline comments look</h2>
          <p className="text-muted-foreground mb-4 text-sm">
            When <code className="bg-muted px-1 rounded text-xs">inline-comments: &apos;true&apos;</code>{" "}
            is set, GauntletCI posts each finding as a GitHub review comment on the exact diff line
            it originated from.
          </p>

          {/* PR comment mockup */}
          <div className="rounded-lg border border-border overflow-hidden">
            <div className="bg-muted/30 px-4 py-2 text-xs text-muted-foreground border-b border-border">
              Pull Request - Files Changed - OrderService.cs
            </div>
            <div className="font-mono text-sm">
              <div className="flex">
                <div className="bg-green-500/5 border-r border-border px-3 py-2 text-xs text-muted-foreground w-10 shrink-0 text-right">
                  44
                </div>
                <div className="bg-green-500/5 px-4 py-2 text-green-400 flex-1">
                  {"+ await _repository.SaveAsync(order);"}
                </div>
              </div>
              <div className="border-t border-cyan-500/20 bg-cyan-500/5 p-4">
                <div className="flex items-start gap-3">
                  <div className="w-7 h-7 rounded-full bg-cyan-500/20 flex items-center justify-center shrink-0 text-xs font-bold text-cyan-400">
                    G
                  </div>
                  <div className="flex-1">
                    <p className="text-xs font-semibold text-foreground mb-1">
                      gauntletci-bot{" "}
                      <span className="font-normal text-muted-foreground">commented</span>
                    </p>
                    <div className="rounded border border-border bg-card p-3 text-sm">
                      <p className="text-red-400 font-semibold text-xs mb-1">
                        [Block] GCI0001 - Behavior Change Without Test Coverage
                      </p>
                      <p className="text-muted-foreground text-xs">
                        <span className="font-semibold text-foreground">Summary:</span>{" "}
                        SaveAsync was modified but no test file covering OrderService was updated.
                      </p>
                      <p className="text-muted-foreground text-xs mt-1">
                        <span className="font-semibold text-foreground">Suggested action:</span>{" "}
                        Add or update a test that exercises the new behavior before merging.
                      </p>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </section>

        <section>
          <h2 className="text-2xl font-semibold mb-3">Inputs</h2>
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
                  ["sensitivity", '"balanced"', "strict | balanced | permissive. Controls which confidence levels trigger findings."],
                  ["fail-on-findings", '"true"', "Exit with code 1 when any findings are produced, failing the GitHub check."],
                  ["inline-comments", '"false"', "Post findings as inline PR review comments. Requires pull-requests: write."],
                  ["no-llm", '"true"', "Disable LLM enrichment. Recommended for CI - keeps the step deterministic and fast."],
                  ["ascii", '"true"', "Use ASCII-only output. Recommended for CI logs - avoids encoding issues."],
                  ["commit", '""', "Commit SHA to analyze. Defaults to the PR head commit (github.event.pull_request.head.sha)."],
                  ["dotnet-version", '"8.0.x"', "The .NET SDK version to install on the runner."],
                  ["gauntletci-version", '"2.1.1"', "The GauntletCI NuGet tool version to install."],
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
          <h2 className="text-2xl font-semibold mb-3">Outputs</h2>
          <div className="rounded-lg border border-border overflow-hidden">
            <table className="w-full text-sm">
              <thead className="bg-muted/50">
                <tr>
                  <th className="text-left px-4 py-2 font-medium text-muted-foreground">Output</th>
                  <th className="text-left px-4 py-2 font-medium text-muted-foreground">Description</th>
                </tr>
              </thead>
              <tbody>
                <tr>
                  <td className="px-4 py-2 font-mono text-xs text-cyan-400">findings-count</td>
                  <td className="px-4 py-2 text-muted-foreground text-sm">
                    Number of findings detected. Reference as{" "}
                    <code className="bg-muted px-1 rounded text-xs">
                      {"${{ steps.<id>.outputs.findings-count }}"}
                    </code>{" "}
                    in downstream steps.
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>

        <section>
          <h2 className="text-2xl font-semibold mb-3">Permissions required</h2>
          <div className="rounded-lg border border-border overflow-hidden">
            <table className="w-full text-sm">
              <thead className="bg-muted/50">
                <tr>
                  <th className="text-left px-4 py-2 font-medium text-muted-foreground">Feature</th>
                  <th className="text-left px-4 py-2 font-medium text-muted-foreground">Permission needed</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {[
                  ["Basic analysis (no comments)", "None - uses default runner permissions"],
                  ["GitHub Checks annotations", "checks: write (automatic on pull_request)"],
                  ["Inline PR review comments", "pull-requests: write (must be set explicitly)"],
                ].map(([feature, perm]) => (
                  <tr key={feature}>
                    <td className="px-4 py-2 text-muted-foreground">{feature}</td>
                    <td className="px-4 py-2 font-mono text-xs text-foreground">{perm}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>

        <section>
          <h2 className="text-2xl font-semibold mb-3">Advisory-only mode</h2>
          <p className="text-muted-foreground mb-3 text-sm">
            To run GauntletCI as an informational check that never blocks merges, set{" "}
            <code className="bg-muted px-1 rounded text-xs">fail-on-findings: &apos;false&apos;</code>.
            Findings still appear in the job log and as inline comments (if enabled), but the check
            always passes.
          </p>
          <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm">
            <pre className="text-foreground whitespace-pre">{`- uses: EricCogen/GauntletCI@v2.1.1
  with:
    fail-on-findings: 'false'
    inline-comments: 'true'
    sensitivity: 'permissive'`}</pre>
          </div>
        </section>
      </div>
    </>
  );
}

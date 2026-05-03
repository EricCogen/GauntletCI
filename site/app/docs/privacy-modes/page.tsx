import type { Metadata } from "next";
import Link from "next/link";

export const metadata: Metadata = {
  title: "Privacy Modes | GauntletCI Docs",
  description: "Understand GauntletCI's four operational modes: Default (local only), Local AI (offline models), Integration (network services), and CI AI (remote endpoints).",
  alternates: { canonical: "/docs/privacy-modes" },
  openGraph: { images: [{ url: '/og/docs.png', width: 1200, height: 630 }] },
};

const jsonLd = {
  "@context": "https://schema.org",
  "@type": "TechArticle",
  "headline": "GauntletCI Privacy Modes",
  "description": "Operational modes for local-only and integrated analysis",
  "url": "https://gauntletci.com/docs/privacy-modes",
  "publisher": { "@type": "Organization", "name": "GauntletCI", "url": "https://gauntletci.com" },
};

export default function PrivacyModesPage() {
  return (
    <>
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLd) }} />
      <div className="space-y-10">
        <div>
          <p className="text-sm font-semibold text-cyan-400 uppercase tracking-widest mb-2">Architecture</p>
          <h1 className="text-4xl font-bold tracking-tight mb-4">Privacy Modes</h1>
          <p className="text-lg text-muted-foreground">
            GauntletCI operates in four distinct modes, each with different privacy and integration characteristics.
            Choose the mode that fits your security and feature requirements.
          </p>
        </div>

        <section className="space-y-8">
          <div className="grid gap-6">
            {/* Default Mode */}
            <div className="border border-border rounded-lg p-6 bg-card">
              <div className="flex items-start gap-3 mb-4">
                <div className="flex-shrink-0 w-10 h-10 rounded-lg bg-green-500/20 flex items-center justify-center">
                  <span className="text-green-400 font-bold">1</span>
                </div>
                <div>
                  <h2 className="text-2xl font-semibold mb-1">Default Mode</h2>
                  <p className="text-sm text-muted-foreground">Deterministic local analysis, no network required</p>
                </div>
              </div>

              <div className="space-y-3 text-sm mb-4">
                <div>
                  <p className="font-semibold text-foreground mb-1">✓ What's enabled:</p>
                  <ul className="list-disc list-inside space-y-1 text-muted-foreground">
                    <li>30+ built-in deterministic rules (GCI0001-GCI0037)</li>
                    <li>Diff-based change detection</li>
                    <li>Local AST analysis (Roslyn syntax trees)</li>
                    <li>Pre-commit hook integration</li>
                    <li>CLI and configuration file support</li>
                  </ul>
                </div>

                <div>
                  <p className="font-semibold text-foreground mb-1">✗ What's disabled:</p>
                  <ul className="list-disc list-inside space-y-1 text-muted-foreground">
                    <li>Network communication (deterministic only)</li>
                    <li>GitHub Checks integration</li>
                    <li>Slack notifications</li>
                    <li>Jira issue linking</li>
                    <li>LLM explanations and enrichment</li>
                    <li>Remote model inference</li>
                    <li>Telemetry and analytics</li>
                  </ul>
                </div>

                <div>
                  <p className="font-semibold text-foreground mb-1">🔒 Privacy guarantee:</p>
                  <p className="text-muted-foreground">
                    No code, diffs, or analysis results leave your machine. All computation runs locally on your hardware.
                  </p>
                </div>

                <div>
                  <p className="font-semibold text-foreground mb-1">⚙️ Configuration:</p>
                  <p className="text-muted-foreground">
                    By default, GauntletCI operates in this mode with no additional setup required.
                  </p>
                </div>
              </div>
            </div>

            {/* Local AI Mode */}
            <div className="border border-border rounded-lg p-6 bg-card">
              <div className="flex items-start gap-3 mb-4">
                <div className="flex-shrink-0 w-10 h-10 rounded-lg bg-blue-500/20 flex items-center justify-center">
                  <span className="text-blue-400 font-bold">2</span>
                </div>
                <div>
                  <h1 className="text-2xl font-semibold mb-1">Local AI Mode</h1>
                  <p className="text-sm text-muted-foreground">Default + optional offline ONNX model inference</p>
                </div>
              </div>

              <div className="space-y-3 text-sm mb-4">
                <div>
                  <p className="font-semibold text-foreground mb-1">✓ What's added:</p>
                  <ul className="list-disc list-inside space-y-1 text-muted-foreground">
                    <li>LLM explanations for built-in findings (optional enrichment)</li>
                    <li>Experimental engineering policy rules (LLM-evaluated, optional)</li>
                    <li>Offline ONNX models (e.g., Phi-4 Mini, Llama 2)</li>
                    <li>Model runs on your local GPU/CPU</li>
                    <li>No network calls required for inference</li>
                  </ul>
                </div>

                <div>
                  <p className="font-semibold text-foreground mb-1">🔒 Privacy guarantee:</p>
                  <p className="text-muted-foreground">
                    Code and diffs remain local. Model inference runs on your hardware. No remote API calls for analysis enrichment.
                  </p>
                </div>

                <div>
                  <p className="font-semibold text-foreground mb-1">⚙️ Configuration:</p>
                  <p className="text-muted-foreground">
                    Configure with <code className="bg-muted px-1 rounded text-xs">--llm-model phi4-mini</code> or in configuration file:
                  </p>
                  <pre className="bg-muted p-2 rounded mt-2 text-xs overflow-x-auto">
{`llm:
  enabled: true
  model: phi4-mini
  offline: true`}
                  </pre>
                </div>
              </div>
            </div>

            {/* Integration Mode */}
            <div className="border border-border rounded-lg p-6 bg-card">
              <div className="flex items-start gap-3 mb-4">
                <div className="flex-shrink-0 w-10 h-10 rounded-lg bg-purple-500/20 flex items-center justify-center">
                  <span className="text-purple-400 font-bold">3</span>
                </div>
                <div>
                  <h2 className="text-2xl font-semibold mb-1">Integration Mode</h2>
                  <p className="text-sm text-muted-foreground">Default + optional network integrations</p>
                </div>
              </div>

              <div className="space-y-3 text-sm mb-4">
                <div>
                  <p className="font-semibold text-foreground mb-1">✓ What's added:</p>
                  <ul className="list-disc list-inside space-y-1 text-muted-foreground">
                    <li>GitHub Checks integration (requires Teams tier)</li>
                    <li>GitHub inline PR comments (requires Teams tier)</li>
                    <li>Slack notifications (optional)</li>
                    <li>Jira issue linking (optional)</li>
                    <li>Telemetry and usage analytics (optional, disabled by default)</li>
                    <li>MCP protocol support (Pro tier feature)</li>
                  </ul>
                </div>

                <div>
                  <p className="font-semibold text-foreground mb-1">🔒 Privacy characteristics:</p>
                  <p className="text-muted-foreground">
                    Code and analysis results are sent only to explicitly configured services. You control which integrations are active.
                    GitHub Checks requires authentication tokens. Telemetry is opt-in and disabled by default.
                  </p>
                </div>

                <div>
                  <p className="font-semibold text-foreground mb-1">⚙️ Configuration:</p>
                  <p className="text-muted-foreground">
                    Requires authentication tokens and service endpoints. Configure per integration:
                  </p>
                  <pre className="bg-muted p-2 rounded mt-2 text-xs overflow-x-auto">
{`integrations:
  github:
    enabled: true
    token: \$GITHUB_TOKEN
  slack:
    enabled: true
    webhook: \$SLACK_WEBHOOK_URL
  telemetry:
    enabled: false`}
                  </pre>
                </div>
              </div>
            </div>

            {/* CI AI Mode */}
            <div className="border border-border rounded-lg p-6 bg-card">
              <div className="flex items-start gap-3 mb-4">
                <div className="flex-shrink-0 w-10 h-10 rounded-lg bg-orange-500/20 flex items-center justify-center">
                  <span className="text-orange-400 font-bold">4</span>
                </div>
                <div>
                  <h2 className="text-2xl font-semibold mb-1">CI AI Mode</h2>
                  <p className="text-sm text-muted-foreground">Default + remote LLM and integrations</p>
                </div>
              </div>

              <div className="space-y-3 text-sm mb-4">
                <div>
                  <p className="font-semibold text-foreground mb-1">✓ What's added:</p>
                  <ul className="list-disc list-inside space-y-1 text-muted-foreground">
                    <li>Remote OpenAI-compatible LLM endpoint</li>
                    <li>Advanced LLM explanations and enrichment</li>
                    <li>All features from Integration Mode</li>
                  </ul>
                </div>

                <div>
                  <p className="font-semibold text-foreground mb-1">🔒 Privacy characteristics:</p>
                  <p className="text-muted-foreground">
                    Code snippets and analysis findings are sent to your configured LLM endpoint (e.g., OpenAI API, Azure OpenAI, self-hosted).
                    Choose a provider that meets your compliance requirements. Diffs are not sent by default—only relevant code snippets for explanation.
                  </p>
                </div>

                <div>
                  <p className="font-semibold text-foreground mb-1">⚙️ Configuration:</p>
                  <p className="text-muted-foreground">
                    Requires LLM API key and endpoint configuration:
                  </p>
                  <pre className="bg-muted p-2 rounded mt-2 text-xs overflow-x-auto">
{`llm:
  enabled: true
  provider: openai
  api_key: \$OPENAI_API_KEY
  model: gpt-4
  endpoint: https://api.openai.com/v1
  
integrations:
  github:
    enabled: true
    token: \$GITHUB_TOKEN`}
                  </pre>
                </div>
              </div>
            </div>
          </div>
        </section>

        {/* Comparison Table */}
        <section className="space-y-4">
          <h2 className="text-2xl font-semibold">Feature Comparison</h2>
          <div className="overflow-x-auto border border-border rounded-lg">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-border bg-muted">
                  <th className="text-left p-3 font-semibold">Feature</th>
                  <th className="text-center p-3 font-semibold">Default</th>
                  <th className="text-center p-3 font-semibold">Local AI</th>
                  <th className="text-center p-3 font-semibold">Integration</th>
                  <th className="text-center p-3 font-semibold">CI AI</th>
                </tr>
              </thead>
              <tbody>
                <tr className="border-b border-border">
                  <td className="p-3">Built-in rules (deterministic)</td>
                  <td className="text-center">✓</td>
                  <td className="text-center">✓</td>
                  <td className="text-center">✓</td>
                  <td className="text-center">✓</td>
                </tr>
                <tr className="border-b border-border">
                  <td className="p-3">Local execution</td>
                  <td className="text-center">✓</td>
                  <td className="text-center">✓</td>
                  <td className="text-center">✓</td>
                  <td className="text-center">✓</td>
                </tr>
                <tr className="border-b border-border">
                  <td className="p-3">Network-free</td>
                  <td className="text-center">✓</td>
                  <td className="text-center">✓</td>
                  <td className="text-center">✗</td>
                  <td className="text-center">✗</td>
                </tr>
                <tr className="border-b border-border">
                  <td className="p-3">Offline LLM enrichment</td>
                  <td className="text-center">✗</td>
                  <td className="text-center">✓</td>
                  <td className="text-center">✗</td>
                  <td className="text-center">✓</td>
                </tr>
                <tr className="border-b border-border">
                  <td className="p-3">GitHub Checks (Teams tier)</td>
                  <td className="text-center">✗</td>
                  <td className="text-center">✗</td>
                  <td className="text-center">✓</td>
                  <td className="text-center">✓</td>
                </tr>
                <tr className="border-b border-border">
                  <td className="p-3">Slack / Jira integrations</td>
                  <td className="text-center">✗</td>
                  <td className="text-center">✗</td>
                  <td className="text-center">✓</td>
                  <td className="text-center">✓</td>
                </tr>
                <tr className="border-b border-border">
                  <td className="p-3">Remote LLM endpoint</td>
                  <td className="text-center">✗</td>
                  <td className="text-center">✗</td>
                  <td className="text-center">✗</td>
                  <td className="text-center">✓</td>
                </tr>
                <tr>
                  <td className="p-3">MCP protocol (Pro tier)</td>
                  <td className="text-center">✗</td>
                  <td className="text-center">✗</td>
                  <td className="text-center">✓</td>
                  <td className="text-center">✓</td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>

        {/* Decision Guide */}
        <section className="space-y-4">
          <h2 className="text-2xl font-semibold">Choosing Your Mode</h2>
          
          <div className="rounded-lg border border-cyan-500/30 bg-cyan-500/5 p-4 mb-4">
            <p className="text-sm font-semibold text-cyan-400 mb-2">📋 Rule Types</p>
            <p className="text-sm text-muted-foreground mb-2">
              <strong>Built-in rules (30+ deterministic):</strong> Run without any LLM. Results are identical across runs. Always enabled.
            </p>
            <p className="text-sm text-muted-foreground">
              <strong>Experimental policy rules (optional):</strong> Defined in markdown, evaluated via LLM. Opt-in via <code className="bg-muted px-1 rounded text-xs">experimental.engineeringPolicy</code>.
            </p>
          </div>
          
          <div className="space-y-3">
            <div className="border-l-2 border-cyan-400 pl-4">
              <p className="font-semibold mb-1">🔒 Highest privacy + simplicity</p>
              <p className="text-muted-foreground text-sm">
                Use <span className="font-mono bg-muted px-1 rounded text-xs">Default</span> mode. Analysis runs entirely on your machine. No configuration needed. No network calls.
              </p>
            </div>
            <div className="border-l-2 border-blue-400 pl-4">
              <p className="font-semibold mb-1">🤖 Richer insights + local privacy</p>
              <p className="text-muted-foreground text-sm">
                Use <span className="font-mono bg-muted px-1 rounded text-xs">Local AI</span> mode. Adds explanations and context from an offline model. Still fully local and private.
              </p>
            </div>
            <div className="border-l-2 border-purple-400 pl-4">
              <p className="font-semibold mb-1">🔗 Team workflow integration</p>
              <p className="text-muted-foreground text-sm">
                Use <span className="font-mono bg-muted px-1 rounded text-xs">Integration</span> mode. Integrates with GitHub Checks, Slack, Jira. Requires GitHub Teams tier for Checks and inline comments. Enable only the integrations you need.
              </p>
            </div>
            <div className="border-l-2 border-orange-400 pl-4">
              <p className="font-semibold mb-1">✨ Maximum intelligence + remote services</p>
              <p className="text-muted-foreground text-sm">
                Use <span className="font-mono bg-muted px-1 rounded text-xs">CI AI</span> mode. Adds remote LLM explanations and all integrations. Requires API keys for OpenAI or compatible endpoint. Requires GitHub Teams tier for Checks.
              </p>
            </div>
          </div>
        </section>

        {/* Next Steps */}
        <section className="space-y-4 p-4 rounded-lg bg-muted/50 border border-border">
          <h2 className="text-lg font-semibold">Next Steps</h2>
          <ul className="space-y-2 text-sm">
            <li>
              <Link href="/docs/configuration" className="text-cyan-400 hover:text-cyan-300 underline">
                Configuration Guide
              </Link>
              {" "}— Learn how to configure each mode
            </li>
            <li>
              <Link href="/docs/integrations" className="text-cyan-400 hover:text-cyan-300 underline">
                Integrations
              </Link>
              {" "}— Set up GitHub, Slack, Jira, and MCP
            </li>
            <li>
              <Link href="/docs/local-llm" className="text-cyan-400 hover:text-cyan-300 underline">
                Local LLM Setup
              </Link>
              {" "}— Install and configure offline models
            </li>
          </ul>
        </section>
      </div>
    </>
  );
}

import type { Metadata } from "next";

export const metadata: Metadata = {
  title: "Configuration | GauntletCI Docs",
  description: "Reference for .gauntletci.json — per-repository configuration for GauntletCI.",
  alternates: { canonical: "/docs/configuration" },
};

export default function ConfigurationPage() {
  return (
    <div className="space-y-10">
      <div>
        <p className="text-sm font-semibold text-cyan-400 uppercase tracking-widest mb-2">Configuration</p>
        <h1 className="text-4xl font-bold tracking-tight mb-4">Configuration Reference</h1>
        <p className="text-lg text-muted-foreground">
          GauntletCI is zero-config by default. Place a <code className="bg-muted px-1 rounded text-sm">.gauntletci.json</code> file
          at your repository root to customize behavior.
        </p>
      </div>

      <section>
        <h2 className="text-2xl font-semibold mb-4">Minimal example</h2>
        <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm">
          <pre className="text-foreground whitespace-pre">{`{
  "rules": {
    "GCI0001": { "enabled": false }
  },
  "exitOn": "Block"
}`}</pre>
        </div>
      </section>

      <section>
        <h2 className="text-2xl font-semibold mb-4">Full example</h2>
        <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm overflow-x-auto">
          <pre className="text-foreground whitespace-pre">{`{
  "rules": {
    "GCI0001": { "enabled": false },
    "GCI0014": { "enabled": true, "severity": "Warn" }
  },
  "exitOn": "Block",
  "llm": {
    "model": "phi4-mini:latest",
    "embeddingOllamaUrl": "http://localhost:11434"
  },
  "experimental": {
    "engineeringPolicy": {
      "enabled": true,
      "path": "docs/engineering-rules.md"
    }
  }
}`}</pre>
        </div>
      </section>

      <section>
        <h2 className="text-2xl font-semibold mb-6">Options</h2>
        <div className="space-y-6">

          <div className="rounded-lg border border-border p-5">
            <div className="flex items-center gap-3 mb-2">
              <code className="text-cyan-400 font-mono text-sm">rules</code>
              <span className="text-xs text-muted-foreground">object</span>
            </div>
            <p className="text-sm text-muted-foreground">
              Per-rule configuration. Keys are rule IDs (e.g. <code className="bg-muted px-1 rounded text-xs">GCI0001</code>).
              Each value supports <code className="bg-muted px-1 rounded text-xs">enabled</code> (boolean) and
              optionally <code className="bg-muted px-1 rounded text-xs">severity</code> (<code className="bg-muted px-1 rounded text-xs">Block</code> or <code className="bg-muted px-1 rounded text-xs">Warn</code>).
            </p>
          </div>

          <div className="rounded-lg border border-border p-5">
            <div className="flex items-center gap-3 mb-2">
              <code className="text-cyan-400 font-mono text-sm">exitOn</code>
              <span className="text-xs text-muted-foreground">string — default: "Block"</span>
            </div>
            <p className="text-sm text-muted-foreground">
              Controls which finding severity causes a non-zero exit code.
              <code className="bg-muted px-1 rounded text-xs ml-1">Block</code> exits 1 only on blocking findings;
              <code className="bg-muted px-1 rounded text-xs ml-1">Warn</code> exits 1 on warnings too.
            </p>
          </div>

          <div className="rounded-lg border border-border p-5">
            <div className="flex items-center gap-3 mb-2">
              <code className="text-cyan-400 font-mono text-sm">llm.model</code>
              <span className="text-xs text-muted-foreground">string — default: "phi4-mini:latest"</span>
            </div>
            <p className="text-sm text-muted-foreground">
              The Ollama model used for both LLM enrichment (<code className="bg-muted px-1 rounded text-xs">--with-llm</code>) and
              expert-context embeddings. Defaults to <code className="bg-muted px-1 rounded text-xs">phi4-mini:latest</code>.
            </p>
          </div>

          <div className="rounded-lg border border-border p-5">
            <div className="flex items-center gap-3 mb-2">
              <code className="text-cyan-400 font-mono text-sm">llm.embeddingOllamaUrl</code>
              <span className="text-xs text-muted-foreground">string</span>
            </div>
            <p className="text-sm text-muted-foreground">
              Base URL of the Ollama server. Defaults to <code className="bg-muted px-1 rounded text-xs">http://localhost:11434</code>.
            </p>
          </div>

          <div className="rounded-lg border border-border p-5">
            <div className="flex items-center gap-3 mb-2">
              <code className="text-cyan-400 font-mono text-sm">forbiddenImports</code>
              <span className="text-xs text-muted-foreground">object</span>
            </div>
            <p className="text-sm text-muted-foreground">
              Per-layer forbidden import rules for GCI0035. Key: source namespace fragment.
              Value: list of forbidden target namespace fragments.
            </p>
            <div className="mt-3 rounded-lg border border-border bg-card p-3 font-mono text-xs">
              <pre className="text-foreground whitespace-pre">{`"forbiddenImports": {
  "Api": ["Infrastructure", "Data"],
  "Domain": ["Infrastructure"]
}`}</pre>
            </div>
          </div>

          <div className="rounded-lg border border-border p-5">
            <div className="flex items-center gap-3 mb-2">
              <code className="text-cyan-400 font-mono text-sm">experimental.engineeringPolicy</code>
              <span className="text-xs text-muted-foreground">object</span>
            </div>
            <p className="text-sm text-muted-foreground">
              Enable engineering policy enforcement. Point <code className="bg-muted px-1 rounded text-xs">path</code> at a markdown
              file containing your team's rules. GauntletCI will evaluate diffs against them using the local LLM.
            </p>
          </div>

        </div>
      </section>

      <section>
        <h2 className="text-2xl font-semibold mb-4">Suppressing findings with a baseline</h2>
        <p className="text-muted-foreground mb-3">
          To suppress pre-existing findings in a legacy codebase, capture a baseline. Future runs will only
          report net-new risks introduced after the snapshot.
        </p>
        <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm">
          <span className="text-cyan-400">$</span>{" "}
          <span className="text-foreground">gauntletci baseline capture --staged</span>
        </div>
        <p className="mt-3 text-sm text-muted-foreground">
          This writes <code className="bg-muted px-1 rounded text-xs">.gauntletci-baseline.json</code> to your repo root.
          Commit it to share the baseline with your team.
        </p>
      </section>
    </div>
  );
}

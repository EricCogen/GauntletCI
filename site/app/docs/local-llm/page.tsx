import type { Metadata } from "next";

export const metadata: Metadata = {
  title: "Local LLM Setup | GauntletCI Docs",
  description: "Set up local LLM enrichment with Ollama and phi4-mini for offline AI explanations.",
};

export default function LocalLlmPage() {
  return (
    <div className="space-y-10">
      <div>
        <p className="text-sm font-semibold text-cyan-400 uppercase tracking-widest mb-2">Local LLM Setup</p>
        <h1 className="text-4xl font-bold tracking-tight mb-4">Local LLM Setup</h1>
        <p className="text-lg text-muted-foreground">
          GauntletCI can enrich high-confidence findings with plain-English explanations using a locally
          hosted LLM. No code is sent to any external service; the model runs entirely on your machine.
        </p>
      </div>

      <section className="rounded-lg border border-cyan-500/30 bg-cyan-500/5 p-5">
        <p className="text-sm font-semibold text-cyan-400 mb-1">Optional feature</p>
        <p className="text-sm text-muted-foreground">
          Local LLM enrichment is opt-in and requires a running Ollama instance. The detection engine
          is fully deterministic and does not require LLM to function. LLM only adds explanations.
        </p>
      </section>

      <section>
        <h2 className="text-2xl font-semibold mb-4">Prerequisites</h2>
        <ul className="space-y-2 text-sm text-muted-foreground">
          <li className="flex items-start gap-2">
            <span className="text-cyan-400 mt-0.5">1.</span>
            <span>
              <a href="https://ollama.com" target="_blank" rel="noopener noreferrer" className="text-cyan-400 hover:underline">Ollama</a> installed and running on your machine or local network
            </span>
          </li>
          <li className="flex items-start gap-2">
            <span className="text-cyan-400 mt-0.5">2.</span>
            <span>The <code className="bg-muted px-1 rounded text-xs">phi4-mini:latest</code> model pulled (or another model of your choice)</span>
          </li>
          <li className="flex items-start gap-2">
            <span className="text-cyan-400 mt-0.5">3.</span>
            <span>A Pro or higher GauntletCI license to enable the feature</span>
          </li>
        </ul>
      </section>

      <section>
        <h2 className="text-2xl font-semibold mb-4">Install Ollama and pull the model</h2>
        <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm space-y-2">
          <p className="text-muted-foreground"># Install Ollama (macOS)</p>
          <p><span className="text-cyan-400">$</span> <span className="text-foreground">brew install ollama</span></p>
          <p className="text-muted-foreground mt-3"># Start the Ollama server</p>
          <p><span className="text-cyan-400">$</span> <span className="text-foreground">ollama serve</span></p>
          <p className="text-muted-foreground mt-3"># Pull the default model</p>
          <p><span className="text-cyan-400">$</span> <span className="text-foreground">ollama pull phi4-mini:latest</span></p>
        </div>
        <p className="mt-3 text-sm text-muted-foreground">
          For Windows and Linux installers, see <a href="https://ollama.com/download" target="_blank" rel="noopener noreferrer" className="text-cyan-400 hover:underline">ollama.com/download</a>.
        </p>
      </section>

      <section>
        <h2 className="text-2xl font-semibold mb-4">Run with LLM enrichment</h2>
        <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm">
          <span className="text-cyan-400">$</span>{" "}
          <span className="text-foreground">gauntletci analyze --staged --with-llm</span>
        </div>
        <p className="mt-3 text-sm text-muted-foreground">
          High-confidence findings will include a plain-English explanation of the risk and a suggested action,
          generated locally by the model. Analysis adds 2-5 seconds per finding depending on hardware.
        </p>
      </section>

      <section>
        <h2 className="text-2xl font-semibold mb-4">Configuration</h2>
        <p className="text-muted-foreground mb-3">
          Override the model or Ollama URL in <code className="bg-muted px-1 rounded text-xs">.gauntletci.json</code>:
        </p>
        <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm">
          <pre className="text-foreground whitespace-pre">{`{
  "llm": {
    "model": "phi4-mini:latest",
    "embeddingOllamaUrl": "http://localhost:11434"
  }
}`}</pre>
        </div>
        <p className="mt-3 text-sm text-muted-foreground">
          Any model available in your Ollama instance can be used. <code className="bg-muted px-1 rounded text-xs">phi4-mini:latest</code> is
          the default; it balances quality and speed on most developer hardware.
        </p>
      </section>

      <section>
        <h2 className="text-2xl font-semibold mb-4">Verify your setup</h2>
        <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm">
          <span className="text-cyan-400">$</span>{" "}
          <span className="text-foreground">gauntletci doctor</span>
        </div>
        <p className="mt-3 text-sm text-muted-foreground">
          The doctor command checks that Ollama is reachable, the configured model is available,
          and the LLM feature is ready to use.
        </p>
      </section>

      <section>
        <h2 className="text-2xl font-semibold mb-4">Privacy</h2>
        <p className="text-sm text-muted-foreground">
          The LLM feature makes no network calls outside your local machine (or your configured Ollama host).
          No diff content, file paths, or findings are transmitted to any external service.
          This makes the feature safe for air-gapped environments and codebases with strict data residency requirements.
        </p>
      </section>
    </div>
  );
}

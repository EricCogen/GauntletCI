import type { Metadata } from "next";

export const metadata: Metadata = {
  title: "Local LLM Setup | GauntletCI Docs",
  description: "Set up local LLM enrichment for offline AI-powered explanations. No Ollama required - GauntletCI includes a built-in ONNX engine with Phi-4 Mini.",
  alternates: { canonical: "/docs/local-llm" },
};

const jsonLd = {
  "@context": "https://schema.org",
  "@type": "TechArticle",
  "headline": "GauntletCI Local LLM Setup",
  "description": "Set up local LLM enrichment for offline AI-powered explanations. No Ollama required - GauntletCI includes a built-in ONNX engine with Phi-4 Mini.",
  "url": "https://gauntletci.com/docs/local-llm",
  "publisher": { "@type": "Organization", "name": "GauntletCI", "url": "https://gauntletci.com" },
};

export default function LocalLlmPage() {
  return (
    <>
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLd) }} />
      <div className="space-y-10">
      <div>
        <p className="text-sm font-semibold text-cyan-400 uppercase tracking-widest mb-2">Local LLM Setup</p>
        <h1 className="text-4xl font-bold tracking-tight mb-4">Local LLM Setup</h1>
        <p className="text-lg text-muted-foreground">
          GauntletCI can enrich high-confidence findings with plain-English explanations using a locally
          hosted model. No code is sent to any external service. The model runs entirely on your machine.
        </p>
      </div>

      <section className="rounded-lg border border-cyan-500/30 bg-cyan-500/5 p-5">
        <p className="text-sm font-semibold text-cyan-400 mb-1">Optional feature</p>
        <p className="text-sm text-muted-foreground">
          LLM enrichment is opt-in. The detection engine is fully deterministic and does not require
          a model to function. LLM only adds plain-English explanations to findings.
        </p>
      </section>

      {/* ---- Path 1: Built-in ONNX ---- */}
      <section>
        <h2 className="text-2xl font-semibold mb-1">Option 1: Built-in ONNX engine</h2>
        <p className="text-sm text-muted-foreground mb-4">
          No external runtime required. GauntletCI ships with a built-in ONNX inference engine
          powered by <code className="bg-muted px-1 rounded text-xs">Microsoft.ML.OnnxRuntimeGenAI</code>.
          You only need to download the model once.
        </p>

        <h3 className="text-base font-semibold mb-2">Step 1: Download the model</h3>
        <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm mb-3">
          <p className="text-muted-foreground"># Downloads Phi-4 Mini INT4 (~2 GB) from HuggingFace</p>
          <p><span className="text-cyan-400">$</span> <span className="text-foreground">gauntletci model download</span></p>
        </div>
        <p className="text-sm text-muted-foreground mb-5">
          The model is cached to <code className="bg-muted px-1 rounded text-xs">~/.gauntletci/models/phi4-mini/</code> and
          only needs to be downloaded once. Subsequent runs load from the local cache.
        </p>

        <h3 className="text-base font-semibold mb-2">Step 2: Run analysis with enrichment</h3>
        <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm mb-3">
          <span className="text-cyan-400">$</span>{" "}
          <span className="text-foreground">gauntletci analyze --staged --with-llm</span>
        </div>
        <p className="text-sm text-muted-foreground">
          High-confidence findings include a plain-English explanation of the risk and a suggested
          action. The ONNX engine runs fully in-process - no external runtime or service is required.
        </p>
      </section>

      <section className="rounded-lg border border-border bg-card/40 p-5 space-y-3">
        <p className="text-sm font-semibold mb-1">How the daemon works (and when it does not matter)</p>
        <p className="text-sm text-muted-foreground">
          On a developer machine, GauntletCI keeps the model loaded in a background daemon between runs.
          This avoids a 2-3 second reload on each invocation when you run <code className="bg-muted px-1 rounded text-xs">gauntletci analyze</code> multiple times in a session.
        </p>
        <p className="text-sm text-muted-foreground">
          The daemon is a local-dev optimization only. If it cannot be started, GauntletCI loads
          <code className="bg-muted px-1 rounded text-xs">LocalLlmEngine</code> directly in-process as a fallback.
          The daemon is never used in CI/CD - see the section below on CI/CD usage.
        </p>
      </section>

      <section className="rounded-lg border border-border bg-card/40 p-5 space-y-2">
        <p className="text-sm font-semibold mb-1">Hardware acceleration</p>
        <p className="text-sm text-muted-foreground">
          On Windows the ONNX engine uses DirectML to run on the GPU automatically.
          On macOS and Linux it falls back to CPU. No driver or CUDA setup required.
        </p>
      </section>

      <section>
        <h3 className="text-base font-semibold mb-2">Custom model path (optional)</h3>
        <p className="text-sm text-muted-foreground mb-3">
          Override the default model directory in <code className="bg-muted px-1 rounded text-xs">.gauntletci.json</code>:
        </p>
        <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm">
          <pre className="text-foreground whitespace-pre">{`{
  "llm": {
    "modelPath": "~/.gauntletci/models/phi4-mini"
  }
}`}</pre>
        </div>
      </section>

      {/* ---- Path 2: Ollama ---- */}
      <div className="border-t border-border pt-8">
        <h2 className="text-2xl font-semibold mb-1">Option 2: Ollama (if you already run Ollama)</h2>
        <p className="text-sm text-muted-foreground mb-4">
          If you already have an Ollama instance running locally or on your network, GauntletCI can use
          it instead of the built-in ONNX engine. This is useful for teams that share a single Ollama
          server or prefer to manage models via Ollama.
        </p>

        <h3 className="text-base font-semibold mb-2">Step 1: Pull the model in Ollama</h3>
        <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm space-y-2 mb-5">
          <p className="text-muted-foreground"># Start Ollama if not already running</p>
          <p><span className="text-cyan-400">$</span> <span className="text-foreground">ollama serve</span></p>
          <p className="text-muted-foreground mt-3"># Pull the default model</p>
          <p><span className="text-cyan-400">$</span> <span className="text-foreground">ollama pull phi4-mini:latest</span></p>
        </div>

        <h3 className="text-base font-semibold mb-2">Step 2: Configure the Ollama endpoint</h3>
        <p className="text-sm text-muted-foreground mb-3">
          Add an Ollama endpoint to <code className="bg-muted px-1 rounded text-xs">.gauntletci.json</code>.
          When this is set, GauntletCI uses Ollama instead of the local ONNX engine.
        </p>
        <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm mb-3">
          <pre className="text-foreground whitespace-pre">{`{
  "llm": {
    "model": "phi4-mini:latest"
  },
  "corpus": {
    "ollamaEndpoints": [
      { "url": "http://localhost:11434", "enabled": true }
    ]
  }
}`}</pre>
        </div>
        <p className="text-sm text-muted-foreground">
          Any Ollama-hosted model can be used. <code className="bg-muted px-1 rounded text-xs">phi4-mini:latest</code> is
          the recommended default. For Ollama installation, see{" "}
          <a href="https://ollama.com/download" target="_blank" rel="noopener noreferrer" className="text-cyan-400 hover:underline">ollama.com/download</a>.
        </p>
      </div>

      {/* ---- CI/CD ---- */}
      <section className="border-t border-border pt-8">
        <h2 className="text-2xl font-semibold mb-3">Using --with-llm in CI/CD</h2>

        <div className="rounded-lg border border-amber-500/30 bg-amber-500/5 p-5 mb-5">
          <p className="text-sm font-semibold text-amber-400 mb-1">ONNX is not available in CI/CD</p>
          <p className="text-sm text-muted-foreground">
            Loading a 2 GB model in an ephemeral CI runner is impractical. GauntletCI
            enforces this: when CI environment variables are detected (<code className="bg-muted px-1 rounded text-xs">CI</code>,{" "}
            <code className="bg-muted px-1 rounded text-xs">GITHUB_ACTIONS</code>,{" "}
            <code className="bg-muted px-1 rounded text-xs">TF_BUILD</code>, etc.), the ONNX
            engine is bypassed entirely. Only remote LLM endpoints are used in CI.
          </p>
        </div>

        <p className="text-sm text-muted-foreground mb-4">
          To use <code className="bg-muted px-1 rounded text-xs">--with-llm</code> in a CI pipeline,
          configure a remote OpenAI-compatible endpoint and a license key in your repository config or
          as environment variables:
        </p>

        <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm mb-4">
          <pre className="text-foreground whitespace-pre">{`{
  "llm": {
    "ciEndpoint": "https://api.openai.com/v1",
    "ciModel": "gpt-4o-mini"
  }
}`}</pre>
        </div>

        <p className="text-sm text-muted-foreground mb-4">
          Set the API key as a secret in your CI environment:
        </p>
        <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm mb-5">
          <p><span className="text-cyan-400">$</span> <span className="text-foreground">GAUNTLETCI_LLM_API_KEY=sk-...</span></p>
        </div>

        <div className="rounded-lg border border-red-500/20 bg-red-500/5 p-5">
          <p className="text-sm font-semibold text-red-400 mb-1">Loud warning if misconfigured</p>
          <p className="text-sm text-muted-foreground">
            If <code className="bg-muted px-1 rounded text-xs">--with-llm</code> is passed in CI
            but no endpoint or API key is configured, GauntletCI prints a structured warning to
            stderr and skips enrichment. It does not silently no-op. Analysis still completes and
            all deterministic findings are still reported.
          </p>
        </div>
      </section>

      {/* ---- Privacy ---- */}
      <section className="border-t border-border pt-8">
        <h2 className="text-2xl font-semibold mb-4">Privacy</h2>
        <p className="text-sm text-muted-foreground">
          Neither engine makes network calls at analysis time. The ONNX engine runs entirely in-process.
          The Ollama path calls only your configured local host. No diff content, file paths, or findings
          are transmitted to any external service. Both options are safe for air-gapped environments and
          codebases with strict data residency requirements.
        </p>
      </section>
    </div>
    </>
  );
}

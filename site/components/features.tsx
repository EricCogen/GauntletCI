import { Zap, Lock, Target, Server, Eye, Shield, Container, BrainCircuit, FileCode2, CheckCircle2 } from "lucide-react";

export function Features() {
  const features = [
    {
      icon: Zap,
      title: "Sub-second analysis",
      description: "No compilation, no AST, no network. Get instant feedback on your changes before the commit is created.",
    },
    {
      icon: Eye,
      title: "Diff-first detection",
      description: "Analyzes only what changed. No style or formatting checks, behavioral risk only, scoped to the exact lines you touched.",
    },
    {
      icon: Lock,
      title: "100% local execution",
      description: "No code leaves your machine. Auto-redaction prevents sensitive data exposure. Works air-gapped.",
    },
    {
      icon: Server,
      title: "Baseline delta mode",
      description: "Snapshot existing findings and suppress them. Subsequent runs show only net-new risks, no legacy noise.",
    },
    {
      icon: Target,
      title: "High-signal output",
      description: "Up to 3 findings per run by design. No alert fatigue. Output developers actually read every time.",
    },
    {
      icon: Container,
      title: "Docker deployment",
      description: "Official runtime image for CI pipelines, self-hosted runners, and air-gapped environments. Pull and run.",
    },
    {
      icon: BrainCircuit,
      title: "MCP server",
      description: "Expose GauntletCI as a tool to Claude, Cursor, GitHub Copilot, and Windsurf. Ask your AI assistant about risk.",
    },
    {
      icon: FileCode2,
      title: "SARIF output",
      description: "Emit findings as SARIF for GitHub Security tab, IDE diagnostics, and any SARIF-compatible pipeline tool.",
    },
    {
      icon: CheckCircle2,
      title: "GitHub Checks integration",
      description: "Post findings as GitHub Checks with inline annotations on the exact diff lines that triggered them.",
    },
    {
      icon: Shield,
      title: "Privacy-focused",
      description: "All analysis runs locally. Telemetry is optional and anonymous. Never includes code, diffs, or findings.",
    },
  ];

  return (
    <section id="features" className="py-20 sm:py-28">
      <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
        <div className="text-center mb-16">
          <h2 className="text-3xl sm:text-4xl font-bold tracking-tight text-balance">
            Designed for high signal
          </h2>
          <p className="mt-4 text-lg text-muted-foreground max-w-2xl mx-auto text-pretty">
            GauntletCI avoids noise by design. Focus on what matters: behavioral changes that
            could slip through code review.
          </p>
        </div>

        <div className="grid gap-6 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-5">
          {features.map((feature, index) => (
            <div
              key={index}
              className="group rounded-lg border border-border bg-card p-6 transition-colors hover:border-cyan-500/50 hover:bg-card/80"
            >
              <div className="mb-4 inline-flex h-10 w-10 items-center justify-center rounded-md bg-gradient-to-br from-cyan-500/20 to-blue-500/20 border border-cyan-500/30">
                <feature.icon className="h-5 w-5 text-cyan-400" />
              </div>
              <h3 className="text-base font-semibold">{feature.title}</h3>
              <p className="mt-2 text-sm text-muted-foreground leading-relaxed">
                {feature.description}
              </p>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}


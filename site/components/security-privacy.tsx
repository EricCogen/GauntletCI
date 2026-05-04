import { ShieldCheck, ServerOff, BrainCircuit, Lock } from "lucide-react";

const pillars = [
  {
    icon: ServerOff,
    title: "No Source Code Uploads",
    body: "Analysis runs entirely on your local hardware or within your private CI/CD runners. Your source code never touches an external server.",
  },
  {
    icon: BrainCircuit,
    title: "No LLM Training",
    body: "We never use your code to train models. LLM enrichment is optional, local by default, and powered by models you control.",
  },
  {
    icon: Lock,
    title: "Deterministic Core",
    body: "The blocking engine is pure Roslyn, no probabilistic guesses. Every finding is reproducible, auditable, and explainable.",
  },
];

export function SecurityPrivacy() {
  return (
    <section id="security" className="py-20 sm:py-28 bg-card/30">
      <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
        <div className="rounded-2xl border border-cyan-500/20 bg-gradient-to-br from-card/80 via-card/60 to-cyan-950/20 p-8 sm:p-12 lg:p-16">
          <div className="grid gap-10 lg:grid-cols-2 lg:gap-16 items-center">
            {/* Left - headline */}
            <div>
              <div className="inline-flex items-center gap-2 rounded-full border border-cyan-500/30 bg-cyan-500/10 px-4 py-1.5 text-xs font-semibold uppercase tracking-widest text-cyan-400 mb-6">
                <ShieldCheck className="h-3.5 w-3.5" />
                Security &amp; Privacy
              </div>
              <h2 className="text-3xl sm:text-4xl font-bold tracking-tight text-balance leading-tight">
                Local-First.{" "}
                <span className="text-cyan-400">Privacy-Always.</span>
              </h2>
              <p className="mt-5 text-lg text-muted-foreground text-pretty">
                GauntletCI runs entirely on your own hardware—no code uploads, no external services. This local-first architecture makes it the only behavioral auditor built for air-gapped, data-residency, and zero-trust networks. Perfect for high-security environments, regulated industries, and teams that never compromise on code privacy.
              </p>
              <div className="mt-6 inline-flex items-center gap-2 text-sm text-emerald-400 font-medium">
                <ShieldCheck className="h-4 w-4" />
                Designed to be trusted in air-gapped, HIPAA-adjacent, and SOC 2 environments
              </div>
            </div>

            {/* Right - pillars */}
            <div className="space-y-5">
              {pillars.map(({ icon: Icon, title, body }) => (
                <div
                  key={title}
                  className="flex gap-4 rounded-xl border border-border/60 bg-card/50 p-5 transition-colors hover:border-cyan-500/40"
                >
                  <div className="mt-0.5 flex-shrink-0">
                    <div className="flex h-9 w-9 items-center justify-center rounded-lg bg-cyan-500/10 border border-cyan-500/20">
                      <Icon className="h-4.5 w-4.5 text-cyan-400" />
                    </div>
                  </div>
                  <div>
                    <p className="text-sm font-semibold text-foreground">{title}</p>
                    <p className="mt-1 text-sm text-muted-foreground leading-relaxed">{body}</p>
                  </div>
                </div>
              ))}
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}

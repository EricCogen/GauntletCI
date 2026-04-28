import { GitBranch, Zap, AlertTriangle, ShieldCheck } from "lucide-react";

const steps = [
  {
    number: "01",
    icon: GitBranch,
    title: "Stage your changes",
    body: "Write code as normal. When you're ready to commit, GauntletCI reads the staged diff directly - no compilation, no network, no setup.",
    color: "text-slate-400",
    bg: "bg-slate-500/10",
  },
  {
    number: "02",
    icon: Zap,
    title: "Analysis runs in under a second",
    body: "30 behavioral change rules evaluate only what changed. The rest of the codebase is not touched. No false positives from pre-existing issues.",
    color: "text-blue-400",
    bg: "bg-blue-500/10",
  },
  {
    number: "03",
    icon: AlertTriangle,
    title: "Up to 3 high-signal findings",
    body: "Each finding includes a rule ID, severity, the exact line, and a plain-English explanation of why the change is risky. No noise. No style warnings.",
    color: "text-amber-400",
    bg: "bg-amber-500/10",
  },
  {
    number: "04",
    icon: ShieldCheck,
    title: "Fix before the commit exists",
    body: "The risky change never reaches tests, review, or CI. You resolve it locally - the pipeline sees a clean diff from the start.",
    color: "text-cyan-400",
    bg: "bg-cyan-500/10",
    highlight: true,
  },
];

export function HowItWorks() {
  return (
    <section className="py-20 sm:py-28">
      <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
        <div className="text-center mb-14">
          <p className="text-sm font-semibold text-cyan-400 uppercase tracking-widest mb-4">
            How it works
          </p>
          <h2 className="text-3xl sm:text-4xl font-bold tracking-tight text-balance">
            Understand GauntletCI in 60 seconds
          </h2>
          <p className="mt-4 text-lg text-muted-foreground max-w-2xl mx-auto text-pretty">
            One command. Sub-second analysis. Findings before the commit exists.
          </p>
        </div>

        <div className="relative max-w-5xl mx-auto">
          {/* Connector line (desktop) */}
          <div className="hidden lg:block absolute top-10 left-[12.5%] right-[12.5%] h-px bg-border" aria-hidden="true" />

          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-6">
            {steps.map((step) => {
              const Icon = step.icon;
              return (
                <div
                  key={step.number}
                  className={`relative flex flex-col gap-4 rounded-xl border p-6 ${
                    step.highlight
                      ? "border-cyan-500/40 bg-cyan-500/5"
                      : "border-border bg-card"
                  }`}
                >
                  <div className="flex items-center gap-3">
                    <div className={`h-10 w-10 rounded-full flex items-center justify-center shrink-0 ${step.bg}`}>
                      <Icon className={`h-5 w-5 ${step.color}`} />
                    </div>
                    <span className={`text-xs font-mono font-bold tracking-widest ${step.highlight ? "text-cyan-400" : "text-muted-foreground"}`}>
                      {step.number}
                    </span>
                  </div>
                  <h3 className="font-semibold text-foreground leading-snug">
                    {step.title}
                  </h3>
                  <p className="text-sm text-muted-foreground leading-relaxed">
                    {step.body}
                  </p>
                </div>
              );
            })}
          </div>
        </div>

        <div className="mt-12 mx-auto max-w-2xl rounded-xl border border-border bg-muted/40 px-6 py-4">
          <p className="text-center text-sm font-mono text-muted-foreground">
            <span className="text-foreground/70">$</span>{" "}
            <span className="text-cyan-400">gauntletci analyze --staged</span>
          </p>
        </div>
      </div>
    </section>
  );
}

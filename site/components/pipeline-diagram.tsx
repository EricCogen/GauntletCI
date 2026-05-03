import { CheckCircle2, XCircle, ArrowRight } from "lucide-react";

const withoutSteps = [
  { label: "Write code", state: "neutral" },
  { label: "Tests pass", state: "pass" },
  { label: "Review approved", state: "pass" },
  { label: "CI green", state: "pass" },
  { label: "Ships to production", state: "fail", note: "Risky change included" },
];

const withSteps = [
  { label: "Write code", state: "neutral" },
  { label: "GauntletCI flags risk", state: "gate", note: "Pre-commit, local, sub-second" },
  { label: "Fix before committing", state: "fix" },
  { label: "Tests pass", state: "pass" },
  { label: "Review: intent only", state: "pass", note: "Behavioral risk already handled" },
  { label: "CI green", state: "pass" },
  { label: "Ships safely", state: "safe" },
];

function StepPill({ label, state, note }: { label: string; state: string; note?: string }) {
  const styles: Record<string, string> = {
    neutral: "border-border bg-card text-foreground",
    pass: "border-green-500/30 bg-green-500/10 text-green-400",
    fail: "border-red-500/40 bg-red-500/10 text-red-400",
    gate: "border-cyan-500/40 bg-cyan-500/10 text-cyan-400",
    fix: "border-amber-500/30 bg-amber-500/10 text-amber-400",
    safe: "border-emerald-500/40 bg-emerald-500/10 text-emerald-400",
  };
  return (
    <div className="flex flex-col items-center gap-1">
      <div className={`rounded-lg border px-3 py-2 text-xs font-medium text-center whitespace-nowrap ${styles[state]}`}>
        {state === "fail" && <XCircle className="inline h-3 w-3 mr-1 -mt-0.5" />}
        {state === "pass" && <CheckCircle2 className="inline h-3 w-3 mr-1 -mt-0.5" />}
        {state === "safe" && <CheckCircle2 className="inline h-3 w-3 mr-1 -mt-0.5" />}
        {label}
      </div>
      {note && (
        <span className="text-[10px] text-muted-foreground text-center leading-tight max-w-[96px]">
          {note}
        </span>
      )}
    </div>
  );
}

function PipelineRow({ steps, label }: { steps: typeof withoutSteps; label: string }) {
  return (
    <div className="space-y-3">
      <p className="text-xs font-semibold text-muted-foreground uppercase tracking-widest">{label}</p>
      <div className="flex flex-wrap items-start gap-2">
        {steps.map((step, i) => (
          <div key={step.label} className="flex items-center gap-2">
            <StepPill {...step} />
            {i < steps.length - 1 && (
              <ArrowRight className="h-3 w-3 text-muted-foreground/40 shrink-0 mt-0" />
            )}
          </div>
        ))}
      </div>
    </div>
  );
}

export function PipelineDiagram() {
  return (
    <section className="py-20 sm:py-28 bg-muted/30">
      <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
        <div className="text-center mb-14">
          <p className="text-sm font-semibold text-cyan-400 uppercase tracking-widest mb-4">
            Where it fits
          </p>
          <h2 className="text-3xl sm:text-4xl font-bold tracking-tight text-balance">
            GauntletCI runs before everything else
          </h2>
          <p className="mt-4 text-lg text-muted-foreground max-w-2xl mx-auto text-pretty">
            It does not replace tests, review, or CI. It runs before them - closing the gap
            that exists before any of those tools see the change.
          </p>
        </div>

        <div className="mx-auto max-w-5xl space-y-10">
          <div className="rounded-xl border border-red-500/20 bg-card p-6 sm:p-8">
            <PipelineRow steps={withoutSteps} label="Without GauntletCI" />
          </div>

          <div className="rounded-xl border border-cyan-500/30 bg-card p-6 sm:p-8">
            <PipelineRow steps={withSteps} label="With GauntletCI" />
          </div>
        </div>

        <div className="mt-10 mx-auto max-w-2xl text-center">
          <p className="text-sm text-muted-foreground">
            GauntletCI runs on the developer's machine in under a second. No full build required.
            No network. No code leaves the machine.
          </p>
        </div>
      </div>
    </section>
  );
}


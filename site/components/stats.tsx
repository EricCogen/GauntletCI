import { Zap, Target, ShieldCheck, Lock, GitCommit } from "lucide-react";

const stats = [
  {
    icon: Zap,
    value: "<1s",
    label: "Analysis time",
    sub: "No compilation. No network.",
    color: "text-cyan-400",
    bg: "bg-cyan-500/10",
  },
  {
    icon: Target,
    value: "≤3",
    label: "Findings per run",
    sub: "High-signal by design.",
    color: "text-violet-400",
    bg: "bg-violet-500/10",
  },
  {
    icon: ShieldCheck,
    value: "30",
    label: "Detection rules",
    sub: "GCI0001-GCI0053.",
    color: "text-emerald-400",
    bg: "bg-emerald-500/10",
  },
  {
    icon: Lock,
    value: "100%",
    label: "Local execution",
    sub: "Code never leaves your machine.",
    color: "text-amber-400",
    bg: "bg-amber-500/10",
  },
  {
    icon: GitCommit,
    value: "0",
    label: "Cloud dependencies",
    sub: "Works fully air-gapped.",
    color: "text-rose-400",
    bg: "bg-rose-500/10",
  },
];

export function Stats() {
  return (
    <section id="stats" className="border-y border-border bg-card/50">
      <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
        <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-5 divide-x divide-y sm:divide-y-0 divide-border">
          {stats.map((stat) => (
            <div
              key={stat.label}
              className="flex flex-col items-center justify-center gap-3 py-8 sm:py-10 px-4 text-center"
            >
              <div className={`flex h-10 w-10 items-center justify-center rounded-lg border border-border ${stat.bg}`}>
                <stat.icon className={`h-5 w-5 ${stat.color}`} />
              </div>
              <div>
                <div className={`text-3xl sm:text-4xl font-bold ${stat.color}`}>{stat.value}</div>
                <div className="mt-1 text-sm font-medium text-foreground/80">{stat.label}</div>
                <div className="mt-0.5 text-xs text-muted-foreground">{stat.sub}</div>
              </div>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}


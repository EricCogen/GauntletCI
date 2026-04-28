import { Users, Layers, GitMerge, Code2 } from "lucide-react";

const audiences = [
  {
    icon: Code2,
    title: "Senior engineers",
    description:
      "You have seen risky changes ship after passing tests and review. You want a tool that catches the subtle behavioral shifts that experience teaches you to fear - not a linter that flags line length.",
    fit: "High fit",
    color: "text-cyan-400",
    bg: "bg-cyan-500/10",
    border: "border-cyan-500/30",
  },
  {
    icon: Users,
    title: "Tech leads and engineering managers",
    description:
      "Your team ships fast. Review bandwidth is limited. You want a pre-PR gate that handles structural and behavioral risk so reviewers can focus on intent and design - not hunting regressions in diffs.",
    fit: "High fit",
    color: "text-violet-400",
    bg: "bg-violet-500/10",
    border: "border-violet-500/30",
  },
  {
    icon: GitMerge,
    title: "CI/CD-mature teams",
    description:
      "You already have tests, linters, and security scans. You know the gap: none of them validate what the change actually does at runtime. GauntletCI slots into the pre-commit step you do not have yet.",
    fit: "Exact fit",
    color: "text-emerald-400",
    bg: "bg-emerald-500/10",
    border: "border-emerald-500/30",
  },
  {
    icon: Layers,
    title: "Teams maintaining large .NET codebases",
    description:
      "The more surface area a codebase has, the easier it is to miss a behavioral change in review. GauntletCI is diff-scoped: it only reports what the current change introduced, regardless of codebase size.",
    fit: "High fit",
    color: "text-blue-400",
    bg: "bg-blue-500/10",
    border: "border-blue-500/30",
  },
];

const notFor = [
  "Teams looking for a code formatter or style enforcer",
  "Projects that do not use C# or .NET",
  "Teams that want AI to write or summarize their code",
  "Anyone looking for a replacement for tests or code review",
];

export function WhoItsFor() {
  return (
    <section className="py-20 sm:py-28 bg-muted/30">
      <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
        <div className="text-center mb-14">
          <p className="text-sm font-semibold text-cyan-400 uppercase tracking-widest mb-4">
            Who this is for
          </p>
          <h2 className="text-3xl sm:text-4xl font-bold tracking-tight text-balance">
            Built for engineers who have seen risky changes ship
          </h2>
          <p className="mt-4 text-lg text-muted-foreground max-w-2xl mx-auto text-pretty">
            GauntletCI is not a general-purpose tool. It is a focused gate for teams
            who already know the visibility gap exists.
          </p>
        </div>

        <div className="grid sm:grid-cols-2 gap-5 max-w-5xl mx-auto mb-14">
          {audiences.map((a) => {
            const Icon = a.icon;
            return (
              <div
                key={a.title}
                className={`rounded-xl border ${a.border} bg-card p-6 space-y-4`}
              >
                <div className="flex items-center gap-3">
                  <div className={`h-10 w-10 rounded-full flex items-center justify-center shrink-0 ${a.bg}`}>
                    <Icon className={`h-5 w-5 ${a.color}`} />
                  </div>
                  <span className={`text-xs font-medium rounded-full px-2.5 py-0.5 border ${a.border} ${a.color} ${a.bg}`}>
                    {a.fit}
                  </span>
                </div>
                <h3 className="font-semibold text-foreground leading-snug">{a.title}</h3>
                <p className="text-sm text-muted-foreground leading-relaxed">{a.description}</p>
              </div>
            );
          })}
        </div>

        <div className="max-w-2xl mx-auto rounded-xl border border-border bg-card p-6">
          <p className="text-sm font-semibold text-foreground mb-4">Not a fit if you are looking for:</p>
          <ul className="space-y-2">
            {notFor.map((item) => (
              <li key={item} className="flex items-start gap-2 text-sm text-muted-foreground">
                <span className="mt-0.5 text-muted-foreground/50 shrink-0">&mdash;</span>
                {item}
              </li>
            ))}
          </ul>
        </div>
      </div>
    </section>
  );
}

import Link from "next/link";

const reasons = [
  {
    title: "Tests keep passing after risky refactors",
    body: "A guard clause removed, an error path swallowed, a type changed quietly - these changes compile and pass tests. GauntletCI flags them before the commit exists.",
    stat: "No compile step needed",
  },
  {
    title: "Reviewers stop playing detective",
    body: "Code review time shifts from hunting structural issues to verifying intent. The behavioral and contract risks are already handled before the PR opens.",
    stat: "Pre-PR, not post-PR",
  },
  {
    title: "Nothing leaves the machine by default",
    body: "Core analysis does not send code to external servers. No account required. Runs entirely on your developer machine. Meets data residency and air-gap requirements. Optional integrations only transmit configured data.",
    stat: "Core analysis is local",
  },
  {
    title: "Signal without noise",
    body: "Rules are scoped to the diff, not the whole codebase. Existing issues in untouched files do not appear. Every finding is directly caused by the current change.",
    stat: "Diff-scoped only",
  },
];

export function WhyTeams() {
  return (
    <section className="py-20 sm:py-28">
      <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
        <div className="text-center mb-12">
          <h2 className="text-3xl sm:text-4xl font-bold tracking-tight text-balance">
            Why teams adopt GauntletCI
          </h2>
          <p className="mt-4 text-lg text-muted-foreground max-w-2xl mx-auto text-pretty">
            Not another tool to manage. A pre-commit gate that closes the gap between
            "the build is green" and "the change is safe."
          </p>
        </div>

        <div className="grid sm:grid-cols-2 gap-5 max-w-4xl mx-auto">
          {reasons.map((r) => (
            <div key={r.title} className="rounded-xl border border-border bg-card p-6 space-y-3">
              <div className="inline-block rounded-full border border-cyan-500/20 bg-cyan-500/5 px-3 py-0.5 text-xs font-medium text-cyan-400">
                {r.stat}
              </div>
              <h3 className="font-semibold text-foreground leading-snug">{r.title}</h3>
              <p className="text-sm text-muted-foreground leading-relaxed">{r.body}</p>
            </div>
          ))}
        </div>

        <div className="mt-10 text-center">
          <Link
            href="/detections"
            className="inline-flex items-center gap-2 text-sm text-cyan-400 hover:text-cyan-300 transition-colors"
          >
            See what it catches in real codebases &rarr;
          </Link>
        </div>
      </div>
    </section>
  );
}

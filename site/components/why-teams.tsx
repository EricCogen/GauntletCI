import { ShieldCheck, GitPullRequest, EyeOff, TestTube } from "lucide-react";

const reasons = [
  {
    icon: ShieldCheck,
    title: "Catches risk before review starts",
    problem: "Code review is too late.",
    benefit:
      "By the time a PR is opened, reviewers are optimizing for correctness, not risk. GauntletCI flags behavioral risk — removed logic without tests, breaking API changes, hardcoded secrets — before the PR is ever created, when the fix costs nothing.",
  },
  {
    icon: EyeOff,
    title: "Works in private, local environments",
    problem: "Cloud-based tools aren't an option.",
    benefit:
      "All analysis runs entirely on the machine where the command runs. No diff, no finding, no file path is ever transmitted. GauntletCI works in air-gapped environments, on proprietary codebases, and in organizations with strict data residency requirements — with no policy exceptions needed.",
  },
  {
    icon: GitPullRequest,
    title: "Low noise by design",
    problem: "Alert fatigue kills every other tool.",
    benefit:
      "Every rule is tuned to surface up to 3 findings per run. Baseline mode suppresses known issues so teams only see what's new. The output is designed to be read every time — because it's almost always relevant.",
  },
  {
    icon: TestTube,
    title: "Useful even when tests pass",
    problem: "Green CI doesn't mean safe to merge.",
    benefit:
      "Tests validate what the code does, not what changed. GauntletCI answers a different question: does this diff introduce a risk that tests won't exercise? It catches the gap between test coverage and behavioral correctness.",
  },
];

export function WhyTeams() {
  return (
    <section id="why-teams" className="py-20 sm:py-28 bg-card/30">
      <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
        <div className="text-center mb-16">
          <h2 className="text-3xl sm:text-4xl font-bold tracking-tight text-balance">
            Why teams adopt GauntletCI
          </h2>
          <p className="mt-4 text-lg text-muted-foreground max-w-2xl mx-auto text-pretty">
            Not because it has features. Because it solves failures in the workflow that
            nothing else addresses.
          </p>
        </div>

        <div className="grid gap-8 sm:grid-cols-2">
          {reasons.map((reason, index) => (
            <div
              key={index}
              className="rounded-lg border border-border bg-card p-8 hover:border-cyan-500/40 transition-colors"
            >
              <div className="mb-5 inline-flex h-11 w-11 items-center justify-center rounded-md bg-gradient-to-br from-cyan-500/20 to-blue-500/20 border border-cyan-500/30">
                <reason.icon className="h-5 w-5 text-cyan-400" />
              </div>
              <p className="text-xs font-semibold uppercase tracking-widest text-cyan-400/80 mb-2">
                {reason.problem}
              </p>
              <h3 className="text-xl font-semibold mb-3">{reason.title}</h3>
              <p className="text-sm text-muted-foreground leading-relaxed">
                {reason.benefit}
              </p>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}

import {
  ShieldAlert,
  Timer,
  SlidersHorizontal,
  GitPullRequestArrow,
  Lock,
  BrainCircuit,
  Layers,
  Ticket,
} from "lucide-react";

const items = [
  {
    icon: ShieldAlert,
    feature: "Deterministic Change-Risk Detection",
    what: "30 rules analyze the exact lines added or removed in a diff, not the whole file. Each rule targets a specific class of risk: removed logic without tests, breaking API changes, hardcoded secrets, unsafe casts, missing null guards, and more.",
    benefit: "A second opinion on every commit that focuses entirely on what changed and why it might fail in production, catching the things that look fine in review but break at runtime.",
  },
  {
    icon: Timer,
    feature: "Sub-Second Feedback in the Developer Loop",
    what: "Installs as a pre-commit hook with `gauntletci init`. Before every commit, it runs on the staged diff. No compilation, no AST build, no network call.",
    benefit: "Developers catch their own risky changes before they leave the machine, when the fix costs nothing and the context is freshest. Fast enough that it doesn't change how people work.",
  },
  {
    icon: SlidersHorizontal,
    feature: "High Signal, Low Noise",
    what: "Every rule surfaces up to 3 findings per run. Baseline delta mode snapshots existing findings and suppresses them; subsequent runs show only net-new risks.",
    benefit: "Teams actually read the output. Alert fatigue is why most static analysis tools get disabled. GauntletCI is designed to be looked at every time because it's almost always relevant.",
  },
  {
    icon: GitPullRequestArrow,
    feature: "CI Gate with GitHub Inline Comments",
    what: "A drop-in GitHub Actions composite action runs GauntletCI on every PR, fails the check if findings are produced, and posts findings as inline review comments directly on the diff.",
    benefit: "Risky changes can't merge unless reviewed or suppressed. Findings appear on the exact lines that triggered them, no separate report to read, no manual triage.",
  },
  {
    icon: Lock,
    feature: "100% Local Execution & Privacy",
    what: "All analysis runs entirely on the machine where the command runs. No diff, no finding, no file path is ever transmitted. Evidence strings for PII and secrets are auto-redacted in output.",
    benefit: "Works in air-gapped environments, on proprietary codebases, and in organizations with strict data residency requirements, no policy exceptions needed. An unconditional guarantee, not a setting.",
  },
  {
    icon: BrainCircuit,
    feature: "Local LLM Enrichment (Fully Offline)",
    what: "Runs high-confidence findings through a locally hosted Phi-4 Mini model and appends a plain-English explanation. No API key, no network call. The model runs on your hardware.",
    benefit: "Junior developers get actionable context on why a finding matters and what to do about it, without asking a senior engineer. Generated locally so it can safely reference the actual diff.",
  },
  {
    icon: Layers,
    feature: "Architecture Policy Enforcement",
    what: "Reads a `forbidden_imports` list from `.gauntletci.json` and flags any added import that violates a configured dependency pair, e.g. a Domain project importing an Infrastructure namespace.",
    benefit: "Architectural boundaries that exist only in wikis and verbal agreements drift silently. GauntletCI enforces them in the diff, at commit time, before the violation is ever merged.",
  },
  {
    icon: Ticket,
    feature: "Ticket Context Attachment",
    what: "Reads the Jira, Linear, or GitHub Issue ticket referenced in the branch name and attaches the ticket description to findings.",
    benefit: "Flags scope drift: a finding in a database layer on a UI-ticket branch is a strong signal the change wasn't intentional. Verifies not just 'is this code safe?' but 'is it doing what the ticket asked for?'",
  },
];

export function FeaturesBenefits() {
  return (
    <section id="features-benefits" className="py-20 sm:py-28 bg-card/30">
      <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
        <div className="text-center mb-16">
          <h2 className="text-3xl sm:text-4xl font-bold tracking-tight text-balance">
            Features &amp; Benefits
          </h2>
          <p className="mt-4 text-lg text-muted-foreground max-w-2xl mx-auto text-pretty">
            Every capability ships with a concrete outcome. Here's what GauntletCI does
            and what that means for your team.
          </p>
        </div>

        <div className="flex flex-col divide-y divide-border">
          {items.map((item) => (
            <div key={item.feature} className="grid md:grid-cols-[2fr_1fr] gap-6 py-8 first:pt-0 last:pb-0">
              <div className="flex gap-5">
                <div className="mt-0.5 shrink-0 inline-flex h-10 w-10 items-center justify-center rounded-md bg-gradient-to-br from-cyan-500/20 to-blue-500/20 border border-cyan-500/30">
                  <item.icon className="h-5 w-5 text-cyan-400" />
                </div>
                <div>
                  <h3 className="text-base font-semibold text-foreground mb-1">{item.feature}</h3>
                  <p className="text-sm text-muted-foreground leading-relaxed">{item.what}</p>
                </div>
              </div>
              <div className="md:pl-4">
                <div className="rounded-md bg-cyan-500/5 border border-cyan-500/20 p-4 h-full flex flex-col justify-center">
                  <p className="text-xs font-semibold uppercase tracking-widest text-cyan-400/70 mb-1.5">The benefit</p>
                  <p className="text-sm text-foreground/80 leading-relaxed">{item.benefit}</p>
                </div>
              </div>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}


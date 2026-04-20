import Image from "next/image";

const groups = [
  {
    label: "CI / CD",
    items: [
      { name: "GitHub Actions", logo: "/logos/github.svg",  description: "Drop-in composite action. Fails the check, posts inline annotations." },
      { name: "GitHub Checks",  logo: "/logos/github.svg",  description: "--github-checks posts findings as native Checks on the PR head commit." },
      { name: "Docker",         logo: "/logos/docker.svg",  description: "Official runtime image for self-hosted runners and air-gapped pipelines." },
    ],
  },
  {
    label: "Notifications",
    items: [
      { name: "Slack",           logo: "/logos/slack.svg",          description: "--notify-slack <webhook> sends a findings summary on every run." },
      { name: "Microsoft Teams", logo: "/logos/microsoftteams.svg",  description: "--notify-teams <webhook> sends an adaptive card to any Teams channel." },
    ],
  },
  {
    label: "Ticket Context",
    items: [
      { name: "Jira",          logo: "/logos/jira.svg",   description: "Reads the linked Jira ticket from the branch name and attaches it to findings." },
      { name: "Linear",        logo: "/logos/linear.svg", description: "Reads Linear issue context from the branch name for scope-drift detection." },
      { name: "GitHub Issues", logo: "/logos/github.svg", description: "Fetches the linked GitHub Issue body and attaches it to findings." },
    ],
  },
  {
    label: "Incident Management",
    items: [
      { name: "PagerDuty", logo: "/logos/pagerduty.svg", description: "trace command correlates the deploy diff with live PagerDuty incidents." },
      { name: "Opsgenie",  logo: "/logos/opsgenie.svg",  description: "trace command supports Opsgenie as an alternative incident source." },
    ],
  },
  {
    label: "Coverage & Security",
    items: [
      { name: "Codecov",         logo: "/logos/codecov.svg", description: "--with-coverage attaches coverage data to findings for context." },
      { name: "GitHub Security", logo: "/logos/github.svg",  description: "--sarif emits SARIF output consumed by the GitHub Security tab." },
    ],
  },
  {
    label: "AI Assistants (MCP)",
    items: [
      { name: "Claude",         logo: "/logos/anthropic.svg", description: "gauntletci mcp serve exposes analyze and audit as callable tools." },
      { name: "Cursor",         logo: "/logos/cursor.svg",    description: "Ask Cursor to run GauntletCI on the current diff from inside the IDE." },
      { name: "GitHub Copilot", logo: "/logos/github.svg",    description: "Copilot Chat can invoke GauntletCI for deterministic risk answers." },
      { name: "Windsurf",       logo: "/logos/codeium.svg",   description: "Full MCP tool support: analyze, audit, and rule listing." },
    ],
  },
];

export function Integrations() {
  return (
    <section id="integrations" className="py-20 sm:py-28 bg-card/30">
      <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
        <div className="text-center mb-16">
          <h2 className="text-3xl sm:text-4xl font-bold tracking-tight text-balance">
            Integrations
          </h2>
          <p className="mt-4 text-lg text-muted-foreground max-w-2xl mx-auto text-pretty">
            GauntletCI plugs into the tools your team already uses, without sending
            your code anywhere.
          </p>
        </div>

        <div className="grid gap-10 sm:grid-cols-2 lg:grid-cols-3">
          {groups.map((group) => (
            <div key={group.label}>
              <p className="text-xs font-semibold uppercase tracking-widest text-cyan-400/70 mb-4">
                {group.label}
              </p>
              <ul className="flex flex-col gap-3">
                {group.items.map((item) => (
                  <li
                    key={item.name}
                    className="flex items-start gap-3 rounded-lg border border-border bg-card px-4 py-3 hover:border-cyan-500/30 transition-colors"
                  >
                    <div className="mt-0.5 shrink-0 h-5 w-5 flex items-center justify-center">
                      <Image
                        src={item.logo}
                        alt={item.name}
                        width={20}
                        height={20}
                        className="h-5 w-5 object-contain invert opacity-80"
                        unoptimized
                      />
                    </div>
                    <div>
                      <span className="text-sm font-semibold text-foreground">{item.name}</span>
                      <p className="mt-0.5 text-xs text-muted-foreground leading-relaxed">
                        {item.description}
                      </p>
                    </div>
                  </li>
                ))}
              </ul>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}


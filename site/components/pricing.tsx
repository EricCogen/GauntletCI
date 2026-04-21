import Link from "next/link";
import { Check, Minus, Key } from "lucide-react";

type CellValue = "check" | "none" | "license";

interface FeatureRow {
  label: string;
  divider?: boolean;
  proTooltip?: React.ReactNode;
  community: CellValue;
  pro: CellValue;
  teams: CellValue;
  enterprise: CellValue;
}

const features: FeatureRow[] = [
  // Community - included in all tiers
  { label: "Deterministic Change-Risk Detection", community: "check", pro: "check", teams: "check", enterprise: "check" },
  { label: "Sub-Second Pre-Commit Hook",           community: "check", pro: "check", teams: "check", enterprise: "check" },
  { label: "Per-Repo Configuration (.gauntletci.json)", community: "check", pro: "check", teams: "check", enterprise: "check" },
  { label: "100% Local Execution & Privacy",       community: "check", pro: "check", teams: "check", enterprise: "check" },
  { label: "Evidence String Redaction (PII / Secrets)", community: "check", pro: "check", teams: "check", enterprise: "check" },

  // Pro
  { label: "Baseline Delta Mode",                  divider: true, community: "none", pro: "license", teams: "check", enterprise: "check" },
  { label: "Local LLM Enrichment (AI Explanations)", proTooltip: <>Uses the built-in ONNX engine - no Ollama needed. Run <code className="bg-muted px-0.5 rounded">gauntletci model download</code> once to cache the model locally. <Link href="/docs/local-llm" className="underline text-cyan-400">Setup guide</Link> included with license.</>, community: "none", pro: "license", teams: "check", enterprise: "check" },
  { label: "AI Assistant Integration (MCP Server)", community: "none", pro: "license", teams: "check", enterprise: "check" },

  // Teams
  { label: "CI Gate with GitHub Inline Comments",  divider: true, community: "none", pro: "none", teams: "license", enterprise: "check" },
  { label: "Notification Integrations (Slack / Teams)", community: "none", pro: "none", teams: "license", enterprise: "check" },
  { label: "Ticket Context Attachment (Jira / Linear)", community: "none", pro: "none", teams: "license", enterprise: "check" },
  { label: "Architecture Policy Enforcement",      community: "none", pro: "none", teams: "license", enterprise: "check" },

  // Enterprise
  { label: "Incident Correlation (trace command)", divider: true, community: "none", pro: "none", teams: "none", enterprise: "license" },
  { label: "Postmortem Analysis",                  community: "none", pro: "none", teams: "none", enterprise: "license" },
  { label: "Complete Audit Trail Export",          community: "none", pro: "none", teams: "none", enterprise: "license" },
];

function Cell({ value, tooltip }: { value: CellValue; tooltip?: string }) {
  if (value === "check") {
    return (
      <div className="flex justify-center">
        <Check className="h-5 w-5 text-cyan-400" />
      </div>
    );
  }
  if (value === "license") {
    return (
      <div className="flex items-center justify-center gap-1.5">
        <a href="https://github.com/EricCogen/GauntletCI" target="_blank" rel="noopener noreferrer" className="flex items-center gap-1.5 group/license">
          <Key className="h-3 w-3 text-amber-400/80 shrink-0 group-hover/license:text-amber-400 transition-colors" />
          <span className="text-xs text-amber-400/80 hidden sm:inline group-hover/license:text-amber-400 group-hover/license:underline transition-colors">
            License
          </span>
        </a>
        {tooltip && (
          <div className="relative group/tip">
            <span className="text-muted-foreground/50 cursor-help text-xs select-none">ⓘ</span>
            <span className="absolute left-1/2 -translate-x-1/2 bottom-full mb-2 w-56 p-2 bg-popover border border-border text-xs rounded-md opacity-0 group-hover/tip:opacity-100 transition-opacity pointer-events-none z-10 text-foreground/90 shadow-lg">
              {tooltip}
            </span>
          </div>
        )}
      </div>
    );
  }
  return (
    <div className="flex justify-center">
      <Minus className="h-4 w-4 text-muted-foreground/25" />
    </div>
  );
}

const tiers = [
  { key: "community", label: "Community", price: "$0", period: "forever",         accent: "text-foreground",    bg: "" },
  { key: "pro",       label: "Pro",        price: "$19", period: "/dev/mo",        accent: "text-cyan-400",      bg: "bg-cyan-500/5" },
  { key: "teams",     label: "Teams",      price: "$49", period: "/dev/mo",        accent: "text-indigo-400",    bg: "bg-indigo-500/5" },
  { key: "enterprise",label: "Enterprise", price: "Custom", period: "",            accent: "text-purple-400",    bg: "bg-purple-500/5" },
] as const;

export function Pricing({ standalone = false }: { standalone?: boolean }) {
  return (
    <section id="pricing" className="py-20 sm:py-28">
      <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
        {!standalone && (
        <div className="text-center mb-16">
          <h2 className="text-3xl sm:text-4xl font-bold tracking-tight text-balance">
            Simple, predictable pricing
          </h2>
          <p className="mt-4 text-lg text-muted-foreground max-w-2xl mx-auto text-pretty">
            Start free. No account required. Pay only when your team needs enforcement,
            CI integration, or AI enrichment.
          </p>
        </div>
        )}

        <div className="rounded-xl border border-border overflow-hidden">
          {/* Header */}
          <div className="grid grid-cols-[2fr_1fr_1fr_1fr_1fr] bg-card/80 border-b border-border">
            <div className="px-5 py-4 text-sm font-semibold text-muted-foreground">Features</div>
            {tiers.map((t) => (
              <div key={t.key} className={`px-3 py-4 text-center ${t.bg}`}>
                <div className={`text-sm font-semibold ${t.accent}`}>{t.label}</div>
                <div className="mt-1 flex items-baseline justify-center gap-1">
                  <span className="text-lg font-bold text-foreground">{t.price}</span>
                  {t.period && <span className="text-xs text-muted-foreground">{t.period}</span>}
                </div>
              </div>
            ))}
          </div>

          {/* Description row */}
          <div className="grid grid-cols-[2fr_1fr_1fr_1fr_1fr] bg-secondary/20 border-b border-border">
            <div className="px-5 py-2" />
            <div className="px-3 py-2 text-center text-xs text-muted-foreground italic">Local-first, forever free</div>
            <div className="px-3 py-2 text-center text-xs text-muted-foreground italic">Everything in Community +</div>
            <div className="px-3 py-2 text-center text-xs text-muted-foreground italic">Everything in Pro +</div>
            <div className="px-3 py-2 text-center text-xs text-muted-foreground italic">Everything in Teams +</div>
          </div>

          {/* Feature rows */}
          {features.map((row, i) => (
            <div
              key={row.label}
              className={`grid grid-cols-[2fr_1fr_1fr_1fr_1fr] border-b border-border last:border-0 transition-colors hover:bg-card/40
                ${row.divider ? "border-t-2 border-t-border" : ""}
                ${i % 2 === 0 ? "bg-transparent" : "bg-card/20"}
              `}
            >
              <div className="px-5 py-3.5 text-sm text-foreground/90 flex items-center">{row.label}</div>
              {tiers.map((t) => (
                <div key={t.key} className={`px-3 py-3.5 flex items-center justify-center ${t.bg}`}>
                  <Cell value={row[t.key]} tooltip={t.key === "pro" ? row.proTooltip : undefined} />
                </div>
              ))}
            </div>
          ))}
        </div>

        {/* Legend */}
        <div className="mt-6 flex flex-wrap items-center justify-center gap-6 text-sm text-muted-foreground">
          <span className="flex items-center gap-2">
            <Check className="h-4 w-4 text-cyan-400" /> Included
          </span>
          <span className="flex items-center gap-2">
            {/* TODO: replace href="#" with real conversion/purchase path */}
            <a href="#" className="flex items-center gap-2 hover:opacity-80 transition-opacity">
              <Key className="h-4 w-4 text-amber-400/80" /> Requires active license; click to get one
            </a>
          </span>
          <span className="flex items-center gap-2">
            <Minus className="h-4 w-4 text-muted-foreground/25" /> Not available
          </span>
        </div>

        <p className="mt-8 text-center text-sm text-muted-foreground">
          Core analysis runs entirely on your machine. No code is sent to any external service by default.
        </p>
      </div>
    </section>
  );
}

import { Check, Minus, AlertCircle } from "lucide-react";

type CellValue = "yes" | "no" | "partial";

interface ComparisonRow {
  label: string;
  sublabel?: string;
  gauntlet: CellValue;
  sonar: CellValue;
  semgrep: CellValue;
  snyk: CellValue;
  codeql: CellValue;
  codeclimate: CellValue;
}

const rows: ComparisonRow[] = [
  {
    label: "Diff-scoped analysis",
    sublabel: "Only analyzes lines that actually changed",
    gauntlet: "yes", sonar: "no", semgrep: "partial", snyk: "no", codeql: "no", codeclimate: "no",
  },
  {
    label: "Sub-second pre-commit feedback",
    sublabel: "Results before the commit is created",
    gauntlet: "yes", sonar: "no", semgrep: "partial", snyk: "no", codeql: "no", codeclimate: "no",
  },
  {
    label: "100% local - no code leaves the machine",
    sublabel: "No cloud upload, no SaaS account required",
    gauntlet: "yes", sonar: "partial", semgrep: "partial", snyk: "no", codeql: "partial", codeclimate: "no",
  },
  {
    label: "Air-gap / data residency friendly",
    gauntlet: "yes", sonar: "partial", semgrep: "partial", snyk: "no", codeql: "partial", codeclimate: "no",
  },
  {
    label: "Free tier with full rule set",
    gauntlet: "yes", sonar: "partial", semgrep: "partial", snyk: "partial", codeql: "partial", codeclimate: "partial",
  },
  {
    label: "GitHub inline PR comments",
    gauntlet: "yes", sonar: "yes", semgrep: "yes", snyk: "yes", codeql: "yes", codeclimate: "yes",
  },
  {
    label: "Baseline delta mode",
    sublabel: "Suppress existing findings, show only net-new",
    gauntlet: "yes", sonar: "partial", semgrep: "no", snyk: "no", codeql: "no", codeclimate: "partial",
  },
  {
    label: "Local LLM enrichment (offline)",
    sublabel: "AI explanations with no API key or network call",
    gauntlet: "yes", sonar: "no", semgrep: "no", snyk: "no", codeql: "no", codeclimate: "no",
  },
  {
    label: "Incident correlation",
    sublabel: "Connects deploy diffs to live PagerDuty/Opsgenie alerts",
    gauntlet: "yes", sonar: "no", semgrep: "no", snyk: "no", codeql: "no", codeclimate: "no",
  },
  {
    label: "Architecture policy enforcement",
    sublabel: "Configurable forbidden import pairs",
    gauntlet: "yes", sonar: "partial", semgrep: "partial", snyk: "no", codeql: "no", codeclimate: "no",
  },
];

const competitors = [
  { key: "gauntlet",    label: "GauntletCI",   highlight: true },
  { key: "sonar",       label: "SonarQube",    highlight: false },
  { key: "semgrep",     label: "Semgrep",      highlight: false },
  { key: "snyk",        label: "Snyk",         highlight: false },
  { key: "codeql",      label: "CodeQL",       highlight: false },
  { key: "codeclimate", label: "Code Climate", highlight: false },
] as const;

function Cell({ value, highlight }: { value: CellValue; highlight: boolean }) {
  if (value === "yes") {
    return (
      <div className="flex justify-center">
        <Check className={`h-5 w-5 ${highlight ? "text-cyan-400" : "text-emerald-500/70"}`} />
      </div>
    );
  }
  if (value === "partial") {
    return (
      <div className="flex justify-center">
        <AlertCircle className="h-4 w-4 text-amber-400/60" />
      </div>
    );
  }
  return (
    <div className="flex justify-center">
      <Minus className="h-4 w-4 text-muted-foreground/25" />
    </div>
  );
}

export function Comparison() {
  return (
    <section id="comparison" className="py-20 sm:py-28">
      <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
        <div className="text-center mb-16">
          <h2 className="text-3xl sm:text-4xl font-bold tracking-tight text-balance">
            How GauntletCI compares
          </h2>
          <p className="mt-4 text-lg text-muted-foreground max-w-2xl mx-auto text-pretty">
            Most tools analyze the whole codebase on a schedule. GauntletCI analyzes
            what changed, locally, before you commit.
          </p>
        </div>

        <div className="rounded-xl border border-border overflow-x-auto">
          {/* Header */}
          <div className="grid grid-cols-[2fr_1fr_1fr_1fr_1fr_1fr] min-w-[640px] bg-card/80 border-b border-border">
            <div className="px-5 py-4 text-sm font-semibold text-muted-foreground">Capability</div>
            {competitors.map((c) => (
              <div key={c.key} className={`px-3 py-4 text-center ${c.highlight ? "bg-cyan-500/5" : ""}`}>
                <span className={`text-sm font-semibold ${c.highlight ? "text-cyan-400" : "text-foreground/70"}`}>
                  {c.label}
                </span>
              </div>
            ))}
          </div>

          {/* Rows */}
          {rows.map((row, i) => (
            <div
              key={row.label}
              className={`grid grid-cols-[2fr_1fr_1fr_1fr_1fr_1fr] min-w-[640px] border-b border-border last:border-0 hover:bg-card/40 transition-colors ${i % 2 === 0 ? "bg-transparent" : "bg-card/20"}`}
            >
              <div className="px-5 py-3.5 flex flex-col justify-center">
                <span className="text-sm text-foreground/90">{row.label}</span>
                {row.sublabel && (
                  <span className="text-xs text-muted-foreground/60 mt-0.5">{row.sublabel}</span>
                )}
              </div>
              {competitors.map((c) => (
                <div key={c.key} className={`px-3 py-3.5 flex items-center justify-center ${c.highlight ? "bg-cyan-500/5" : ""}`}>
                  <Cell value={row[c.key]} highlight={c.highlight} />
                </div>
              ))}
            </div>
          ))}
        </div>

        {/* Legend */}
        <div className="mt-6 flex flex-wrap items-center justify-center gap-6 text-sm text-muted-foreground">
          <span className="flex items-center gap-2"><Check className="h-4 w-4 text-cyan-400" /> Yes</span>
          <span className="flex items-center gap-2"><AlertCircle className="h-4 w-4 text-amber-400/60" /> Partial / paid tier only</span>
          <span className="flex items-center gap-2"><Minus className="h-4 w-4 text-muted-foreground/25" /> No</span>
        </div>
      </div>
    </section>
  );
}

import Link from "next/link";
import { Check, Minus, AlertCircle, ArrowRight } from "lucide-react";
import { Button } from "@/components/ui/button";
import { addUtmParams } from "@/lib/utils";

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
  ndepend: CellValue;
}



const rows: ComparisonRow[] = [
  {
    label: "Diff-scoped analysis",
    sublabel: "Only analyzes lines that actually changed",
    gauntlet: "yes", sonar: "no", semgrep: "partial", snyk: "no", codeql: "no", codeclimate: "no", ndepend: "no",
  },
  {
    label: "Sub-second pre-commit feedback",
    sublabel: "Results before the commit is created",
    gauntlet: "yes", sonar: "no", semgrep: "partial", snyk: "no", codeql: "no", codeclimate: "no", ndepend: "no",
  },
  {
    label: "Core analysis runs locally - no code leaves by default",
    sublabel: "No cloud upload by default, no SaaS account required",
    gauntlet: "yes", sonar: "partial", semgrep: "partial", snyk: "no", codeql: "partial", codeclimate: "no", ndepend: "yes",
  },
  {
    label: "Core engine can run air-gapped",
    gauntlet: "yes", sonar: "partial", semgrep: "partial", snyk: "no", codeql: "partial", codeclimate: "no", ndepend: "yes",
  },
  {
    label: "Free tier with full rule set",
    gauntlet: "yes", sonar: "partial", semgrep: "partial", snyk: "partial", codeql: "partial", codeclimate: "partial", ndepend: "no",
  },
  {
    label: "GitHub inline PR comments",
    sublabel: "Requires Teams tier",
    gauntlet: "yes", sonar: "yes", semgrep: "yes", snyk: "yes", codeql: "yes", codeclimate: "yes", ndepend: "partial",
  },
  {
    label: "Baseline delta mode",
    sublabel: "Suppress existing findings, show only net-new",
    gauntlet: "yes", sonar: "partial", semgrep: "no", snyk: "no", codeql: "no", codeclimate: "partial", ndepend: "partial",
  },
  {
    label: "Local LLM enrichment (offline)",
    sublabel: "AI explanations with no API key or network call",
    gauntlet: "yes", sonar: "no", semgrep: "no", snyk: "no", codeql: "no", codeclimate: "no", ndepend: "no",
  },
  {
    label: "Incident correlation",
    sublabel: "Connects deploy diffs to live PagerDuty/Opsgenie alerts",
    gauntlet: "yes", sonar: "no", semgrep: "no", snyk: "no", codeql: "no", codeclimate: "no", ndepend: "no",
  },
  {
    label: "Architecture policy enforcement",
    sublabel: "Configurable forbidden import pairs",
    gauntlet: "yes", sonar: "partial", semgrep: "partial", snyk: "no", codeql: "no", codeclimate: "no", ndepend: "yes",
  },
  {
    label: "MCP server (AI assistant integration)",
    sublabel: "Pro tier: Copilot, Cursor, and other AI tools call GauntletCI directly",
    gauntlet: "yes", sonar: "no", semgrep: "no", snyk: "no", codeql: "no", codeclimate: "no", ndepend: "no",
  },
  {
    label: "Custom rules via code",
    sublabel: "Implement IRule in C# - no YAML, no query language",
    gauntlet: "yes", sonar: "yes", semgrep: "yes", snyk: "no", codeql: "yes", codeclimate: "partial", ndepend: "yes",
  },
];

const competitors = [
  { key: "gauntlet",    label: "GauntletCI",   highlight: true,  href: null },
  { key: "sonar",       label: "SonarQube",    highlight: false, href: "/compare/gauntletci-vs-sonarqube" },
  { key: "semgrep",     label: "Semgrep",      highlight: false, href: "/compare/gauntletci-vs-semgrep" },
  { key: "snyk",        label: "Snyk",         highlight: false, href: "/compare/gauntletci-vs-snyk" },
  { key: "codeql",      label: "CodeQL",       highlight: false, href: "/compare/gauntletci-vs-codeql" },
  { key: "codeclimate", label: "Code Climate", highlight: false, href: "/compare/gauntletci-vs-codeclimate" },
  { key: "ndepend",     label: "NDepend",      highlight: false, href: "/compare/gauntletci-vs-ndepend" },
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

        <div className="mx-auto max-w-3xl text-center mb-10 space-y-3 text-muted-foreground leading-relaxed">
          <p>GauntletCI is designed to complement the tools your team already uses.</p>
          <p>
            SonarQube, Semgrep, Snyk, CodeQL, and similar tools are valuable, but they primarily
            answer different questions: Is this code maintainable? Is it vulnerable? Does it match
            known patterns?
          </p>
          <p>
            GauntletCI answers a narrower question at a more urgent moment: Did this diff introduce
            Behavioral Change Risk that should block or change the review?
          </p>
        </div>

        <div className="rounded-xl border border-border overflow-x-auto">
          {/* Header */}
          <div className="grid grid-cols-[2fr_1fr_1fr_1fr_1fr_1fr_1fr_1fr] min-w-[880px] bg-card/80 border-b border-border">
            <div className="px-5 py-4 text-sm font-semibold text-muted-foreground">Capability</div>
            {competitors.map((c) => (
              <div key={c.key} className={`px-3 py-4 text-center ${c.highlight ? "bg-cyan-500/5" : ""}`}>
                {c.href ? (
                  <Link href={c.href} className="group inline-flex flex-col items-center gap-0.5">
                    <span className={`text-sm font-semibold ${c.highlight ? "text-cyan-400" : "text-foreground/70 group-hover:text-foreground transition-colors"}`}>
                      {c.label}
                    </span>
                    <span className="text-[10px] text-muted-foreground/50 group-hover:text-cyan-400 transition-colors">deep dive &rsaquo;</span>
                  </Link>
                ) : (
                  <span className={`text-sm font-semibold ${c.highlight ? "text-cyan-400" : "text-foreground/70"}`}>
                    {c.label}
                  </span>
                )}
              </div>
            ))}
          </div>

          {/* Rows */}
          {rows.map((row, i) => (
            <div
              key={row.label}
              className={`grid grid-cols-[2fr_1fr_1fr_1fr_1fr_1fr_1fr_1fr] min-w-[880px] border-b border-border last:border-0 hover:bg-card/40 transition-colors ${i % 2 === 0 ? "bg-transparent" : "bg-card/20"}`}
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

        {/* AI code review link */}
        <div className="mt-10 text-center space-y-6">
          <div className="text-sm text-muted-foreground">
            Not seeing your tool?{" "}
            <Link href="/compare/gauntletci-vs-ai-code-review" className="text-cyan-400 hover:text-cyan-300 transition-colors underline underline-offset-2">
              Compare vs AI code review tools
            </Link>
          </div>
          
          <div className="flex justify-center">
            <Button size="lg" asChild className="bg-cyan-500 hover:bg-cyan-600 text-black font-semibold">
              <Link href={addUtmParams("#pricing", "comparison", "cta_button", "try_free")}>
                Try GauntletCI Free
                <ArrowRight className="ml-2 h-4 w-4" />
              </Link>
            </Button>
          </div>
        </div>
      </div>
    </section>
  );
}

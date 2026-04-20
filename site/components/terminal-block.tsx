import { Terminal } from "lucide-react";

interface OutputLine {
  type: "cmd" | "blank" | "critical" | "risk" | "info" | "action" | "pass" | "fail" | "dim";
  text: string;
  annotation?: number;
}

const lines: OutputLine[] = [
  { type: "cmd",      text: "$ gauntletci analyze --staged" },
  { type: "blank",    text: "" },
  { type: "dim",      text: "  GauntletCI v2.1.0, 35 rules, diff-scoped" },
  { type: "blank",    text: "" },
  { type: "critical", text: "  [BLOCK]  GCI0004  Breaking change in public API", annotation: 1 },
  { type: "dim",      text: "           OrderService.cs:142: ProcessPayment(decimal amount)" },
  { type: "dim",      text: "           Parameter type changed: decimal -> Money" },
  { type: "risk",     text: "           4 downstream callers detected, 0 updated tests.", annotation: 2 },
  { type: "blank",    text: "" },
  { type: "critical", text: "  [BLOCK]  GCI0032  Uncaught exception path added", annotation: 3 },
  { type: "dim",      text: "           PaymentGateway.cs:87: throw new TimeoutException()" },
  { type: "risk",     text: "           No catch block or test for this exit point." },
  { type: "blank",    text: "" },
  { type: "info",     text: "  [WARN]   GCI0029  Potential PII in log statement", annotation: 4 },
  { type: "dim",      text: "           CustomerRepository.cs:211: log.Info(customer.Email)" },
  { type: "blank",    text: "" },
  { type: "fail",     text: "  x  2 blocking risks detected. Commit blocked.", annotation: 5 },
  { type: "dim",      text: "     Run with --with-llm for remediation guidance." },
];

const annotations = [
  { n: 1, label: "Rule ID + severity", detail: "Each finding maps to a named rule. BLOCK halts the commit. WARN surfaces without blocking." },
  { n: 2, label: "Caller impact", detail: "GauntletCI counts downstream callers and checks whether tests were updated to match the new signature." },
  { n: 3, label: "Exception path", detail: "A new throw with no catch block and no test coverage - a silent crash waiting for the first request." },
  { n: 4, label: "PII detection", detail: "Customer email logged to a structured sink. Flagged as a warning, not a block - your team decides." },
  { n: 5, label: "Commit blocked", detail: "The pre-commit hook exits non-zero. Git does not create the commit until blocking findings are resolved." },
];

const colorMap: Record<OutputLine["type"], string> = {
  cmd:      "text-cyan-300 font-semibold",
  blank:    "",
  dim:      "text-gray-500",
  critical: "text-red-400 font-semibold",
  risk:     "text-orange-400",
  info:     "text-yellow-400",
  action:   "text-blue-400",
  pass:     "text-emerald-400 font-semibold",
  fail:     "text-red-400 font-semibold",
};

export function TerminalBlock() {
  return (
    <section id="terminal" className="py-20 sm:py-28">
      <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
        <div className="text-center mb-12">
          <h2 className="text-3xl sm:text-4xl font-bold tracking-tight text-balance">
            See it in action
          </h2>
          <p className="mt-4 text-lg text-muted-foreground max-w-2xl mx-auto text-pretty">
            GauntletCI intercepts behavioral risks before your commit lands.
            Here&apos;s what it looks like when it catches a real breaking change.
          </p>
        </div>

        <div className="mx-auto max-w-5xl flex flex-col lg:flex-row gap-6 items-start">
          {/* Terminal window */}
          <div className="flex-1 min-w-0">
            <div className="rounded-xl overflow-hidden border border-border/60 shadow-2xl shadow-black/40">
              {/* Title bar */}
              <div className="flex items-center gap-2 bg-zinc-900 border-b border-border/40 px-4 py-3">
                <span className="h-3 w-3 rounded-full bg-red-500/80" />
                <span className="h-3 w-3 rounded-full bg-yellow-500/80" />
                <span className="h-3 w-3 rounded-full bg-emerald-500/80" />
                <div className="flex-1 flex items-center justify-center gap-1.5 text-xs text-gray-500">
                  <Terminal className="h-3 w-3" />
                  <span>bash: ~/repos/my-service</span>
                </div>
              </div>

              {/* Output body */}
              <div className="bg-zinc-950 px-5 py-5 font-mono text-[13px] leading-relaxed">
                {lines.map((line, i) =>
                  line.type === "blank" ? (
                    <div key={i} className="h-2" />
                  ) : (
                    <div key={i} className={`flex items-baseline gap-2 ${colorMap[line.type]}`}>
                      <span className="flex-1">{line.text}</span>
                      {line.annotation && (
                        <span className="shrink-0 inline-flex items-center justify-center w-4 h-4 rounded-full bg-cyan-500/20 text-cyan-400 text-[10px] font-bold leading-none">
                          {line.annotation}
                        </span>
                      )}
                    </div>
                  )
                )}
              </div>
            </div>
          </div>

          {/* Annotations */}
          <div className="lg:w-64 xl:w-72 shrink-0 space-y-3">
            {annotations.map((a) => (
              <div key={a.n} className="flex gap-3 rounded-lg border border-border bg-card/50 p-3">
                <span className="shrink-0 inline-flex items-center justify-center w-5 h-5 rounded-full bg-cyan-500/20 text-cyan-400 text-[10px] font-bold leading-none mt-0.5">
                  {a.n}
                </span>
                <div className="space-y-0.5">
                  <p className="text-xs font-semibold text-foreground">{a.label}</p>
                  <p className="text-xs text-muted-foreground leading-relaxed">{a.detail}</p>
                </div>
              </div>
            ))}
          </div>
        </div>
      </div>
    </section>
  );
}

import { Terminal } from "lucide-react";

interface OutputLine {
  type: "cmd" | "blank" | "critical" | "risk" | "info" | "action" | "pass" | "fail" | "dim";
  text: string;
}

const lines: OutputLine[] = [
  { type: "cmd",      text: "$ gauntletci analyze --staged" },
  { type: "blank",    text: "" },
  { type: "dim",      text: "  GauntletCI v2.1.0, 35 rules, diff-scoped" },
  { type: "blank",    text: "" },
  { type: "critical", text: "  [BLOCK]  GCI0004  Breaking change in public API" },
  { type: "dim",      text: "           OrderService.cs:142: ProcessPayment(decimal amount)" },
  { type: "dim",      text: "           Parameter type changed: decimal → Money" },
  { type: "risk",     text: "           4 downstream callers detected, 0 updated tests." },
  { type: "blank",    text: "" },
  { type: "critical", text: "  [BLOCK]  GCI0032  Uncaught exception path added" },
  { type: "dim",      text: "           PaymentGateway.cs:87: throw new TimeoutException()" },
  { type: "risk",     text: "           No catch block or test for this exit point." },
  { type: "blank",    text: "" },
  { type: "info",     text: "  [WARN]   GCI0029  Potential PII in log statement" },
  { type: "dim",      text: "           CustomerRepository.cs:211: log.Info(customer.Email)" },
  { type: "blank",    text: "" },
  { type: "fail",     text: "  ✖  2 blocking risks detected. Commit blocked." },
  { type: "dim",      text: "     Run with --with-llm for remediation guidance." },
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

        <div className="mx-auto max-w-3xl">
          {/* Window chrome */}
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
                  <div key={i} className={colorMap[line.type]}>
                    {line.text}
                  </div>
                )
              )}
            </div>
          </div>

          {/* Caption */}
          <p className="mt-5 text-center text-sm text-muted-foreground">
            Blocking risks halt the commit.{" "}
            <span className="text-cyan-400">Warnings surface without blocking.</span>{" "}
            No false-positive flood, just what matters.
          </p>
        </div>
      </div>
    </section>
  );
}

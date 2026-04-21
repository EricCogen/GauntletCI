const diff = [
  { type: "context", line: "public async Task<Order> CreateOrderAsync(CreateOrderRequest request)" },
  { type: "context", line: "{" },
  { type: "removed", line: "    if (request is null) throw new ArgumentNullException(nameof(request));" },
  { type: "context", line: "    var order = new Order(request.CustomerId, request.Items);" },
  { type: "context", line: "    return await _repo.SaveAsync(order);" },
  { type: "context", line: "}" },
];

const lineStyle: Record<string, string> = {
  context: "text-foreground/60",
  removed: "text-red-400 bg-red-500/10 block -mx-4 px-4",
  added:   "text-green-400 bg-green-500/10 block -mx-4 px-4",
};

const linePrefix: Record<string, string> = {
  context: "  ",
  removed: "- ",
  added:   "+ ",
};

export function ProofExample() {
  return (
    <section className="py-20 sm:py-24 bg-background">
      <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">

        <div className="text-center mb-10">
          <p className="text-xs font-semibold uppercase tracking-widest text-cyan-400/70 mb-3">
            Real detection -- not synthetic
          </p>
          <h2 className="text-3xl sm:text-4xl font-bold tracking-tight text-balance">
            The change that looks fine in review
          </h2>
          <p className="mt-4 text-lg text-muted-foreground max-w-2xl mx-auto text-pretty">
            It compiles. Tests pass. Every reviewer approves it. GauntletCI flags it
            before the commit lands.
          </p>
        </div>

        <div className="grid lg:grid-cols-2 gap-8 items-start">
          {/* Diff panel */}
          <div className="rounded-xl border border-border overflow-hidden">
            <div className="border-b border-border bg-card/60 px-4 py-2 flex items-center gap-2">
              <div className="flex gap-1.5">
                <div className="w-2.5 h-2.5 rounded-full bg-red-500/40" />
                <div className="w-2.5 h-2.5 rounded-full bg-amber-500/40" />
                <div className="w-2.5 h-2.5 rounded-full bg-green-500/40" />
              </div>
              <span className="text-xs font-mono text-muted-foreground/40 ml-1">
                OrderService.cs -- 1 line removed
              </span>
            </div>
            <div className="p-4 font-mono text-xs leading-relaxed space-y-0.5 bg-background/50">
              {diff.map((line, i) => (
                <span key={i} className={`${lineStyle[line.type]} block`}>
                  <span className="select-none text-muted-foreground/30 mr-3">{linePrefix[line.type]}</span>
                  {line.line}
                </span>
              ))}
            </div>
            <div className="border-t border-border bg-card/40 px-4 py-3">
              <p className="font-mono text-xs text-amber-400 leading-relaxed">
                [High] GCI0003: Guard clause removed at line 3. ArgumentNullException no
                longer thrown on null input. Callers relying on this contract will see
                NullReferenceException deeper in the call stack.
              </p>
            </div>
          </div>

          {/* Explanation panel */}
          <div className="space-y-5">
            <div className="rounded-xl border border-border bg-card/30 p-5">
              <p className="text-xs font-semibold uppercase tracking-widest text-red-400/70 mb-2">
                Why code review misses it
              </p>
              <p className="text-sm text-muted-foreground leading-relaxed">
                The change is a single line removal. The surrounding code still looks
                correct. The PR description says "cleaned up redundant null check" --
                and upstream callers do validate input, so it seems safe. No test fails.
                The reviewer approves.
              </p>
            </div>

            <div className="rounded-xl border border-border bg-card/30 p-5">
              <p className="text-xs font-semibold uppercase tracking-widest text-red-400/70 mb-2">
                Why tests miss it
              </p>
              <p className="text-sm text-muted-foreground leading-relaxed">
                Tests exercise the happy path. The null input case was implicitly covered
                by the guard -- but there is no explicit test for it. Coverage reports
                show green. The regression ships.
              </p>
            </div>

            <div className="rounded-xl border border-cyan-500/20 bg-cyan-500/5 p-5">
              <p className="text-xs font-semibold uppercase tracking-widest text-cyan-400/70 mb-2">
                Why GauntletCI catches it
              </p>
              <p className="text-sm text-muted-foreground leading-relaxed">
                GCI0003 fires on any diff where a null guard is removed from a public
                method. It does not care about tests, coverage, or upstream callers.
                It analyzes only what changed -- and that line removal is a behavioral
                contract break, not a cleanup.
              </p>
            </div>
          </div>
        </div>

      </div>
    </section>
  );
}

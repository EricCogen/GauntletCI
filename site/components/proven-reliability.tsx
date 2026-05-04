import Link from "next/link";

const studies = [
  {
    project: "dotnet/efcore",
    category: "Performance",
    rule: "GCI0044",
    scenario: "A PR refactored a query helper method. The new implementation introduced LINQ inside a loop over a large result set.",
    finding: "O(n2) performance risk flagged before commit. The pattern would have been invisible in code review -- each piece looked fine in isolation.",
    tag: "LINQ in hot path",
    link: "/detections",
  },
  {
    project: "StackExchange.Redis",
    category: "Behavioral",
    rule: "GCI0036",
    scenario: "A property getter was refactored to lazily initialize a backing field. The initialization had a side effect that mutated shared context.",
    finding: "Pure context mutation in property getter caught pre-commit. No test covered the initialization path. The bug would have appeared only under concurrent access.",
    tag: "State mutation",
    link: "/detections",
  },
  {
    project: "AngleSharp",
    category: "Breaking Change",
    rule: "GCI0021",
    scenario: "An enum member used in JSON serialization was removed during a cleanup pass. All tests passed because they used different enum values.",
    finding: "Enum member removal detected as a serialization contract break. Existing stored or transmitted JSON would have failed to deserialize after deploy.",
    tag: "Serialization contract",
    link: "/detections",
  },
  {
    project: "Dapper",
    category: "Nullability",
    rule: "GCI0043",
    scenario: "A null-forgiving operator was added to suppress a compiler warning on a value that could legitimately be null at runtime.",
    finding: "Null-forgiving operator misuse flagged. The suppressed warning was masking a real null path that would throw in production on certain query results.",
    tag: "Null safety",
    link: "/detections",
  },
  {
    project: "Polly",
    category: "Concurrency",
    rule: "GCI0016",
    scenario: "A retry handler was simplified during cleanup. The author replaced an awaited call with .Result to avoid propagating async through the call chain.",
    finding: "Synchronous block on async method detected. Under load this pattern causes thread pool starvation -- the same mechanism behind classic ASP.NET Core deadlocks.",
    tag: "Blocking async call",
    link: "/detections#gci0016",
  },
  {
    project: "FluentValidation",
    category: "Behavioral",
    rule: "GCI0003",
    scenario: "A public Validate() method was refactored to remove 'redundant' checks. A null guard on the incoming model was removed as assumed to be handled upstream.",
    finding: "Null guard removal on a public API method detected. Callers passing null now receive a NullReferenceException deep in the validation pipeline instead of a clear ArgumentNullException.",
    tag: "Guard removed",
    link: "/detections#gci0003",
  },
];

const categoryColor: Record<string, string> = {
  Performance: "text-orange-400 bg-orange-500/10 border-orange-500/20",
  Behavioral: "text-yellow-400 bg-yellow-500/10 border-yellow-500/20",
  "Breaking Change": "text-red-400 bg-red-500/10 border-red-500/20",
  Nullability: "text-blue-400 bg-blue-500/10 border-blue-500/20",
  Concurrency: "text-purple-400 bg-purple-500/10 border-purple-500/20",
};

export function ProvenReliability() {
  return (
    <section id="reliability" className="py-20 sm:py-28 bg-card/30">
      <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
        <div className="text-center mb-12">
          <h2 className="text-3xl sm:text-4xl font-bold tracking-tight text-balance">
            Validated against real open-source PRs
          </h2>
          <p className="mt-4 text-lg text-muted-foreground max-w-2xl mx-auto text-pretty">
            GauntletCI rules were developed by running the engine against historical pull
            requests from major .NET OSS projects. These are the findings that would have
            been caught before merge.
          </p>
        </div>

        <div className="grid sm:grid-cols-2 lg:grid-cols-3 gap-5">
          {studies.map((s) => (
            <div key={s.project} className="rounded-xl border border-border bg-card overflow-hidden flex flex-col">
              <div className="flex items-center justify-between gap-3 px-5 py-4 border-b border-border">
                <div className="flex items-center gap-2.5">
                  <div className="h-7 w-7 rounded bg-secondary flex items-center justify-center shrink-0">
                    <span className="text-xs font-mono text-cyan-400">{s.project.charAt(0).toUpperCase()}</span>
                  </div>
                  <span className="font-mono text-sm font-medium">{s.project}</span>
                </div>
                <div className="flex items-center gap-2">
                  <span className={`text-xs font-medium px-2 py-0.5 rounded-full border ${categoryColor[s.category]}`}>
                    {s.tag}
                  </span>
                </div>
              </div>
              <div className="p-5 space-y-3 flex-1">
                <div>
                  <p className="text-xs font-semibold text-muted-foreground/50 uppercase tracking-widest mb-1">The change</p>
                  <p className="text-sm text-muted-foreground leading-relaxed">{s.scenario}</p>
                </div>
                <div>
                  <p className="text-xs font-semibold text-cyan-400/70 uppercase tracking-widest mb-1">What GauntletCI flagged</p>
                  <p className="text-sm text-foreground/80 leading-relaxed">{s.finding}</p>
                </div>
              </div>
              <div className="px-5 py-3 border-t border-border">
                <Link href={s.link} className="text-xs text-muted-foreground/50 hover:text-cyan-400 transition-colors">
                  See annotated detection examples →
                </Link>
              </div>
            </div>
          ))}
        </div>

        <div className="mt-8 flex flex-col sm:flex-row items-center justify-center gap-4 text-sm text-muted-foreground">
          <Link href="/detections" className="text-cyan-400 hover:text-cyan-300 transition-colors">
            See annotated detection examples →
          </Link>
          <span className="hidden sm:inline text-muted-foreground/30">&bull;</span>
          <Link href="/benchmark" className="text-cyan-400 hover:text-cyan-300 transition-colors">
            View benchmark results →
          </Link>
          <span className="hidden sm:inline text-muted-foreground/30">&bull;</span>
          <Link href="/releases" className="text-cyan-400 hover:text-cyan-300 transition-colors">
            Version history →
          </Link>
        </div>
      </div>
    </section>
  );
}


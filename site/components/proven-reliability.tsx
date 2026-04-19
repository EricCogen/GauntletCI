import { CheckCircle2 } from "lucide-react";

export function ProvenReliability() {
  const validations = [
    {
      project: "dotnet/efcore",
      finding: "O(n²) performance risk (LINQ in loops)",
    },
    {
      project: "StackExchange.Redis",
      finding: "Context mutation in property getter",
    },
    {
      project: "Dapper",
      finding: "Null-forgiving operator misuse",
    },
    {
      project: "SharpCompress",
      finding: "Numeric overflow risk",
    },
    {
      project: "AngleSharp",
      finding: "Enum member removal breaking serialization",
    },
  ];

  return (
    <section id="reliability" className="py-20 sm:py-28 bg-card/30">
      <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
        <div className="grid lg:grid-cols-2 gap-12 lg:gap-16 items-center">
          <div>
            <h2 className="text-3xl sm:text-4xl font-bold tracking-tight text-balance">
              Validated against real-world pull requests
            </h2>
            <p className="mt-4 text-lg text-muted-foreground leading-relaxed text-pretty">
              GauntletCI rules have been tested against production codebases from major 
              open-source .NET projects, catching issues that regular code review missed.
            </p>
            <div className="mt-8 space-y-4">
              <div className="flex items-start gap-3">
                <CheckCircle2 className="h-5 w-5 text-cyan-400 mt-0.5 shrink-0" />
                <span className="text-muted-foreground">Detects behavior changes without test coverage</span>
              </div>
              <div className="flex items-start gap-3">
                <CheckCircle2 className="h-5 w-5 text-cyan-400 mt-0.5 shrink-0" />
                <span className="text-muted-foreground">Catches API and serialization breaking changes</span>
              </div>
              <div className="flex items-start gap-3">
                <CheckCircle2 className="h-5 w-5 text-cyan-400 mt-0.5 shrink-0" />
                <span className="text-muted-foreground">Identifies security vulnerabilities early</span>
              </div>
            </div>
          </div>
          
          <div className="rounded-lg border border-border bg-card overflow-hidden">
            <div className="border-b border-border bg-secondary/50 px-4 py-3">
              <span className="text-sm font-medium">Proven Detections</span>
            </div>
            <div className="divide-y divide-border">
              {validations.map((item, index) => (
                <div key={index} className="flex items-center justify-between gap-4 px-4 py-4">
                  <div className="flex items-center gap-3 min-w-0">
                    <div className="h-8 w-8 rounded bg-secondary flex items-center justify-center shrink-0">
                      <span className="text-xs font-mono text-cyan-400">{item.project.charAt(0)}</span>
                    </div>
                    <span className="font-mono text-sm truncate">{item.project}</span>
                  </div>
                  <span className="text-sm text-muted-foreground text-right">{item.finding}</span>
                </div>
              ))}
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}

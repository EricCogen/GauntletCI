import { AlertTriangle, Database, Cpu, Shield, Activity, GitBranch, TestTube } from "lucide-react";

export function DetectionRules() {
  const categories = [
    {
      icon: AlertTriangle,
      title: "Behavior & Contracts",
      description: "Behavior changes without tests, API and serialization changes",
    },
    {
      icon: Shield,
      title: "Security",
      description: "SQL injection risks, hardcoded secrets, PII exposure",
    },
    {
      icon: Database,
      title: "Data Integrity",
      description: "Numeric truncation/overflow risks, state mutation issues",
    },
    {
      icon: Cpu,
      title: "Async & Concurrency",
      description: "Blocking async calls, disposable leaks",
    },
    {
      icon: Activity,
      title: "Observability",
      description: "Missing logging, silent failures",
    },
    {
      icon: GitBranch,
      title: "Architecture",
      description: "Structural changes that impact system design",
    },
    {
      icon: TestTube,
      title: "Test Quality",
      description: "Test coverage gaps, assertion quality",
    },
  ];

  return (
    <section id="detection-rules" className="py-20 sm:py-28 bg-card/30">
      <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
        <div className="text-center mb-16">
          <h2 className="text-3xl sm:text-4xl font-bold tracking-tight text-balance">
            30 built-in detection rules
          </h2>
          <p className="mt-4 text-lg text-muted-foreground max-w-2xl mx-auto text-pretty">
            Comprehensive coverage across behavioral risk categories.
          </p>
        </div>
        
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
          {categories.map((category, index) => (
            <div
              key={index}
              className="flex items-start gap-3 rounded-lg border border-border bg-card p-4 transition-colors hover:border-cyan-500/50"
            >
              <div className="h-9 w-9 rounded bg-gradient-to-br from-cyan-500/20 to-blue-500/20 border border-cyan-500/30 flex items-center justify-center shrink-0">
                <category.icon className="h-4 w-4 text-cyan-400" />
              </div>
              <div className="min-w-0">
                <h3 className="font-medium text-sm">{category.title}</h3>
                <p className="mt-1 text-xs text-muted-foreground leading-relaxed">
                  {category.description}
                </p>
              </div>
            </div>
          ))}
        </div>
        
        <div className="mt-12 rounded-xl border border-border bg-card p-6 sm:p-8">
          <div className="mb-6">
            <h3 className="text-lg font-semibold">What GauntletCI is not</h3>
            <p className="mt-1 text-sm text-muted-foreground">
              It focuses only on change-risk in your diff. It does not replace these tools.
            </p>
          </div>
          <div className="grid gap-3 sm:grid-cols-3">
            {[
              { label: "Not a linter", detail: "It does not enforce style, formatting, or naming conventions." },
              { label: "Not a test runner", detail: "It does not execute your tests or measure code coverage." },
              { label: "Not a SAST scanner", detail: "It does not scan your entire codebase for known vulnerability patterns." },
            ].map((item) => (
              <div key={item.label} className="flex items-start gap-3 rounded-lg border border-border bg-background/50 px-4 py-3">
                <span className="mt-0.5 text-muted-foreground/50 text-lg font-bold leading-none select-none">x</span>
                <div>
                  <p className="text-sm font-semibold text-foreground">{item.label}</p>
                  <p className="mt-0.5 text-xs text-muted-foreground leading-relaxed">{item.detail}</p>
                </div>
              </div>
            ))}
          </div>
        </div>
      </div>
    </section>
  );
}

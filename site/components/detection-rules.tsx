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
    <section className="py-20 sm:py-28 bg-card/30">
      <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
        <div className="text-center mb-16">
          <h2 className="text-3xl sm:text-4xl font-bold tracking-tight text-balance">
            28 built-in detection rules
          </h2>
          <p className="mt-4 text-lg text-muted-foreground max-w-2xl mx-auto text-pretty">
            Comprehensive coverage across behavioral risk categories. Rule IDs range from 
            GCI0001-GCI0050.
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
        
        <div className="mt-12 rounded-lg border border-border bg-card p-6 sm:p-8">
          <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
            <div>
              <h3 className="text-lg font-semibold">What GauntletCI is not</h3>
              <p className="mt-1 text-sm text-muted-foreground">
                Not a linter, static analysis replacement, test runner, or formatter. 
                Focuses only on change-risk.
              </p>
            </div>
            <div className="flex flex-wrap gap-2">
              {["Not a linter", "Not a formatter", "Not a test runner"].map((item, index) => (
                <span
                  key={index}
                  className="inline-flex items-center rounded-full bg-secondary px-3 py-1 text-xs text-muted-foreground"
                >
                  {item}
                </span>
              ))}
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}

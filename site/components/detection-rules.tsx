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

        <div className="mt-10 text-center">
          <a href="/docs/rules" className="inline-flex items-center gap-2 text-sm text-cyan-400 hover:text-cyan-300 transition-colors">
            Browse all rules
            <svg xmlns="http://www.w3.org/2000/svg" className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M9 5l7 7-7 7" />
            </svg>
          </a>
        </div>

      </div>
    </section>
  );
}

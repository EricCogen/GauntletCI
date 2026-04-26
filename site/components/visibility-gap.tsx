import { CheckCircle2, Eye, Code2, Shield } from "lucide-react";

export function VisibilityGap() {
  return (
    <section id="visibility-gap" className="py-24 sm:py-32">
      <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
        <div className="mx-auto max-w-3xl text-center mb-12">
          <p className="text-sm font-semibold text-cyan-400 uppercase tracking-widest mb-4">The Visibility Gap</p>
          <h2 className="text-3xl font-bold tracking-tight sm:text-4xl text-balance">
            You have tests. You have linters. You still have regressions.
          </h2>
          <p className="mt-6 text-lg text-muted-foreground leading-relaxed text-pretty">
            Most development pipelines validate code quality, security, and known test cases.
            They do not validate the behavioral impact of the change itself.
          </p>
        </div>

        <div className="mx-auto max-w-5xl grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-6 mb-16">
          <div className="flex flex-col gap-3 rounded-xl border border-border bg-card p-6">
            <div className="h-10 w-10 rounded-full bg-green-500/10 flex items-center justify-center">
              <CheckCircle2 className="h-5 w-5 text-green-400" />
            </div>
            <p className="font-semibold text-foreground">Tests</p>
            <p className="text-sm text-muted-foreground leading-relaxed">
              Confirm expected behavior that someone remembered to test. They do not prove that
              new behavior is safe.
            </p>
          </div>
          <div className="flex flex-col gap-3 rounded-xl border border-border bg-card p-6">
            <div className="h-10 w-10 rounded-full bg-blue-500/10 flex items-center justify-center">
              <Code2 className="h-5 w-5 text-blue-400" />
            </div>
            <p className="font-semibold text-foreground">Linters and static analysis</p>
            <p className="text-sm text-muted-foreground leading-relaxed">
              Find style issues, code smells, and known risky patterns. They do not reason about
              the intent of a specific diff.
            </p>
          </div>
          <div className="flex flex-col gap-3 rounded-xl border border-border bg-card p-6">
            <div className="h-10 w-10 rounded-full bg-violet-500/10 flex items-center justify-center">
              <Eye className="h-5 w-5 text-violet-400" />
            </div>
            <p className="font-semibold text-foreground">Code review</p>
            <p className="text-sm text-muted-foreground leading-relaxed">
              Helps humans understand whether the change looks reasonable. Reviewers still miss
              subtle runtime behavior shifts.
            </p>
          </div>
          <div className="flex flex-col gap-3 rounded-xl border border-cyan-500/40 bg-cyan-500/5 p-6">
            <div className="h-10 w-10 rounded-full bg-cyan-500/20 flex items-center justify-center">
              <Shield className="h-5 w-5 text-cyan-400" />
            </div>
            <p className="font-semibold text-cyan-400">GauntletCI</p>
            <p className="text-sm text-muted-foreground leading-relaxed">
              Analyzes the diff itself and flags Behavioral Change Risk before the change reaches
              review, CI, or production.
            </p>
          </div>
        </div>

        <blockquote className="mx-auto max-w-3xl text-center mb-8">
          <p className="text-xl sm:text-2xl font-medium text-foreground leading-relaxed">
            &quot;Tests tell you if the code works. Diffs tell you what changed. GauntletCI tells you if your intent is still intact.&quot;
          </p>
        </blockquote>

        <p className="mx-auto max-w-2xl text-center text-muted-foreground">
          GauntletCI closes the gap between{" "}
          <code className="text-foreground/80 bg-muted px-1.5 py-0.5 rounded text-sm">the build is green</code>
          {" "}and{" "}
          <code className="text-foreground/80 bg-muted px-1.5 py-0.5 rounded text-sm">this change is safe</code>.
        </p>
      </div>
    </section>
  );
}

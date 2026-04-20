import { CheckCircle2, Eye, AlertTriangle } from "lucide-react";

export function VisibilityGap() {
  return (
    <section id="visibility-gap" className="py-24 sm:py-32">
      <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
        <div className="mx-auto max-w-3xl text-center mb-16">
          <p className="text-sm font-semibold text-cyan-400 uppercase tracking-widest mb-4">The Visibility Gap</p>
          <h2 className="text-3xl font-bold tracking-tight sm:text-4xl text-balance">
            You Have Tests. You Have Linters. You Still Have Regressions.
          </h2>
        </div>

        <blockquote className="mx-auto max-w-3xl text-center mb-16">
          <p className="text-xl sm:text-2xl font-medium text-foreground leading-relaxed">
            &quot;Tests tell you if the code works. Diffs tell you what changed. GauntletCI tells you if your intent is still intact.&quot;
          </p>
        </blockquote>

        <div className="mx-auto max-w-4xl grid grid-cols-1 md:grid-cols-3 gap-8 text-center">
          <div className="flex flex-col items-center gap-3">
            <div className="h-12 w-12 rounded-full bg-green-500/10 flex items-center justify-center">
              <CheckCircle2 className="h-6 w-6 text-green-400" />
            </div>
            <p className="font-semibold text-foreground">Tests pass</p>
            <p className="text-sm text-muted-foreground">Your test suite confirms existing behavior is unchanged. It cannot confirm that new behavior is safe.</p>
          </div>
          <div className="flex flex-col items-center gap-3">
            <div className="h-12 w-12 rounded-full bg-blue-500/10 flex items-center justify-center">
              <Eye className="h-6 w-6 text-blue-400" />
            </div>
            <p className="font-semibold text-foreground">Diff looks clean</p>
            <p className="text-sm text-muted-foreground">The change is small, readable, and passes review. The risk is in what the change does at runtime, not what it looks like on screen.</p>
          </div>
          <div className="flex flex-col items-center gap-3">
            <div className="h-12 w-12 rounded-full bg-destructive/10 flex items-center justify-center">
              <AlertTriangle className="h-6 w-6 text-destructive" />
            </div>
            <p className="font-semibold text-foreground">Logic shifts silently</p>
            <p className="text-sm text-muted-foreground">An estimated 80% of production regressions originate from changes that looked safe in review. The compiler approved them. The tests passed. The behavior changed.</p>
          </div>
        </div>
      </div>
    </section>
  );
}

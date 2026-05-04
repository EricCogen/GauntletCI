import Link from "next/link";
import { Button } from "@/components/ui/button";
import { ArrowRight, Shield } from "lucide-react";
import { addUtmParams } from "@/lib/utils";

export function Hero() {
  return (
    <section id="hero" className="relative pt-32 pb-20 sm:pt-40 sm:pb-28 overflow-hidden">
      <div className="absolute inset-0 bg-[linear-gradient(rgba(255,255,255,0.02)_1px,transparent_1px),linear-gradient(90deg,rgba(255,255,255,0.02)_1px,transparent_1px)] bg-[size:64px_64px]" />

      <div className="relative mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
        <div className="flex flex-col items-center text-center">
          <div className="inline-flex items-center gap-2 rounded-full border border-cyan-500/30 bg-cyan-500/10 px-4 py-1.5 text-sm text-muted-foreground mb-8">
            <Shield className="h-4 w-4 text-cyan-400" />
            <span>Pre-commit Behavioral Change Risk detection</span>
          </div>

          <h1 className="max-w-4xl text-4xl font-bold tracking-tight sm:text-6xl lg:text-7xl">
            Your tests passed. Your PR was approved.
            <br className="hidden sm:block" />{" "}
            <span className="text-red-400/90">Your change still broke production.</span>
          </h1>

          <p className="mt-6 max-w-2xl text-xl text-muted-foreground leading-relaxed text-pretty">
            Tests confirm expected behavior. Code review confirms intent.
            Neither validates what your change actually does at runtime.
          </p>

          <p className="mt-4 max-w-2xl text-base text-muted-foreground leading-relaxed text-pretty">
            GauntletCI detects Behavioral Change Risk in pull request diffs, identifying logic shifts,
            missing validations, and hidden regressions before they merge.
          </p>

          <p className="mt-3 max-w-2xl text-base text-muted-foreground leading-relaxed text-pretty">
            Detect breaking changes, regressions, and behavioral drift that pass tests and code review.
          </p>

          <p className="mt-3 text-sm text-muted-foreground/70">
            Built for .NET and C# teams running diff-aware validation before commit and before merge.
          </p>

          <div className="mt-14 grid grid-cols-1 md:grid-cols-3 gap-6 max-w-4xl w-full text-left">
            <div className="rounded-lg border border-border bg-card p-5">
              <p className="text-xs font-semibold text-cyan-400 uppercase tracking-widest mb-2">The Problem: Diffs are Deceptive</p>
              <p className="text-sm text-muted-foreground leading-relaxed">
                A diff can look clean, compile successfully, and pass every unit test while still
                changing runtime behavior. The risk is not always in the code that looks wrong.
                It is often in the assumption that changed quietly.
              </p>
            </div>
            <div className="rounded-lg border border-border bg-card p-5">
              <p className="text-xs font-semibold text-yellow-400 uppercase tracking-widest mb-2">The Risk: Behavioral Change Risk</p>
              <p className="text-sm text-muted-foreground leading-relaxed">
                Behavioral Change Risk appears when a small edit changes a contract, branch, exception
                path, validation rule, or side effect without an equally clear test or review signal.
              </p>
            </div>
            <div className="rounded-lg border border-border bg-card p-5">
              <p className="text-xs font-semibold text-green-400 uppercase tracking-widest mb-2">The Solution: Diff-first Behavioral Change Risk validation</p>
              <p className="text-sm text-muted-foreground leading-relaxed">
                GauntletCI acts as an automated auditor for the change itself. It flags unintended
                side effects, broken assumptions, and unvalidated behavior shifts before they leave
                your machine or reach a pull request.
              </p>
            </div>
          </div>

          <div className="mt-14 flex flex-col sm:flex-row items-center justify-center gap-4">
            <Button size="lg" asChild className="bg-cyan-500 hover:bg-cyan-600 text-black font-semibold">
              <Link href={addUtmParams("#pricing", "hero", "cta_button", "install_now")}>
                Install Now
                <ArrowRight className="ml-2 h-4 w-4" />
              </Link>
            </Button>
            <Button variant="outline" size="lg" asChild>
              <Link href={addUtmParams("/docs", "hero", "cta_button", "explore_docs")}>
                Explore Docs
              </Link>
            </Button>
          </div>
        </div>
      </div>
    </section>
  );
}

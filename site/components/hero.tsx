import Link from "next/link";
import { Button } from "@/components/ui/button";
import { ArrowRight, Shield } from "lucide-react";

export function Hero() {
  return (
    <section id="hero" className="relative pt-32 pb-20 sm:pt-40 sm:pb-28 overflow-hidden">
      <div className="absolute inset-0 bg-[linear-gradient(rgba(255,255,255,0.02)_1px,transparent_1px),linear-gradient(90deg,rgba(255,255,255,0.02)_1px,transparent_1px)] bg-[size:64px_64px]" />

      <div className="relative mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
        <div className="flex flex-col items-center text-center">
          <div className="inline-flex items-center gap-2 rounded-full border border-cyan-500/30 bg-cyan-500/10 px-4 py-1.5 text-sm text-muted-foreground mb-8">
            <Shield className="h-4 w-4 text-cyan-400" />
            <span>Pre-commit change-risk detection</span>
          </div>

          <h1 className="max-w-4xl text-4xl font-bold tracking-tight sm:text-6xl lg:text-7xl text-balance">
            Does Your Code Still Do What You Think It Does?
          </h1>

          <p className="mt-6 max-w-2xl text-xl text-muted-foreground leading-relaxed text-pretty">
            GauntletCI identifies the high-risk logic shifts hidden in your diffs.
          </p>

          <div className="mt-14 grid grid-cols-1 md:grid-cols-3 gap-6 max-w-4xl w-full text-left">
            <div className="rounded-lg border border-border bg-card p-5">
              <p className="text-xs font-semibold text-cyan-400 uppercase tracking-widest mb-2">The Problem: Diffs are Deceptive</p>
              <p className="text-sm text-muted-foreground leading-relaxed">
                A clean change can look perfectly safe to your compiler and pass every unit test, while silently introducing new exception paths, orphaning legacy data, or shifting critical business rules.
              </p>
            </div>
            <div className="rounded-lg border border-border bg-card p-5">
              <p className="text-xs font-semibold text-yellow-400 uppercase tracking-widest mb-2">The Risk: Behavioral Shifts</p>
              <p className="text-sm text-muted-foreground leading-relaxed">
                These shifts are the primary cause of production regressions. They represent a gap in your defense where code is syntactically correct but logically dangerous.
              </p>
            </div>
            <div className="rounded-lg border border-border bg-card p-5">
              <p className="text-xs font-semibold text-green-400 uppercase tracking-widest mb-2">The Solution: Automated Auditing</p>
              <p className="text-sm text-muted-foreground leading-relaxed">
                GauntletCI acts as an automated auditor for your pull requests. It flags unintended side effects and broken assumptions before they leave your machine, ensuring your logic matches your intent.
              </p>
            </div>
          </div>

          <div className="mt-10 flex flex-col sm:flex-row items-center gap-4">
            <div className="flex items-center gap-2 rounded-lg border border-border bg-card px-5 py-3 font-mono text-sm select-all">
              <span className="text-cyan-400">$</span>
              <span className="text-foreground">dotnet tool install -g GauntletCI</span>
            </div>
            <Button variant="outline" size="lg" asChild>
              <Link href="#detection-rules">
                View the Rule Library (GCI0001-GCI0037)
                <ArrowRight className="ml-2 h-4 w-4" />
              </Link>
            </Button>
          </div>
        </div>
      </div>
    </section>
  );
}

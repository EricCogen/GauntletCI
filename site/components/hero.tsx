import Link from "next/link";
import { Button } from "@/components/ui/button";
import { ArrowRight, Github, Shield } from "lucide-react";

export function Hero() {
  return (
    <section id="hero" className="relative pt-32 pb-20 sm:pt-40 sm:pb-28 overflow-hidden">
      {/* Subtle grid background */}
      <div className="absolute inset-0 bg-[linear-gradient(rgba(255,255,255,0.02)_1px,transparent_1px),linear-gradient(90deg,rgba(255,255,255,0.02)_1px,transparent_1px)] bg-[size:64px_64px]" />
      
      <div className="relative mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
        <div className="flex flex-col items-center text-center">
          <div className="inline-flex items-center gap-2 rounded-full border border-cyan-500/30 bg-cyan-500/10 px-4 py-1.5 text-sm text-muted-foreground mb-8">
            <Shield className="h-4 w-4 text-cyan-400" />
            <span>Pre-commit change-risk detection</span>
          </div>
          
          <h1 className="max-w-4xl text-4xl font-bold tracking-tight sm:text-6xl lg:text-7xl text-balance">
            <span className="text-foreground">Catch risky changes </span>
            <span className="bg-gradient-to-r from-cyan-400 to-blue-500 bg-clip-text text-transparent">before code review</span>
          </h1>
          
          <p className="mt-6 max-w-2xl text-lg text-muted-foreground leading-relaxed text-pretty">
            GauntletCI analyzes what changed in your pull request and flags unverified behavioral 
            changes — before they slip through code review. Sub-second analysis, runs locally, 
            high-signal output.
          </p>
          
          <div className="mt-10 flex flex-col sm:flex-row items-center gap-4">
            <Button size="lg" asChild>
              <Link href="#pricing">
                Pricing
                <ArrowRight className="ml-2 h-4 w-4" />
              </Link>
            </Button>
            <Button variant="outline" size="lg" asChild>
              <Link href="https://github.com/ericcogen/gauntletci" target="_blank" rel="noopener noreferrer">
                <Github className="mr-2 h-4 w-4" />
                View on GitHub
              </Link>
            </Button>
          </div>
          
          <div className="mt-16 w-full max-w-3xl">
            <div className="rounded-lg border border-border bg-card p-4 sm:p-6 font-mono text-sm">
              <div className="flex items-center gap-2 text-muted-foreground mb-4">
                <div className="flex gap-1.5">
                  <div className="h-3 w-3 rounded-full bg-destructive/60" />
                  <div className="h-3 w-3 rounded-full bg-yellow-500/60" />
                  <div className="h-3 w-3 rounded-full bg-cyan-500/60" />
                </div>
                <span className="ml-2">terminal</span>
              </div>
              <div className="space-y-2 text-left">
                <p className="text-muted-foreground">
                  <span className="text-cyan-400">$</span> dotnet tool install -g GauntletCI
                </p>
                <p className="text-muted-foreground">
                  <span className="text-cyan-400">$</span> gauntletci analyze --staged
                </p>
                <p className="text-foreground mt-4">GauntletCI v2.0.0</p>
                <p className="text-muted-foreground">Analyzing staged changes...</p>
                <p className="text-foreground mt-2">Findings</p>
                <p className="text-muted-foreground">--------</p>
                <p><span className="text-destructive">[BLOCK]</span> Removed logic without tests</p>
                <p><span className="text-yellow-500">[WARN]</span> Missing input validation</p>
                <p className="text-foreground mt-2">Result</p>
                <p className="text-muted-foreground">------</p>
                <p className="text-muted-foreground">Exit code: <span className="text-destructive">1</span></p>
              </div>
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}

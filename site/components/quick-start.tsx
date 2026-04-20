"use client";

import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Check, Copy } from "lucide-react";

export function QuickStart() {
  const [copiedIndex, setCopiedIndex] = useState<number | null>(null);

  const commands = [
    { label: "Install", command: "dotnet tool install -g GauntletCI" },
    { label: "Analyze staged", command: "gauntletci analyze --staged" },
    { label: "Analyze commit", command: "gauntletci analyze --commit <sha>" },
    { label: "Create baseline", command: "gauntletci baseline create" },
  ];

  const copyToClipboard = (text: string, index: number) => {
    navigator.clipboard.writeText(text);
    setCopiedIndex(index);
    setTimeout(() => setCopiedIndex(null), 2000);
  };

  return (
    <section id="quickstart" className="py-20 sm:py-28">
      <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
        <div className="text-center mb-16">
          <h2 className="text-3xl sm:text-4xl font-bold tracking-tight text-balance">
            Get started in 5 minutes
          </h2>
          <p className="mt-4 text-lg text-muted-foreground max-w-2xl mx-auto text-pretty">
            Install the tool, run it on your current changes, and see up to 3 high-signal findings. 
            No setup required.
          </p>
        </div>
        
        <div className="max-w-2xl mx-auto space-y-4">
          {commands.map((item, index) => (
            <div
              key={index}
              className="group flex items-center justify-between gap-4 rounded-lg border border-border bg-card p-4 transition-colors hover:border-cyan-500/50"
            >
              <div className="flex items-center gap-4 min-w-0">
                <span className="text-sm text-muted-foreground w-28 shrink-0">{item.label}</span>
                <code className="font-mono text-sm text-foreground truncate">{item.command}</code>
              </div>
              <Button
                variant="ghost"
                size="icon"
                className="shrink-0 h-8 w-8"
                onClick={() => copyToClipboard(item.command, index)}
                aria-label={`Copy ${item.label} command`}
              >
                {copiedIndex === index ? (
                  <Check className="h-4 w-4 text-cyan-400" />
                ) : (
                  <Copy className="h-4 w-4 text-muted-foreground" />
                )}
              </Button>
            </div>
          ))}
        </div>
        
        <div className="mt-12 text-center">
          <p className="text-sm text-muted-foreground">
            Works with .NET 6.0 and later. Supports Windows, macOS, and Linux.
          </p>
        </div>
      </div>
    </section>
  );
}

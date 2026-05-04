"use client";

import { useState, useEffect, useRef } from "react";
import { Zap, Target, ShieldCheck, Lock, GitCommit } from "lucide-react";

const stats = [
  {
    icon: Zap,
    value: "<1s",
    label: "Analysis time",
    sub: "No full build required. Core analysis requires no network.",
    color: "text-cyan-400",
    bg: "bg-cyan-500/10",
    numericValue: null,
  },
  {
    icon: Target,
    value: "≤3",
    label: "Findings per run",
    sub: "High-signal by design.",
    color: "text-violet-400",
    bg: "bg-violet-500/10",
    numericValue: null,
  },
  {
    icon: ShieldCheck,
    value: "30",
    label: "Detection rules",
    sub: "Across 7 risk categories.",
    color: "text-emerald-400",
    bg: "bg-emerald-500/10",
    numericValue: 30,
  },
  {
    icon: Lock,
    value: "100%",
    label: "Local execution",
    sub: "By default, code stays on your machine.",
    color: "text-amber-400",
    bg: "bg-amber-500/10",
    numericValue: 100,
  },
  {
    icon: GitCommit,
    value: "0",
    label: "Cloud dependencies",
    sub: "Core engine can run air-gapped.",
    color: "text-rose-400",
    bg: "bg-rose-500/10",
    numericValue: 0,
  },
];

function CountUpStat({ stat }: { stat: typeof stats[0] }) {
  const [displayValue, setDisplayValue] = useState(0);
  const [hasStarted, setHasStarted] = useState(false);
  const elementRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const observer = new IntersectionObserver(
      ([entry]) => {
        if (entry.isIntersecting && !hasStarted && stat.numericValue !== null) {
          setHasStarted(true);
        }
      },
      { threshold: 0.1 }
    );

    if (elementRef.current) {
      observer.observe(elementRef.current);
    }

    return () => observer.disconnect();
  }, [hasStarted, stat.numericValue]);

  useEffect(() => {
    if (!hasStarted || stat.numericValue === null) return;

    let animationFrame: number;
    let startTime: number;
    const duration = 1500; // ms

    const animate = (timestamp: number) => {
      if (!startTime) startTime = timestamp;
      const elapsed = timestamp - startTime;
      const progress = Math.min(elapsed / duration, 1);
      const current = Math.floor(progress * stat.numericValue);
      setDisplayValue(current);

      if (progress < 1) {
        animationFrame = requestAnimationFrame(animate);
      }
    };

    animationFrame = requestAnimationFrame(animate);
    return () => cancelAnimationFrame(animationFrame);
  }, [hasStarted, stat.numericValue]);

  const displayText = stat.numericValue !== null ? displayValue + (stat.value.includes("%") ? "%" : "") : stat.value;

  return (
    <div
      ref={elementRef}
      className="flex flex-col items-center justify-center gap-3 py-8 sm:py-10 px-4 text-center"
    >
      <div className={`flex h-10 w-10 items-center justify-center rounded-lg border border-border ${stat.bg}`}>
        <stat.icon className={`h-5 w-5 ${stat.color}`} />
      </div>
      <div>
        <div className={`text-3xl sm:text-4xl font-bold ${stat.color}`}>{displayText}</div>
        <div className="mt-1 text-sm font-medium text-foreground/80">{stat.label}</div>
        <div className="mt-0.5 text-xs text-muted-foreground">{stat.sub}</div>
      </div>
    </div>
  );
}

export function Stats() {
  return (
    <section id="stats" className="border-y border-border bg-card/50">
      <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
        <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-5 divide-x divide-y sm:divide-y-0 divide-border">
          {stats.map((stat) => (
            <CountUpStat key={stat.label} stat={stat} />
          ))}
        </div>
      </div>
    </section>
  );
}



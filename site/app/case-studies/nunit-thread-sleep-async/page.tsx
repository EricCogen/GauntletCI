import type { Metadata } from "next";
import Link from "next/link";
import { Header } from "@/components/header";
import { Footer } from "@/components/footer";

export const metadata: Metadata = {
  title: "Case Study: Thread.Sleep in Async Context in NUnit | GauntletCI",
  description:
    "GauntletCI catches Thread.Sleep() inside async test infrastructure in nunit/nunit PR#5192. Blocks thread pool threads and causes starvation under parallel test runs. Rule GCI0016.",
  alternates: { canonical: "/case-studies/nunit-thread-sleep-async" },
  openGraph: { images: [{ url: "/og/case-studies.png", width: 1200, height: 630 }] },
};

const jsonLd = {
  "@context": "https://schema.org",
  "@type": "Article",
  headline: "Case Study: Thread.Sleep in Async Context in NUnit",
  description:
    "GauntletCI catches Thread.Sleep() inside async test infrastructure in nunit/nunit PR#5192. Blocks thread pool threads and causes starvation under parallel test runs. Rule GCI0016.",
  url: "https://gauntletci.com/case-studies/nunit-thread-sleep-async",
  publisher: { "@type": "Organization", name: "GauntletCI", url: "https://gauntletci.com" },
};

const lineColor: Record<string, string> = {
  added: "bg-green-500/10 text-green-300",
  removed: "bg-red-500/10 text-red-300",
  context: "text-muted-foreground/60",
};

const linePrefix: Record<string, string> = {
  added: "+",
  removed: "-",
  context: " ",
};

const diff: { type: string; line: string }[] = [
  { type: "context", line: "// Added in NUnit async test infrastructure" },
  { type: "added", line: "private async Task WaitForConditionAsync(Func<bool> condition)" },
  { type: "added", line: "{" },
  { type: "added", line: "    while (!condition())" },
  { type: "added", line: "    {" },
  { type: "added", line: "        Thread.Sleep(100);  // GCI0016: blocks thread pool thread in async context" },
  { type: "added", line: "        // Should be: await Task.Delay(100);" },
  { type: "added", line: "    }" },
  { type: "added", line: "}" },
  { type: "context", line: "" },
  { type: "context", line: "// Also added:" },
  { type: "added", line: "private static int _retryCount;  // GCI0016: static mutable without synchronization" },
];

const finding = [
  "[GCI0016] Concurrency and State Risk",
  "Summary  : Thread.Sleep() in async context detected - blocks thread pool thread.",
  "Evidence : Thread.Sleep(100); in async method WaitForConditionAsync",
  "Why      : Thread.Sleep blocks the calling thread for the full duration. In async",
  "           contexts this defeats cooperative yielding and contributes to thread",
  "           pool starvation under parallel workloads.",
  "Action   : Replace Thread.Sleep with await Task.Delay() to yield the thread.",
].join("\n");

export default function NUnitThreadSleepAsyncPage() {
  return (
    <>
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLd) }}
      />
      <Header />
      <main className="min-h-screen bg-background pt-24">
        <div className="mx-auto max-w-4xl px-4 sm:px-6 lg:px-8 py-16 sm:py-20 space-y-16">

          {/* Hero */}
          <div className="space-y-5 border-b border-border pb-12">
            <div className="flex items-center justify-between">
              <p className="text-sm font-semibold uppercase tracking-widest text-cyan-400">
                Case Study
              </p>
              <Link
                href="/case-studies"
                className="text-sm text-muted-foreground hover:text-cyan-400 transition-colors"
              >
                &larr; All case studies
              </Link>
            </div>
            <h1 className="text-4xl sm:text-5xl font-bold tracking-tight text-balance">
              Thread.Sleep in Async Context in NUnit
            </h1>
            <div className="flex flex-wrap items-center gap-3">
              <span className="font-mono text-sm text-muted-foreground">
                nunit/nunit
              </span>
              <a
                href="https://github.com/nunit/nunit/pull/5192"
                target="_blank"
                rel="noopener noreferrer"
                className="font-mono text-sm text-cyan-400 hover:text-cyan-300 transition-colors"
              >
                PR#5192 &nearr;
              </a>
            </div>
            <div className="flex flex-wrap gap-2">
              <Link
                href="/docs/rules/GCI0016"
                className="font-mono text-xs font-medium px-2 py-0.5 rounded bg-cyan-500/10 text-cyan-400 border border-cyan-500/20 hover:bg-cyan-500/20 transition-colors"
              >
                GCI0016
              </Link>
              <span className="text-xs font-semibold text-red-400 bg-red-500/10 border border-red-500/20 px-2 py-0.5 rounded-full">
                BLOCK
              </span>
              <span className="text-xs font-medium px-2 py-0.5 rounded-full bg-secondary text-muted-foreground">
                Concurrency
              </span>
              <span className="text-xs font-medium px-2 py-0.5 rounded-full bg-secondary text-muted-foreground">
                Async
              </span>
            </div>
          </div>

          {/* Context */}
          <section className="space-y-4">
            <h2 className="text-2xl font-bold tracking-tight">Context</h2>
            <p className="text-muted-foreground leading-relaxed">
              NUnit PR#5192 introduced a{" "}
              <code className="font-mono text-sm text-foreground/80 bg-muted px-1 py-0.5 rounded">
                Thread.Sleep()
              </code>{" "}
              call inside async test infrastructure code.{" "}
              <code className="font-mono text-sm text-foreground/80 bg-muted px-1 py-0.5 rounded">
                Thread.Sleep
              </code>{" "}
              blocks a thread pool thread for the full sleep duration without
              releasing it. In an async context, this defeats the purpose of async
              - instead of yielding the thread while waiting, the code holds it,
              contributing to thread pool starvation under parallel test execution.
              The same PR also introduced a new static mutable field without
              synchronization.
            </p>
          </section>

          {/* Diff */}
          <section className="space-y-4">
            <h2 className="text-2xl font-bold tracking-tight">Diff evidence</h2>
            <div className="rounded-xl border border-border overflow-hidden">
              <div className="border-b border-border bg-card/60 px-4 py-2 flex items-center gap-2">
                <div className="flex gap-1.5">
                  <div className="w-2.5 h-2.5 rounded-full bg-red-500/40" />
                  <div className="w-2.5 h-2.5 rounded-full bg-amber-500/40" />
                  <div className="w-2.5 h-2.5 rounded-full bg-green-500/40" />
                </div>
                <span className="text-xs font-mono text-muted-foreground/40 ml-1">
                  src/NUnitFramework/framework/Internal/AsyncToSyncAdapter.cs
                </span>
              </div>
              <div className="p-4 font-mono text-xs leading-relaxed space-y-0.5 bg-background/50">
                {diff.map((line, i) => (
                  <div
                    key={i}
                    className={`flex gap-2 px-2 py-0.5 rounded ${lineColor[line.type]}`}
                  >
                    <span className="shrink-0 w-3 select-none">
                      {linePrefix[line.type]}
                    </span>
                    <span className="whitespace-pre">{line.line}</span>
                  </div>
                ))}
              </div>
              <div className="border-t border-border bg-red-500/5 px-4 py-3">
                <pre className="text-xs font-mono text-red-400 leading-relaxed whitespace-pre-wrap">
                  {finding}
                </pre>
              </div>
            </div>
          </section>

          {/* Why it matters */}
          <section className="space-y-4">
            <h2 className="text-2xl font-bold tracking-tight">Why it matters</h2>
            <p className="text-muted-foreground leading-relaxed">
              The irony of a{" "}
              <code className="font-mono text-sm text-foreground/80 bg-muted px-1 py-0.5 rounded">
                Thread.Sleep
              </code>{" "}
              in the NUnit source itself is significant. NUnit is the test
              framework used to validate that code is correct. If the framework's
              own async infrastructure blocks threads, it can cause intermittent
              test timeouts under parallel execution (the default in modern .NET
              test runs), mask real timing bugs because the test environment does
              not match production async behavior, and create false passes or
              failures that developers learn to ignore. Every project using NUnit
              async tests inherits this behavior.
            </p>
          </section>

          {/* Rule link */}
          <section className="border-t border-border pt-10 space-y-4">
            <h2 className="text-lg font-semibold">Detection rule</h2>
            <p className="text-muted-foreground text-sm leading-relaxed">
              This finding is produced by{" "}
              <Link
                href="/docs/rules/GCI0016"
                className="text-cyan-400 hover:text-cyan-300 transition-colors"
              >
                GCI0016 - Concurrency and State Risk
              </Link>
              . The rule flags{" "}
              <code className="font-mono text-sm text-foreground/80 bg-muted px-1 py-0.5 rounded">
                Thread.Sleep
              </code>{" "}
              inside async methods, unsynchronized static mutable state, and other
              patterns that cause thread pool starvation or data races.
            </p>
          </section>

          {/* Navigation */}
          <div className="border-t border-border pt-10 flex flex-col sm:flex-row gap-4">
            <Link
              href="/case-studies"
              className="inline-flex items-center gap-2 rounded-lg border border-border bg-card px-6 py-3 text-sm font-semibold hover:bg-card/80 transition-colors"
            >
              &larr; All case studies
            </Link>
            <Link
              href="/docs"
              className="inline-flex items-center justify-center gap-2 rounded-lg bg-cyan-500 px-6 py-3 text-sm font-semibold text-background hover:bg-cyan-400 transition-colors"
            >
              Get started free
            </Link>
          </div>

        </div>
      </main>
      <Footer />
    </>
  );
}

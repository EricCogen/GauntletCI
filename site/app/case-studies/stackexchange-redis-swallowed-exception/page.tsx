import type { Metadata } from "next";
import Link from "next/link";
import JsonLd from "@/components/json-ld";
import { Header } from "@/components/header";
import { Footer } from "@/components/footer";

export const metadata: Metadata = {
  title: "Case Study: Swallowed Exception in StackExchange.Redis | GauntletCI",
  description:
    "GauntletCI catches a bare catch {} block in StackExchange.Redis PR#2995 that silently drops all exceptions in the message dispatch loop. Rule GCI0007.",
  alternates: { canonical: "/case-studies/stackexchange-redis-swallowed-exception" },
  openGraph: { images: [{ url: "/og/case-studies.png", width: 1200, height: 630 }] },
};

const jsonLd = {
  "@context": "https://schema.org",
  "@type": "Article",
  headline: "Case Study: Swallowed Exception in StackExchange.Redis",
  description:
    "GauntletCI catches a bare catch {} block in StackExchange.Redis PR#2995 that silently drops all exceptions in the message dispatch loop. Rule GCI0007.",
  url: "https://gauntletci.com/case-studies/stackexchange-redis-swallowed-exception",
  author: { "@type": "Organization", name: "GauntletCI" },
  publisher: { "@type": "Organization", name: "GauntletCI", url: "https://gauntletci.com" },
  datePublished: "2025-05-03"
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
  { type: "context", line: "// Before" },
  { type: "context", line: "public async Task WriteAsync(ChannelMessage message)" },
  { type: "context", line: "{" },
  { type: "context", line: "    await _channel.Writer.WriteAsync(message);" },
  { type: "context", line: "}" },
  { type: "context", line: "" },
  { type: "context", line: "// After - bare catch added in the dispatch loop" },
  { type: "context", line: "private async Task ProcessAsync()" },
  { type: "context", line: "{" },
  { type: "context", line: "    try" },
  { type: "context", line: "    {" },
  { type: "context", line: "        while (await _channel.Reader.WaitToReadAsync())" },
  { type: "context", line: "        {" },
  { type: "context", line: "            var message = _channel.Reader.TryRead(out var msg) ? msg : default;" },
  { type: "context", line: "            await _handler(message);" },
  { type: "context", line: "        }" },
  { type: "context", line: "    }" },
  { type: "added", line: "    catch { }  // <-- GCI0007: swallowed exception" },
  { type: "context", line: "}" },
];

const finding = [
  "[GCI0007] Error Handling Integrity (4 occurrences)",
  "Location : src/StackExchange.Redis/ChannelMessageQueue.cs (lines 140, 148, 272, 284)",
  "Summary  : Swallowed exception detected",
  "Evidence : catch { } // matches MessageCompletable",
  "Why      : Empty or silent catch blocks hide failures, making bugs invisible and",
  "           debugging nearly impossible.",
  "Action   : Log the exception, rethrow it, or handle it explicitly.",
].join("\n");

export default function StackExchangeRedisSwallowedExceptionPage() {
  return (
    <>
      <JsonLd data={jsonLd} />
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
                ← All case studies
              </Link>
            </div>
            <h1 className="text-4xl sm:text-5xl font-bold tracking-tight text-balance">
              Swallowed Exception in StackExchange.Redis
            </h1>
            <div className="flex flex-wrap items-center gap-3">
              <span className="font-mono text-sm text-muted-foreground">
                StackExchange/StackExchange.Redis
              </span>
              <a
                href="https://github.com/StackExchange/StackExchange.Redis/pull/2995"
                target="_blank"
                rel="noopener noreferrer"
                className="font-mono text-sm text-cyan-400 hover:text-cyan-300 transition-colors"
              >
                PR#2995 ↗
              </a>
            </div>
            <div className="flex flex-wrap gap-2">
              <Link
                href="/docs/rules/GCI0007"
                className="font-mono text-xs font-medium px-2 py-0.5 rounded bg-cyan-500/10 text-cyan-400 border border-cyan-500/20 hover:bg-cyan-500/20 transition-colors"
              >
                GCI0007
              </Link>
              <span className="text-xs font-semibold text-red-400 bg-red-500/10 border border-red-500/20 px-2 py-0.5 rounded-full">
                BLOCK
              </span>
              <span className="text-xs font-medium px-2 py-0.5 rounded-full bg-secondary text-muted-foreground">
                Error Handling
              </span>
              <span className="text-xs font-medium px-2 py-0.5 rounded-full bg-secondary text-muted-foreground">
                Concurrency
              </span>
            </div>
          </div>

          {/* Context */}
          <section className="space-y-4">
            <h2 className="text-2xl font-bold tracking-tight">Context</h2>
            <p className="text-muted-foreground leading-relaxed">
              StackExchange.Redis PR#2995 refactored{" "}
              <code className="font-mono text-sm text-foreground/80 bg-muted px-1 py-0.5 rounded">
                ChannelMessageQueue.cs
              </code>
              . It added a bare{" "}
              <code className="font-mono text-sm text-foreground/80 bg-muted px-1 py-0.5 rounded">
                catch {"{ }"}
              </code>{" "}
              block that silently swallows all exceptions in the message dispatch
              loop. When an exception occurs in the queue processing, the failure
              is invisible - no log, no rethrow, no signal to callers. The
              connection appears healthy while messages are silently dropped.
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
                  src/StackExchange.Redis/ChannelMessageQueue.cs
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
              StackExchange.Redis is downloaded over 400 million times. A{" "}
              <code className="font-mono text-sm text-foreground/80 bg-muted px-1 py-0.5 rounded">
                catch {"{ }"}
              </code>{" "}
              block in the message dispatch loop means any exception thrown by a
              subscriber handler - including{" "}
              <code className="font-mono text-sm text-foreground/80 bg-muted px-1 py-0.5 rounded">
                OutOfMemoryException
              </code>
              ,{" "}
              <code className="font-mono text-sm text-foreground/80 bg-muted px-1 py-0.5 rounded">
                OperationCanceledException
              </code>{" "}
              on shutdown, or transient network faults - disappears without a
              trace. The queue continues processing, the channel appears healthy,
              and messages are silently dropped. The only indication of a problem
              is that subscribers stop receiving messages, with no exception in
              logs, no error return, and no way to distinguish the failure from a
              quiet period with no traffic.
            </p>
          </section>

          {/* Rule link */}
          <section className="border-t border-border pt-10 space-y-4">
            <h2 className="text-lg font-semibold">Detection rule</h2>
            <p className="text-muted-foreground text-sm leading-relaxed">
              This finding is produced by{" "}
              <Link
                href="/docs/rules/GCI0007"
                className="text-cyan-400 hover:text-cyan-300 transition-colors"
              >
                GCI0007 - Error Handling Integrity
              </Link>
              . The rule flags empty catch blocks, catch blocks that only rethrow,
              and cases where exception handlers are removed relative to the
              previous version of the code.
            </p>
          </section>

          {/* Navigation */}
          <div className="border-t border-border pt-10 flex flex-col sm:flex-row gap-4">
            <Link
              href="/case-studies"
              className="inline-flex items-center gap-2 rounded-lg border border-border bg-card px-6 py-3 text-sm font-semibold hover:bg-card/80 transition-colors"
            >
              ← All case studies
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

import type { Metadata } from "next";
import { CaseStudyLayout, type CaseStudyDiffLine } from "../_components/case-study-layout";

export const metadata: Metadata = {
  title: "Case Study: Swallowed Handler Exceptions in StackExchange.Redis | GauntletCI",
  description:
    "A deep dive into StackExchange.Redis PR #2995, where a keyspace notification feature PR surfaced bare catch blocks that silently discard user handler failures.",
  alternates: { canonical: "/articles/case-studies/stackexchange-redis-swallowed-exception" },
  openGraph: { images: [{ url: "/og/case-studies.png", width: 1200, height: 630 }] },
};

const diffLines: CaseStudyDiffLine[] = [
  { type: "context", line: "// PR #2995: ChannelMessageQueue was restructured during keyspace notification work" },
  { type: "context", line: "private void OnMessageSyncImpl(ChannelMessage next, Action<ChannelMessage>? handler)" },
  { type: "context", line: "{" },
  { type: "context", line: "    try { handler?.Invoke(next); }" },
  { type: "added", line: "    catch { } // matches MessageCompletable" },
  { type: "context", line: "}" },
  { type: "context", line: "" },
  { type: "context", line: "private async Task OnMessageAsyncImpl(ChannelMessage next, Func<ChannelMessage, Task>? handler)" },
  { type: "context", line: "{" },
  { type: "context", line: "    try" },
  { type: "context", line: "    {" },
  { type: "context", line: "        var task = handler?.Invoke(next);" },
  { type: "context", line: "        if (task != null && task.Status != TaskStatus.RanToCompletion) await task.ForAwait();" },
  { type: "context", line: "    }" },
  { type: "added", line: "    catch { } // matches MessageCompletable" },
  { type: "context", line: "}" },
];

const findingBody = [
  "[GCI0007] Error Handling Integrity",
  "Signal   : bare catch block with no log, rethrow, or explicit error path",
  "Location : src/StackExchange.Redis/ChannelMessageQueue.cs",
  "Evidence : catch { } // matches MessageCompletable",
  "Risk     : exceptions thrown by user message handlers are discarded silently",
  "Action   : log, surface through an internal-error hook, or document the intentional swallow",
].join("\n");

export default function StackExchangeRedisSwallowedExceptionPage() {
  return (
    <CaseStudyLayout
      title="Swallowed Handler Exceptions in StackExchange.Redis"
      description="PR #2995 added keyspace notification and cluster support. In the middle of that large feature diff, the message queue restructuring surfaced two bare catch blocks that silently discard exceptions thrown by subscriber handlers."
      canonicalPath="/articles/case-studies/stackexchange-redis-swallowed-exception"
      repo="StackExchange/StackExchange.Redis"
      pr="PR #2995"
      prUrl="https://github.com/StackExchange/StackExchange.Redis/pull/2995"
      outcomeLabel="BLOCK"
      outcomeTone="red"
      tags={["Error Handling", "Pub/Sub", "Infrastructure"]}
      ruleIds={["GCI0007"]}
      stats={[
        { value: "61", label: "files changed" },
        { value: "4,039", label: "lines added" },
        { value: "2", label: "handler swallows surfaced" },
      ]}
      sections={[
        {
          title: "What changed",
          children: (
            <>
              <p>
                The pull request was primarily about keyspace notifications: new APIs, cluster routing,
                and supporting infrastructure. It was not an exception-handling PR. The relevant file,
                <code className="font-mono text-sm text-foreground/80 bg-muted px-1 py-0.5 rounded"> ChannelMessageQueue.cs</code>,
                was restructured as part of the work, so pre-existing handler logic appeared as new
                lines in the diff.
              </p>
              <p>
                That matters because diff-based review operates on what the reviewer sees in the PR. A
                carried-forward catch block can still be the right time to ask whether the behavior is
                acceptable, especially when the surrounding feature changes how messages are delivered.
              </p>
            </>
          ),
        },
        {
          title: "Why this is risky",
          children: (
            <>
              <p>
                The affected code invokes user-provided synchronous and asynchronous message handlers.
                If a handler throws while processing a Redis pub/sub message or cache-invalidation
                notification, the exception is swallowed and the loop continues. There is no log, no
                metric, and no callback to tell the application that its handler failed.
              </p>
              <p>
                In a cache-invalidation path, that can leave stale application state behind while the
                Redis connection still looks healthy. Operators see a quiet system, not a failing one.
              </p>
            </>
          ),
        },
        {
          title: "Why a human reviewer can miss it",
          children: (
            <>
              <p>
                The PR was large and feature-heavy. Review attention naturally goes to the new
                keyspace-notification API, cluster routing, and test coverage. The catch blocks are
                short, visually familiar, and accompanied by a comment saying they match another type.
              </p>
              <p>
                That comment explains intent, but it does not provide operational visibility. GauntletCI
                turns the one-line swallow into a specific review question: is silent handler failure
                still the desired contract for this new message-delivery surface?
              </p>
            </>
          ),
        },
      ]}
      diffTitle="Diff evidence"
      diffFile="src/StackExchange.Redis/ChannelMessageQueue.cs"
      diffLines={diffLines}
      findingTitle="GauntletCI finding"
      findingBody={findingBody}
      caveats={[
        "PR #2995 did not originally introduce these two handler swallows; they appeared as added lines because the file was restructured.",
        "The same PR improved one separate bare catch in a reflection-only count helper by adding Debug.WriteLine.",
        "The comment indicates the swallow is intentional, so the right outcome may be documentation or an explicit internal-error hook rather than a simple rethrow.",
      ]}
      nextActions={[
        "Confirm whether user handler exceptions are intentionally isolated from the Redis message loop.",
        "If the swallow is intentional, route the exception to the multiplexer internal-error mechanism or document the contract prominently.",
        "Add a focused test that proves subscriber exceptions do not silently break cache-invalidation expectations.",
      ]}
      sources={[
        { label: "PR #2995", href: "https://github.com/StackExchange/StackExchange.Redis/pull/2995" },
        {
          label: "Rule GCI0007",
          href: "https://gauntletci.com/docs/rules/GCI0007",
        },
      ]}
    />
  );
}

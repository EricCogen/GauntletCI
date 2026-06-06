import type { Metadata } from "next";
import { CaseStudyLayout, type CaseStudyDiffLine } from "../_components/case-study-layout";

export const metadata: Metadata = {
  title: "Case Study: Paired Implementation Drift in StackExchange.Redis | GauntletCI",
  description:
    "StackExchange.Redis PR #2995 shipped an inverted IsSubscriberConnected predicate in MultiNodeSubscription — the logic defect Greptile and Qodo caught while diff noise hid it from many reviewers.",
  alternates: { canonical: "/articles/case-studies/stackexchange-redis-paired-implementation" },
  openGraph: { images: [{ url: "/og/case-studies.png", width: 1200, height: 630 }] },
};

const diffLines: CaseStudyDiffLine[] = [
  { type: "context", line: "// PR #2995: cluster-aware subscription cleanup in Subscription.cs" },
  { type: "context", line: "internal sealed class SingleNodeSubscription : Subscription" },
  { type: "context", line: "{" },
  { type: "context", line: "    internal override void RemoveDisconnectedEndpoints()" },
  { type: "context", line: "    {" },
  { type: "context", line: "        if (server is { IsSubscriberConnected: false })" },
  { type: "context", line: "            _currentServer = null;" },
  { type: "context", line: "    }" },
  { type: "context", line: "}" },
  { type: "context", line: "" },
  { type: "context", line: "internal sealed class MultiNodeSubscription : Subscription" },
  { type: "context", line: "{" },
  { type: "context", line: "    internal override void RemoveDisconnectedEndpoints()" },
  { type: "context", line: "    {" },
  { type: "context", line: "        foreach (var server in _servers)" },
  { type: "added", line: "            if (server.Value.IsSubscriberConnected)" },
  { type: "added", line: "                scratch[count++] = server.Key;" },
  { type: "context", line: "    }" },
  { type: "context", line: "}" },
];

const findingBody = [
  "[GCI0058] Paired Implementation Consistency",
  "Signal   : sibling classes apply opposite polarity to IsSubscriberConnected in the same method",
  "Location : src/StackExchange.Redis/Subscription.cs",
  "Evidence : SingleNodeSubscription expects IsSubscriberConnected: false; MultiNodeSubscription uses IsSubscriberConnected (true branch)",
  "Risk     : disconnected endpoints are kept (or connected ones removed) during cluster cleanup",
  "Action   : align MultiNodeSubscription with SingleNodeSubscription or document intentional divergence",
].join("\n");

export default function StackExchangeRedisPairedImplementationPage() {
  return (
    <CaseStudyLayout
      title="Paired Implementation Drift in StackExchange.Redis"
      description="PR #2995 added cluster subscription routing across dozens of files. The adjudicated logic bug was not a missing test or a swallowed exception — it was inverted boolean polarity between SingleNodeSubscription and MultiNodeSubscription in RemoveDisconnectedEndpoints()."
      canonicalPath="/articles/case-studies/stackexchange-redis-paired-implementation"
      repo="StackExchange/StackExchange.Redis"
      pr="PR #2995"
      prUrl="https://github.com/StackExchange/StackExchange.Redis/pull/2995"
      outcomeLabel="BLOCK"
      outcomeTone="red"
      tags={["Logic Bug", "Cluster", "Library"]}
      ruleIds={["GCI0058"]}
      stats={[
        { value: "61", label: "files changed" },
        { value: "4,039", label: "lines added" },
        { value: "1", label: "inverted sibling predicate" },
      ]}
      sections={[
        {
          title: "What changed",
          children: (
            <>
              <p>
                The pull request introduced keyspace notifications and multi-node subscription
                management. Two sibling classes implement the same cleanup method:
                <code className="font-mono text-sm text-foreground/80 bg-muted px-1 py-0.5 rounded"> RemoveDisconnectedEndpoints()</code>.
                Single-node mode clears the server when the subscriber is <em>not</em> connected.
                Multi-node mode was supposed to do the equivalent sweep across servers, but the added
                condition kept endpoints when <code className="font-mono text-sm">IsSubscriberConnected</code> was
                true — the opposite polarity.
              </p>
              <p>
                This is a classic copy/paste drift pattern: the method names match, the surrounding
                feature work is correct, and the bug is one negation away from the reference
                implementation.
              </p>
            </>
          ),
        },
        {
          title: "Why this is risky",
          children: (
            <>
              <p>
                Wrong endpoint cleanup in a Redis client does not fail loudly at compile time. Under
                cluster failover or partial disconnects, the library may retain stale server entries
                or drop live ones. Pub/sub routing becomes nondeterministic relative to connection
                state, and application code still sees a healthy multiplexer.
              </p>
              <p>
                Greptile, Qodo, and CodeRabbit surfaced this defect in eval-lab runs. GauntletCI originally
                missed it while emitting dozens of lower-signal findings on the same large diff; GCI0058 now catches it deterministically.
              </p>
            </>
          ),
        },

        {
          title: "Eval-lab comparison (same diff)",
          children: (
            <>
              <p>
                Private eval-lab PR #7 mirrors this diff. Scorecard: eval/scorecards/stackexchange-redis-pr-2995.json (May 2026 harvest).
              </p>
              <table className="w-full text-sm border border-border mt-4">
                <thead>
                  <tr className="bg-muted/50">
                    <th className="text-left p-2">Tool</th>
                    <th className="text-left p-2">Caught inversion</th>
                    <th className="text-left p-2">Notes</th>
                  </tr>
                </thead>
                <tbody>
                  <tr><td className="p-2">GauntletCI</td><td className="p-2">Yes (GCI0058)</td><td className="p-2">16 findings, library profile + delivery cap</td></tr>
                  <tr><td className="p-2">Greptile</td><td className="p-2">Yes</td><td className="p-2">Inverted RemoveDisconnectedEndpoints</td></tr>
                  <tr><td className="p-2">Qodo</td><td className="p-2">Yes</td><td className="p-2">Inline review on Subscription.cs</td></tr>
                  <tr><td className="p-2">CodeRabbit</td><td className="p-2">Yes</td><td className="p-2">Walkthrough cited inverted RemoveDisconnectedEndpoints (eval-lab PR #7 harvest)</td></tr>
                  <tr><td className="p-2">CodeQL</td><td className="p-2">No</td><td className="p-2">Same-defect scoring: no matching alert</td></tr>
                </tbody>
              </table>
            </>
          ),
        },
        {
          title: "How GauntletCI catches it now",
          children: (
            <>
              <p>
                Rule GCI0058 compares sibling classes in the same diff: same method
                name, same boolean predicate, opposite polarity. It is deterministic (not LLM-judged)
                and fires on property patterns like
                <code className="font-mono text-sm"> IsSubscriberConnected: false</code> versus
                <code className="font-mono text-sm"> .IsSubscriberConnected</code> without a dedicated
                Redis hardcode.
              </p>
              <p>
                Platform phases PG-DELIVERY and PG-DOMAIN cap noise on library profiles so this
                finding survives delivery instead of being buried under per-line fanout.
              </p>
            </>
          ),
        },
        {
          title: "Why a human reviewer can miss it",
          children: (
            <>
              <p>
                Reviewers anchor on the new public API and cluster tests. The bug sits in parallel
                internal classes that look intentionally symmetric. Without cross-class comparison,
                the added lines read like reasonable null/state checks rather than an inversion.
              </p>
            </>
          ),
        },
      ]}
      diffTitle="Diff evidence"
      diffFile="src/StackExchange.Redis/Subscription.cs"
      diffLines={diffLines}
      findingTitle="GauntletCI finding"
      findingBody={findingBody}
      caveats={[
        "GCI0058 currently focuses on if/while/ternary conditions; switch expressions and helper-wrapped predicates may require future hardening.",
        "The same PR also contains swallowed-handler issues (see the GCI0007 case study on this PR).",
        "Large feature volume still produces medium-severity edge-case findings (GCI0006) that reviewers should triage separately.",
      ]}
      nextActions={[
        "Verify MultiNodeSubscription removes endpoints when IsSubscriberConnected is false, matching SingleNodeSubscription semantics.",
        "Add a cluster integration test that asserts disconnected servers are evicted from the scratch buffer.",
        "If polarity is intentional, document the asymmetry in XML remarks on both overrides.",
      ]}
      sources={[
        { label: "PR #2995", href: "https://github.com/StackExchange/StackExchange.Redis/pull/2995" },
        { label: "Eval scorecard", href: "https://github.com/EricCogen/GauntletCI/blob/main/eval/redis-2995-scorecard.json" },
        {
          label: "Rule GCI0058",
          href: "https://gauntletci.com/docs/rules/GCI0058",
        },
      ]}
    />
  );
}

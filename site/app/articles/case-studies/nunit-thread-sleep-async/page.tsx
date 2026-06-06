import type { Metadata } from "next";
import { CaseStudyLayout, type CaseStudyDiffLine } from "../_components/case-study-layout";

export const metadata: Metadata = {
  title: "Case Study: Timeout Inheritance Change in NUnit | GauntletCI",
  description:
    "A corrected deep dive into NUnit PR #5192, where a release-branch merge changed timeout attribute inheritance without introducing the Thread.Sleep pattern the old page claimed.",
  alternates: { canonical: "/articles/case-studies/nunit-thread-sleep-async" },
  openGraph: { images: [{ url: "/og/case-studies.png", width: 1200, height: 630 }] },
};

const diffLines: CaseStudyDiffLine[] = [
  { type: "context", line: "// CancelAfterAttribute now propagates from base fixtures/classes" },
  { type: "removed", line: '[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited=false)]' },
  { type: "added", line: '[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]' },
  { type: "context", line: "public class CancelAfterAttribute : PropertyAttribute, IApplyToContext" },
  { type: "context", line: "" },
  { type: "context", line: "// TimeoutAttribute also became inheritable" },
  { type: "removed", line: "[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]" },
  { type: "added", line: "[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Assembly, AllowMultiple = false, Inherited = true)]" },
  { type: "context", line: "#if !NETFRAMEWORK" },
  { type: "context", line: '[Obsolete(".NET No longer supports aborting threads as it is not a safe thing to do...")]' },
];

const findingBody = [
  "Coverage note",
  "Signal   : a one-token attribute metadata change alters derived fixture execution behavior",
  "Reality  : current GCI0016 would not fire; no Thread.Sleep, .Wait(), .Result, lock(this), or async void was added",
  "Lesson   : some high-impact behavior changes are API metadata changes, not obvious code-body hazards",
].join("\n");

export default function NUnitThreadSleepAsyncPage() {
  return (
    <CaseStudyLayout
      title="Timeout Inheritance Change in NUnit"
      description="PR #5192 did not add Thread.Sleep in async code. The real case-study value is subtler: a release-branch merge changed timeout attribute inheritance, silently changing how derived test fixtures receive cancellation and timeout behavior."
      canonicalPath="/articles/case-studies/nunit-thread-sleep-async"
      repo="nunit/nunit"
      pr="PR #5192"
      prUrl="https://github.com/nunit/nunit/pull/5192"
      outcomeLabel="COVERAGE GAP"
      outcomeTone="cyan"
      tags={["Async Tests", "Timeouts", "Rule Design"]}
      ruleIds={["GCI0003"]}
      stats={[
        { value: "70", label: "files changed" },
        { value: "37", label: "commits merged" },
        { value: "0", label: "GCI0016 matches" },
      ]}
      sections={[
        {
          title: "What changed",
          children: (
            <>
              <p>
                PR #5192 was a release/4.6 merge to main. Among platform filter updates and framework
                additions, two attribute declarations changed
                <code className="font-mono text-sm text-foreground/80 bg-muted px-1 py-0.5 rounded"> Inherited = false</code>
                to
                <code className="font-mono text-sm text-foreground/80 bg-muted px-1 py-0.5 rounded"> Inherited = true</code>:
                <code className="font-mono text-sm text-foreground/80 bg-muted px-1 py-0.5 rounded"> CancelAfterAttribute</code>
                and
                <code className="font-mono text-sm text-foreground/80 bg-muted px-1 py-0.5 rounded"> TimeoutAttribute</code>.
              </p>
              <p>
                That means derived fixtures can begin inheriting cancellation or timeout behavior from
                a base class. The change may be correct, but it is externally observable for any test
                suite that depends on NUnit inheritance behavior.
              </p>
            </>
          ),
        },
        {
          title: "Why this is risky",
          children: (
            <>
              <p>
                Timeout and cancellation metadata controls execution, not just documentation. A base
                fixture decorated with
                <code className="font-mono text-sm text-foreground/80 bg-muted px-1 py-0.5 rounded"> [CancelAfter]</code>
                can now change the runtime behavior of every derived test. Long-running async tests may
                start receiving cancellation that they previously ignored.
              </p>
              <p>
                The diff is deceptively small. A reviewer scanning for blocking calls will not see
                <code className="font-mono text-sm text-foreground/80 bg-muted px-1 py-0.5 rounded"> Thread.Sleep</code>
                or
                <code className="font-mono text-sm text-foreground/80 bg-muted px-1 py-0.5 rounded"> .Wait()</code>.
                The risk is encoded in attribute metadata.
              </p>
            </>
          ),
        },
        {
          title: "What the original thin page got wrong",
          children: (
            <>
              <p>
                The old page claimed PR #5192 introduced
                <code className="font-mono text-sm text-foreground/80 bg-muted px-1 py-0.5 rounded"> Thread.Sleep</code>
                and static mutable state. The verified diff does not support that. The closest file
                uses an existing
                <code className="font-mono text-sm text-foreground/80 bg-muted px-1 py-0.5 rounded"> AutoResetEvent.WaitOne()</code>
                pattern in tests, not a newly added sleep.
              </p>
              <p>
                Keeping this as a case study is still useful if it is framed honestly: it documents a
                rule-design gap and a class of behavior change that deserves a future detector.
              </p>
            </>
          ),
        },
      ]}
      diffTitle="Diff evidence"
      diffFile="src/NUnitFramework/framework/Attributes"
      diffLines={diffLines}
      findingTitle="What a better detector would ask"
      findingBody={findingBody}
      caveats={[
        "Current GCI0016 would not fire on this PR; there was no added Thread.Sleep, async blocking call, lock(this), or async void pattern.",
        "The inheritance change appears intentional and may be a bug fix, not a regression.",
        "This page is now a coverage-gap case study rather than a claim that GauntletCI would have blocked PR #5192.",
      ]}
      nextActions={[
        "Review inherited timeout/cancellation behavior with derived fixture tests before merging framework changes.",
        "Add release notes for behavior changes caused by attribute metadata, even when method bodies do not change.",
        "Consider a future rule for high-impact AttributeUsage changes on test framework attributes.",
      ]}
      sources={[
        { label: "PR #5192", href: "https://github.com/nunit/nunit/pull/5192" },
        { label: "Rule GCI0003", href: "https://gauntletci.com/docs/rules/GCI0003" },
      ]}
    />
  );
}

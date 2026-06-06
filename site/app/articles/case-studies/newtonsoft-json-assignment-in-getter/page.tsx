import type { Metadata } from "next";
import { CaseStudyLayout, type CaseStudyDiffLine } from "../_components/case-study-layout";

export const metadata: Metadata = {
  title: "Case Study: Nullable Migration in Newtonsoft.Json | GauntletCI",
  description:
    "A corrected deep dive into Newtonsoft.Json PR #1950, a 169-file nullable reference type migration that changed public annotations and fixed real null edge cases.",
  alternates: { canonical: "/articles/case-studies/newtonsoft-json-assignment-in-getter" },
  openGraph: { images: [{ url: "/og/case-studies.png", width: 1200, height: 630 }] },
};

const diffLines: CaseStudyDiffLine[] = [
  { type: "context", line: "// JToken.Parent became nullable" },
  { type: "removed", line: "private JContainer _parent;" },
  { type: "added", line: "private JContainer? _parent;" },
  { type: "removed", line: "public JContainer Parent" },
  { type: "added", line: "public JContainer? Parent" },
  { type: "context", line: "" },
  { type: "context", line: "// BeforeSelf() now handles parentless tokens" },
  { type: "added", line: "if (Parent == null)" },
  { type: "added", line: "{" },
  { type: "added", line: "    yield break;" },
  { type: "added", line: "}" },
  { type: "removed", line: "for (JToken o = Parent.First; o != this; o = o.Next)" },
  { type: "added", line: "for (JToken? o = Parent.First; o != this && o != null; o = o.Next)" },
  { type: "context", line: "" },
  { type: "context", line: "// JProperty.Value getter stayed pure; it only added null-forgiving annotation" },
  { type: "removed", line: "get { return _content._token; }" },
  { type: "added", line: "get { return _content._token!; }" },
];

const findingBody = [
  "[GCI0043] Nullability and Type Safety",
  "Signal   : nullable annotations, null-forgiving operators, and warning suppressions changed",
  "",
  "[GCI0055] Method Signature Change Risk",
  "Signal   : public API annotations changed, e.g. JContainer -> JContainer?",
  "",
  "[GCI0003/GCI0006] Behavioral and edge-case change",
  "Signal   : BeforeSelf() no longer throws for a parentless token; it returns an empty sequence",
].join("\n");

export default function NewtonsoftJsonAssignmentInGetterPage() {
  return (
    <CaseStudyLayout
      title="Nullable Migration in Newtonsoft.Json"
      description="PR #1950 was not an assignment-in-getter bug. It was a large nullable reference type migration that changed API annotations across Newtonsoft.Json and fixed real null-parent behavior in JToken.BeforeSelf()."
      canonicalPath="/articles/case-studies/newtonsoft-json-assignment-in-getter"
      repo="JamesNK/Newtonsoft.Json"
      pr="PR #1950"
      prUrl="https://github.com/JamesNK/Newtonsoft.Json/pull/1950"
      outcomeLabel="REVIEW"
      outcomeTone="yellow"
      tags={["Nullability", "API Contracts", "Edge Cases"]}
      ruleIds={["GCI0043", "GCI0055", "GCI0003", "GCI0006"]}
      stats={[
        { value: "169", label: "files changed" },
        { value: "7 mo", label: "PR lifetime" },
        { value: "2", label: "NRE fixes noted" },
      ]}
      sections={[
        {
          title: "What changed",
          children: (
            <>
              <p>
                The pull request enabled nullable reference type annotations across the library. It
                changed fields, return types, method parameters, indexers, and test expectations in one
                broad migration. The PR body also called out two null-reference bugs discovered during
                the work: <code className="font-mono text-sm text-foreground/80 bg-muted px-1 py-0.5 rounded">JToken.BeforeSelf()</code>
                and null XML node converter serialization.
              </p>
              <p>
                The most important reviewer signal is not a hidden getter mutation. It is the
                combination of public annotation changes and observable behavior changes in a library
                that millions of downstream projects compile against.
              </p>
            </>
          ),
        },
        {
          title: "Why this is risky",
          children: (
            <>
              <p>
                Nullable annotations are source-compatible at runtime, but they are still an API
                contract. A return type moving from non-nullable to nullable changes compiler warnings
                for consumers in nullable-aware projects. That can break warning-as-error builds even
                when binaries continue to load.
              </p>
              <p>
                The <code className="font-mono text-sm text-foreground/80 bg-muted px-1 py-0.5 rounded">BeforeSelf()</code>
                fix is a good behavioral change: parentless tokens now return an empty sequence instead
                of throwing. But it is still externally observable behavior and deserves a regression
                test, which the PR added.
              </p>
            </>
          ),
        },
        {
          title: "What the original thin page got wrong",
          children: (
            <>
              <p>
                The old case-study framing said this PR added an assignment inside a property getter.
                The verified diff does not support that. The relevant getter change was
                <code className="font-mono text-sm text-foreground/80 bg-muted px-1 py-0.5 rounded"> return _content._token!;</code>,
                which is a null-forgiving annotation, not a mutation.
              </p>
              <p>
                Correcting that premise makes the case study stronger: it shows that high-quality case
                studies should explain the actual risk, even when that means replacing a more dramatic
                but inaccurate claim.
              </p>
            </>
          ),
        },
      ]}
      diffTitle="Diff evidence"
      diffFile="Src/Newtonsoft.Json/Linq/JToken.cs and JProperty.cs"
      diffLines={diffLines}
      findingTitle="Accurate GauntletCI review signals"
      findingBody={findingBody}
      caveats={[
        "GCI0036 Pure Context Mutation would not fire on this PR; the getter body did not add an assignment.",
        "GCI0004 Breaking Change Risk would not fire either; the PR did not add or remove an Obsolete attribute.",
        "The nullable migration fixed real null edge cases, so the review question is contract impact and regression coverage, not whether the change is inherently bad.",
      ]}
      nextActions={[
        "Review public nullable annotation changes as API-surface changes, especially for consumers using warnings as errors.",
        "Keep the new BeforeSelf() parentless-token regression tests tied to the behavior change.",
        "Audit null-forgiving operators and warning suppressions added during the migration; each one is a promise to the compiler.",
      ]}
      sources={[
        { label: "PR #1950", href: "https://github.com/JamesNK/Newtonsoft.Json/pull/1950" },
        { label: "Rule GCI0043", href: "https://gauntletci.com/docs/rules/GCI0043" },
        { label: "Rule GCI0055", href: "https://gauntletci.com/docs/rules/GCI0055" },
      ]}
    />
  );
}

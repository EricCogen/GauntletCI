import type { Metadata } from "next";
import { CaseStudyLayout, type CaseStudyDiffLine } from "../_components/case-study-layout";

export const metadata: Metadata = {
  title: "Case Study: Cosmos Serialization Modernization in EF Core | GauntletCI",
  description:
    "A corrected deep dive into EF Core PR #38024, where Cosmos serialization modernization created public deprecation, internal signature churn, and unmapped JSON preservation risk.",
  alternates: { canonical: "/articles/case-studies/efcore-breaking-api-removal" },
  openGraph: { images: [{ url: "/og/case-studies.png", width: 1200, height: 630 }] },
};

const diffLines: CaseStudyDiffLine[] = [
  { type: "context", line: "// Public Cosmos option marked obsolete" },
  { type: "added", line: '[Obsolete("Enabling ContentResponseOnWrite currently has no benefit for EF Core.")]' },
  { type: "context", line: "public virtual CosmosDbContextOptionsBuilder ContentResponseOnWriteEnabled(bool enabled = true)" },
  { type: "context", line: "" },
  { type: "context", line: "// Internal wrapper signatures moved from DOM objects to serialized bytes" },
  { type: "removed", line: "public virtual Task<bool> CreateItemAsync(string containerId, JToken document, ...)" },
  { type: "added", line: "public virtual Task<bool> CreateItemAsync(string containerId, string documentId, ReadOnlyMemory<byte> document, ...)" },
  { type: "removed", line: "public virtual Task<bool> ReplaceItemAsync(string collectionId, string documentId, JObject document, ...)" },
  { type: "added", line: "public virtual Task<bool> ReplaceItemAsync(string collectionId, string documentId, ReadOnlyMemory<byte> document, ...)" },
  { type: "context", line: "" },
  { type: "context", line: "// Write-response hydration of __jObject was removed" },
  { type: "removed", line: "var createdDocument = Serializer.Deserialize<JObject>(jsonReader);" },
  { type: "removed", line: "entry.SetStoreGeneratedValue(jObjectProperty, createdDocument);" },
  { type: "added", line: "entry.SetStoreGeneratedValue(etagProperty, eTag);" },
];

const findingBody = [
  "[GCI0004] Breaking Change Risk",
  "Signal   : [Obsolete] added to a public Cosmos options-builder API",
  "",
  "[GCI0003] Behavioral Change Detection",
  "Signal   : method signatures changed across Cosmos wrapper/update pipeline types",
  "",
  "[GCI0015] Data Integrity reviewer question",
  "Signal   : fresh serialization can drop unmapped JSON fields that were previously preserved",
].join("\n");

export default function EFCoreBreakingApiRemovalPage() {
  return (
    <CaseStudyLayout
      title="Cosmos Serialization Modernization in EF Core"
      description="PR #38024 was not a simple public API removal. It modernized the EF Core Cosmos write pipeline from Newtonsoft DOM objects toward System.Text.Json streaming, while also adding a public deprecation and changing internal update behavior."
      canonicalPath="/articles/case-studies/efcore-breaking-api-removal"
      repo="dotnet/efcore"
      pr="PR #38024"
      prUrl="https://github.com/dotnet/efcore/pull/38024"
      outcomeLabel="BLOCK/REVIEW"
      outcomeTone="red"
      tags={["Cosmos DB", "Serialization", "API Contracts"]}
      ruleIds={["GCI0004", "GCI0003", "GCI0015"]}
      stats={[
        { value: "33", label: "files changed" },
        { value: "1,815", label: "lines touched" },
        { value: "1", label: "public obsolete API" },
      ]}
      sections={[
        {
          title: "What changed",
          children: (
            <>
              <p>
                The PR replaced parts of the Cosmos update pipeline that used
                <code className="font-mono text-sm text-foreground/80 bg-muted px-1 py-0.5 rounded"> JObject</code>
                and <code className="font-mono text-sm text-foreground/80 bg-muted px-1 py-0.5 rounded">JToken</code>
                with serialized byte payloads written through
                <code className="font-mono text-sm text-foreground/80 bg-muted px-1 py-0.5 rounded"> Utf8JsonWriter</code>.
                It also marked
                <code className="font-mono text-sm text-foreground/80 bg-muted px-1 py-0.5 rounded"> ContentResponseOnWriteEnabled()</code>
                obsolete because EF Core no longer benefits from the response body content.
              </p>
              <p>
                The largest compatibility question is not the public method being deleted. It is the
                shift from mutating a previously hydrated JSON document to serializing a fresh document
                from EF-tracked state.
              </p>
            </>
          ),
        },
        {
          title: "Why this is risky",
          children: (
            <>
              <p>
                Cosmos documents can contain fields that are not modeled by EF. Before this
                modernization, the pipeline could preserve unknown fields through the internal
                <code className="font-mono text-sm text-foreground/80 bg-muted px-1 py-0.5 rounded"> __jObject</code>
                path. After the change, updates serialize the modeled entity state again. That is
                cleaner and faster, but it raises a silent data-loss question for applications that
                store extra JSON alongside EF-managed properties.
              </p>
              <p>
                The new obsolete attribute is also worth a reviewer stop. It tells users to stop using
                the API, but the message does not name a replacement because the intended migration is
                simply to remove the call.
              </p>
            </>
          ),
        },
        {
          title: "What the original thin page got wrong",
          children: (
            <>
              <p>
                The earlier framing said this was a public API removal that would break every provider
                author. The verified diff is more nuanced: the direct public API change is an
                <code className="font-mono text-sm text-foreground/80 bg-muted px-1 py-0.5 rounded"> [Obsolete]</code>
                annotation, while most signature churn is on EF-internal types that carry explicit
                internal-API disclaimers.
              </p>
              <p>
                The stronger case study is the behavioral one: a serialization pipeline rewrite can be
                correct and still deserve merge-gate attention because persisted JSON shape and
                unmapped-field preservation are production contracts.
              </p>
            </>
          ),
        },
      ]}
      diffTitle="Diff evidence"
      diffFile="src/EFCore.Cosmos/Storage/Internal and CosmosDbContextOptionsBuilder.cs"
      diffLines={diffLines}
      findingTitle="GauntletCI review signals"
      findingBody={findingBody}
      caveats={[
        "The PR did not remove the public ContentResponseOnWriteEnabled method; it added Obsolete.",
        "Most signature changes are on EF-internal types, so their compatibility risk is lower than ordinary public API churn.",
        "The unmapped-field data-loss concern is a reviewer question, not proof that the PR was wrong; the PR intentionally modernized the pipeline.",
      ]}
      nextActions={[
        "Verify the Obsolete message gives users a clear migration decision: remove the call rather than switch APIs.",
        "Review tests for Cosmos documents with unmapped JSON fields to confirm the new update path has the intended behavior.",
        "Separate internal API churn from externally supported API changes in release notes and compatibility docs.",
      ]}
      sources={[
        { label: "PR #38024", href: "https://github.com/dotnet/efcore/pull/38024" },
        { label: "Rule GCI0004", href: "https://gauntletci.com/docs/rules/GCI0004" },
        { label: "Rule GCI0015", href: "https://gauntletci.com/docs/rules/GCI0015" },
      ]}
    />
  );
}

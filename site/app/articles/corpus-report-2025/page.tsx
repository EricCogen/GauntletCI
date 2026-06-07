import type { Metadata } from "next";
import Link from "next/link";
import { ArticleLayout } from "../_components/article-layout";
import { SourcesSection } from "../_components/sources-section";

const slug = "corpus-report-2025";
const title = "State of Behavioral Change Risk in .NET";
const description =
  "A field report from 610 merged C# pull requests across 61 repositories, with raw findings, high-confidence findings, outliers, and rule-level evidence.";
const readingTime = "11 min read";
const ruleIds = [
  "GCI0003",
  "GCI0004",
  "GCI0006",
  "GCI0015",
  "GCI0016",
  "GCI0024",
  "GCI0010",
  "GCI0036",
];

const sources = [
  {
    label: "GauntletCI corpus fixture export",
    href: "https://github.com/EricCogen/GauntletCI/blob/main/data/corpus-fixtures.csv",
    description:
      "Public fixture-level corpus export used to cross-check PR, repository, test-change, and finding totals.",
  },
  {
    label: "Corpus audit runner",
    href: "https://github.com/EricCogen/GauntletCI/blob/main/RunCorpusAudit.csx",
    description:
      "Audit script that hydrates corpus fixtures and runs GauntletCI analysis against the local corpus database.",
  },
  {
    label: "Azure SDK for .NET PR #57223",
    href: "https://github.com/Azure/azure-sdk-for-net/pull/57223",
    description:
      "The largest outlier in the current corpus; it accounts for 40,155 raw findings and 16,611 high-confidence findings.",
  },
  {
    label: "Azure SDK PR #57223 deep dive",
    href: "/articles/azure-sdk-pr-57223-risk-analysis",
    description:
      "Internal case study explaining why multiframework API-surface changes can produce unusually large raw finding counts.",
  },
  {
    label: "Behavioral Change Risk framework",
    href: "/articles/behavioral-change-risk-formal-framework",
    description:
      "Internal methodology article defining the change-risk categories used throughout the corpus analysis.",
  },
  {
    label: "Anthropic coordinated vulnerability disclosure dashboard",
    href: "https://red.anthropic.com/2026/cvd/",
    description:
      "Public example of a candidate finding, triage, disclosure, patch, advisory, and ledger workflow for AI-assisted vulnerability research.",
  },
];

const rawStats = [
  { value: "610", label: "merged C# PRs" },
  { value: "61", label: "public repositories" },
  { value: "147,958", label: "raw BCR findings" },
  { value: "35,871", label: "high-confidence findings" },
];

const topRules = [
  ["GCI0004", "Public API exposure and visibility changes", "59,965"],
  ["GCI0003", "Method signature and contract changes", "39,628"],
  ["GCI0006", "Null and edge-case handling changes", "10,978"],
  ["GCI0015", "Exception-path changes", "10,389"],
  ["GCI0016", "Async and deadlock candidates", "4,040"],
  ["GCI0024", "Dangerous API usage", "3,435"],
  ["GCI0010", "Thread-safety and concurrency risks", "3,225"],
  ["GCI0001", "Mixed-scope or diff-integrity risk", "2,674"],
  ["GCI0036", "Performance hot-path risks", "2,524"],
  ["GCI0047", "Additional behavioral-change signals", "1,450"],
];

const topRepositories = [
  ["Azure/azure-sdk-for-net", "18", "42,919", "16,875"],
  ["JamesNK/Newtonsoft.Json", "10", "12,086", "1,034"],
  ["googleapis/google-api-dotnet-client", "17", "12,009", "3,236"],
  ["DapperLib/Dapper", "7", "9,696", "107"],
  ["StackExchange/StackExchange.Redis", "10", "5,568", "825"],
  ["dotnet/reactive", "12", "5,546", "217"],
  ["apache/logging-log4net", "9", "4,716", "1,359"],
  ["dotnet/orleans", "14", "4,188", "681"],
  ["grpc/grpc-dotnet", "12", "3,935", "243"],
  ["DevToys-app/DevToys", "12", "3,787", "1,011"],
];

export const metadata: Metadata = {
  title: `${title}: 147,958 Risk Signals Across 610 C# PRs | GauntletCI`,
  description,
  alternates: { canonical: `/articles/${slug}` },
  keywords: [
    "behavioral change risk",
    ".NET code review",
    "C# pull request analysis",
    "pull request risk analysis",
    "static analysis corpus",
    "GauntletCI corpus report",
    "code review data",
  ],
  authors: [{ name: "Eric Cogen", url: "https://github.com/EricCogen" }],
  creator: "Eric Cogen",
  publisher: "GauntletCI",
  openGraph: {
    title: `${title}: 147,958 Risk Signals Across 610 C# PRs`,
    description,
    url: `https://gauntletci.com/articles/${slug}`,
    type: "article",
    images: [{ url: `/og/${slug}.png`, width: 1200, height: 630, alt: title }],
  },
  twitter: {
    card: "summary_large_image",
    title,
    description,
    images: [`/og/${slug}.png`],
  },
};

const jsonLd = {
  "@context": "https://schema.org",
  "@type": "Article",
  headline: title,
  description,
  image: `/og/${slug}.png`,
  datePublished: "2026-05-20T00:00:00Z",
  author: { "@type": "Person", name: "Eric Cogen", url: "https://github.com/EricCogen" },
  publisher: {
    "@type": "Organization",
    name: "GauntletCI",
    url: "https://gauntletci.com",
    logo: { "@type": "ImageObject", url: "https://gauntletci.com/icon.svg" },
  },
  mainEntityOfPage: {
    "@type": "WebPage",
    "@id": `https://gauntletci.com/articles/${slug}`,
  },
  keywords: metadata.keywords,
};

export default function CorpusReportPage() {
  return (
    <>
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLd) }} />
      <ArticleLayout
        category="Corpus Evidence"
        title={title}
        intro="Most code-review content argues from taste. This report starts from a local corpus: 610 already-merged C# pull requests, 61 repositories, and the Behavioral Change Risk signals GauntletCI found in the diffs."
        dateTime="2026-05-20"
        dateLabel="May 20, 2026"
        readingTime={readingTime}
        ruleIds={ruleIds}
        related={
          <section className="space-y-4 rounded-xl border border-border bg-card/50 p-6">
            <h3 className="font-semibold text-foreground">Use this report with</h3>
            <div className="grid gap-3 sm:grid-cols-2">
              <Link href="/articles/azure-sdk-pr-57223-risk-analysis" className="text-sm font-medium text-cyan-400 hover:text-cyan-300">
                Azure SDK PR #57223 deep dive
              </Link>
              <Link href="/articles/what-is-pull-request-risk-analysis" className="text-sm font-medium text-cyan-400 hover:text-cyan-300">
                What is pull request risk analysis?
              </Link>
              <Link href="/articles/behavioral-change-risk-formal-framework" className="text-sm font-medium text-cyan-400 hover:text-cyan-300">
                Behavioral Change Risk framework
              </Link>
              <Link href="/articles/case-studies" className="text-sm font-medium text-cyan-400 hover:text-cyan-300">
                PR case studies
              </Link>
            </div>
          </section>
        }
      >
        <section className="space-y-6">
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            {rawStats.map((stat) => (
              <div key={stat.label} className="rounded-xl border border-cyan-500/20 bg-cyan-500/5 p-5">
                <div className="text-3xl font-bold text-cyan-400">{stat.value}</div>
                <div className="mt-1 text-sm text-muted-foreground">{stat.label}</div>
              </div>
            ))}
          </div>
          <div className="rounded-xl border border-yellow-500/20 bg-yellow-500/5 p-5">
            <p className="text-sm text-muted-foreground leading-relaxed">
              <strong className="text-foreground">Counting matters:</strong> this page separates raw findings from high-confidence findings. Raw findings preserve every affected file, framework surface, and rule hit. High-confidence findings use the corpus database confidence threshold of 0.70 or higher. The former measures blast radius; the latter is the cleaner signal for prioritization.
            </p>
          </div>
        </section>

        <section className="space-y-4">
          <h2 className="text-3xl font-bold">The uncomfortable pattern: contract risk dominates</h2>
          <p className="text-lg text-muted-foreground leading-relaxed">
            The largest finding groups are not exotic security bugs or dramatic runtime crashes. They are ordinary-looking API and contract changes: visibility changes, signature changes, nullable edge cases, exception paths, and async behavior. Those are exactly the changes that can look reasonable in review while still changing what downstream callers experience.
          </p>
          <p className="text-lg text-muted-foreground leading-relaxed">
            That is the core reason GauntletCI treats pull request risk as a diff problem instead of a whole-codebase cleanliness score. The question is not "is this repository good?" The question is "what did this PR make newly dangerous?"
          </p>
        </section>

        <section className="space-y-5">
          <h2 className="text-3xl font-bold">Top rule families in the corpus</h2>
          <div className="overflow-x-auto rounded-xl border border-border">
            <table className="w-full text-sm">
              <thead className="bg-card">
                <tr>
                  <th className="p-3 text-left font-semibold text-foreground">Rule</th>
                  <th className="p-3 text-left font-semibold text-foreground">Signal family</th>
                  <th className="p-3 text-right font-semibold text-foreground">Raw findings</th>
                </tr>
              </thead>
              <tbody>
                {topRules.map(([rule, label, count], index) => (
                  <tr key={rule} className={index % 2 ? "bg-card/40" : undefined}>
                    <td className="p-3 font-mono text-cyan-400">{rule}</td>
                    <td className="p-3 text-muted-foreground">{label}</td>
                    <td className="p-3 text-right font-mono text-muted-foreground">{count}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <p className="text-sm text-muted-foreground leading-relaxed">
            GCI0004 and GCI0003 together account for 99,593 raw findings. That does not mean every finding is a defect. It means API shape and contract changes are the dominant risk surface in this corpus.
          </p>
        </section>

        <section className="space-y-5">
          <h2 className="text-3xl font-bold">Repository distribution, with the outlier left visible</h2>
          <p className="text-lg text-muted-foreground leading-relaxed">
            The corpus is intentionally not flattened to hide uncomfortable skew. Large SDK and framework PRs produce more signals because they touch more published surface area. That skew is a feature of the data, not a reason to erase it.
          </p>
          <div className="overflow-x-auto rounded-xl border border-border">
            <table className="w-full text-sm">
              <thead className="bg-card">
                <tr>
                  <th className="p-3 text-left font-semibold text-foreground">Repository</th>
                  <th className="p-3 text-right font-semibold text-foreground">Corpus PRs</th>
                  <th className="p-3 text-right font-semibold text-foreground">Raw findings</th>
                  <th className="p-3 text-right font-semibold text-foreground">High-confidence</th>
                </tr>
              </thead>
              <tbody>
                {topRepositories.map(([repo, prs, raw, high], index) => (
                  <tr key={repo} className={index % 2 ? "bg-card/40" : undefined}>
                    <td className="p-3 font-medium text-foreground">{repo}</td>
                    <td className="p-3 text-right font-mono text-muted-foreground">{prs}</td>
                    <td className="p-3 text-right font-mono text-muted-foreground">{raw}</td>
                    <td className="p-3 text-right font-mono text-muted-foreground">{high}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>

        <section className="space-y-4 rounded-xl border border-cyan-500/20 bg-cyan-500/5 p-6">
          <h2 className="text-2xl font-bold">Outlier disclosure: Azure SDK PR #57223</h2>
          <p className="text-muted-foreground leading-relaxed">
            Azure SDK for .NET PR #57223 contributes 40,155 raw findings and 16,611 high-confidence findings. That is 27.1% of the corpus raw total and 46.3% of the high-confidence total. Any honest reading of the corpus has to say that out loud.
          </p>
          <p className="text-muted-foreground leading-relaxed">
            The right conclusion is not "Azure SDK is bad." The useful conclusion is that multiframework, published-surface-area changes create a different risk profile than small application PRs. For libraries, one signature or visibility change can echo through multiple target frameworks and generated surfaces.
          </p>
          <Link href="/articles/azure-sdk-pr-57223-risk-analysis" className="inline-flex font-semibold text-cyan-400 hover:text-cyan-300">
            Read the Azure SDK PR #57223 analysis →
          </Link>
        </section>

        <section className="space-y-4">
          <h2 className="text-3xl font-bold">Test changes are not a reliable proxy for risk</h2>
          <p className="text-lg text-muted-foreground leading-relaxed">
            The corpus contains 178 PRs with no test-file changes recorded. Of those, 131 had at least one Behavioral Change Risk finding, and 46 had at least one high-confidence finding. That does not prove the PRs were wrong. It proves that "tests changed" and "risk was introduced" are different signals.
          </p>
          <p className="text-lg text-muted-foreground leading-relaxed">
            A reviewer needs both. A test diff shows what behavior the author chose to prove. A risk diff shows what behavior the author may have changed without making that choice explicit.
          </p>
        </section>

        <section className="space-y-4">
          <h2 className="text-3xl font-bold">What this changes for PR review</h2>
          <div className="grid gap-4 md:grid-cols-3">
            <div className="rounded-lg border border-border bg-card p-5">
              <h3 className="font-semibold text-foreground">Review the delta, not the vibe</h3>
              <p className="mt-2 text-sm text-muted-foreground leading-relaxed">
                A polished PR can still alter contracts, exception paths, and runtime assumptions. Risk analysis gives reviewers a concrete checklist tied to the diff.
              </p>
            </div>
            <div className="rounded-lg border border-border bg-card p-5">
              <h3 className="font-semibold text-foreground">Treat API shape as production behavior</h3>
              <p className="mt-2 text-sm text-muted-foreground leading-relaxed">
                Visibility and signature changes dominated the corpus. For library and platform teams, API shape is not metadata; it is behavior customers compile against.
              </p>
            </div>
            <div className="rounded-lg border border-border bg-card p-5">
              <h3 className="font-semibold text-foreground">Use findings as evidence, not theater</h3>
              <p className="mt-2 text-sm text-muted-foreground leading-relaxed">
                A finding is not a verdict. It is a deterministic pointer to changed behavior that deserves a human decision before merge.
              </p>
            </div>
          </div>
        </section>

        <section className="space-y-4 rounded-xl border border-border bg-card/50 p-6">
          <h2 className="text-2xl font-bold">Toward a finding ledger</h2>
          <p className="text-muted-foreground leading-relaxed">
            The next credibility step is not a louder claim about what GauntletCI can find. It is a clearer public surface for how findings move from candidate signal to reviewer decision. Anthropic's Mythos Preview dashboard is useful here as a workflow reference: candidates are triaged, validated, disclosed, patched, and tied to ledger entries.
          </p>
          <p className="text-muted-foreground leading-relaxed">
            A GauntletCI finding ledger would be narrower and product-specific: PR, rule ID, changed file, evidence snippet, confidence band, reviewer verdict, disposition, and follow-up outcome. That would make the corpus easier to audit and make it obvious that a finding is evidence for human review, not an automatic defect accusation.
          </p>
        </section>

        <section className="space-y-4">
          <h2 className="text-3xl font-bold">Methodology and limitations</h2>
          <p className="text-lg text-muted-foreground leading-relaxed">
            The current local corpus database contains 610 public, already-merged C# pull requests across 61 repositories. The analyzed findings table contains 147,958 triggered findings across 529 PRs and 28 rule IDs. A high-confidence finding is a triggered finding with an `actual_confidence` value of at least 0.70.
          </p>
          <p className="text-lg text-muted-foreground leading-relaxed">
            The report is not a benchmark of repository quality, maintainer skill, or defect rate. It is a field report about where deterministic change-risk rules fire when applied to real merged diffs. Some findings represent intentional changes. Some represent generated or multiframework duplication. That is why this page reports raw counts, high-confidence counts, and the largest outlier separately.
          </p>
        </section>

        <SourcesSection sources={sources} />
      </ArticleLayout>
    </>
  );
}

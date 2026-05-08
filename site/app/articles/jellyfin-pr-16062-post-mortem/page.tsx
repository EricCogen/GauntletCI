import type { Metadata } from "next";
import Link from "next/link";
import { Header } from "@/components/header";
import { Footer } from "@/components/footer";
import { Breadcrumbs } from "@/components/breadcrumbs";
import { RulesApplied } from "@/components/rules-applied";
import JsonLd from "@/components/json-ld";

export const metadata: Metadata = {
  title: "Jellyfin PR #16062 Post-Mortem: 129 Findings in 660ms | GauntletCI",
  description:
    "GauntletCI analyzed Jellyfin PR #16062 after merge: 129 findings across 13 rules in 660ms, including 11 block-level issues that escaped code review. A detailed post-mortem analysis.",
  alternates: { canonical: "/articles/jellyfin-pr-16062-post-mortem" },
  keywords: ["code review", "static analysis", ".NET", "behavioral change risk", "diff analysis", "GauntletCI"],
  authors: [{ name: "Eric Cogen", url: "https://github.com/EricCogen" }],
  creator: "Eric Cogen",
  publisher: "GauntletCI",
  openGraph: {
    title: "Jellyfin PR #16062 Post-Mortem: 129 Findings in 660ms",
    description:
      "GauntletCI found 11 block-level issues in a merged Jellyfin PR that escaped human review and automated tests. A detailed analysis of what went wrong.",
    url: "https://gauntletci.com/articles/jellyfin-pr-16062-post-mortem",
    type: "article",
    images: [
      {
        url: "/og/jellyfin-pr-16062.png",
        width: 1200,
        height: 630,
        alt: "Jellyfin PR #16062 Post-Mortem",
      },
    ],
  },
  twitter: {
    card: "summary_large_image",
    title: "Jellyfin PR #16062 Post-Mortem: 129 Findings in 660ms",
    description: "GauntletCI found 11 block-level issues that escaped code review and tests",
    images: ["/og/jellyfin-pr-16062.png"],
  },
};

const jsonLd = {
  "@context": "https://schema.org",
  "@type": "Article",
  headline: "Jellyfin PR #16062 Post-Mortem: 129 Findings in 660ms",
  description:
    "GauntletCI analyzed Jellyfin PR #16062 after merge: 129 findings across 13 rules in 660ms, including 11 block-level issues that escaped code review.",
  image: "/og/jellyfin-pr-16062.png",
  datePublished: "2026-05-07T00:00:00Z",
  author: {
    "@type": "Person",
    name: "Eric Cogen",
    url: "https://github.com/EricCogen",
  },
  publisher: {
    "@type": "Organization",
    name: "GauntletCI",
    url: "https://gauntletci.com",
    logo: {
      "@type": "ImageObject",
      url: "https://gauntletci.com/icon.svg",
    },
  },
  mainEntityOfPage: {
    "@type": "WebPage",
    "@id": "https://gauntletci.com/articles/jellyfin-pr-16062-post-mortem",
  },
  keywords: [
    "code review",
    "static analysis",
    ".NET",
    "behavioral change risk",
    "diff analysis",
    "GauntletCI",
    "Jellyfin",
    "pull request",
  ],
};

const findings = [
  {
    rule: "GCI0016",
    title: "Concurrency and State Risk",
    count: 5,
    severity: "Block",
    description: "Five deadlock candidates: blocking calls on async operations (.Wait() and .GetAwaiter().GetResult())",
    impact: "In ASP.NET Core, blocking on async can cause deadlock via synchronization context starvation. The request hangs with no exception or log entry.",
  },
  {
    rule: "GCI0012",
    title: "Security Risk",
    count: 3,
    severity: "Block",
    description: "Three dangerous API usages: Reflection and Activator.CreateInstance bypassing the DI container",
    impact: "Reflection instantiation bypasses dependency injection, access controls, validation, and lifecycle management. In a media server handling authentication and content access, this is a real security concern.",
  },
  {
    rule: "GCI0044",
    title: "Performance Hotpath Risk",
    count: 28,
    severity: "Warn",
    description: "Twenty-eight N+1 query patterns: LINQ queries executing inside loops",
    impact: "For a media library with tens of thousands of items, the difference between milliseconds and minutes. The PR was titled 'Query Performance Improvements.'",
  },
  {
    rule: "GCI0038",
    title: "Dependency Injection Safety",
    count: 45,
    severity: "Warn",
    description: "Forty-five service locator anti-patterns: reaching into the DI container instead of declaring dependencies",
    impact: "Service locator code is harder to test, harder to reason about, and creates hidden coupling. The most common architectural regression in growing .NET codebases.",
  },
  {
    rule: "GCI0043",
    title: "Nullability and Type Safety",
    count: 15,
    severity: "Warn",
    description: "Fifteen as-cast operations without null checks",
    impact: "obj as SomeType returns null on failure, not an exception. Using the result without checking causes NullReferenceException at runtime with no useful context.",
  },
  {
    rule: "GCI0006",
    title: "Edge Case Handling",
    count: 13,
    severity: "Warn",
    description: "Thirteen .Value accesses on nullable types without preceding null checks",
    impact: "Explicit dereference of nullable values without verification. Runtime crashes in edge cases that tests did not cover.",
  },
];

export default function JellyfinArticle() {
  return (
    <>
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLd) }} />
      <Header />
      <main className="min-h-screen bg-background pt-24">
        <div className="mx-auto max-w-4xl px-4 sm:px-6 lg:px-8 py-16 sm:py-20 space-y-16">

          {/* Breadcrumbs */}
          <Breadcrumbs />

          {/* Hero */}
          <div className="space-y-5 border-b border-border pb-12">
            <div className="flex items-center justify-between">
              <p className="text-sm font-semibold uppercase tracking-widest text-cyan-400">Case Study</p>
              <Link href="/articles" className="text-sm text-muted-foreground hover:text-cyan-400 transition-colors">← All articles</Link>
            </div>
            <h1 className="text-4xl sm:text-5xl font-bold tracking-tight text-balance">
              We Ran a Static Analysis Tool on a Merged Open Source PR. Here Is What It Found.
            </h1>
            <p className="text-xl text-muted-foreground leading-relaxed text-pretty">
              Jellyfin PR #16062: 129 findings across 13 rules in 660ms. 11 were block-level issues that escaped code review.
            </p>
            <div className="flex items-center gap-2 pt-1">
              <span className="text-sm font-medium text-foreground">Eric Cogen</span>
              <span className="text-muted-foreground/40 text-sm">·</span>
              <span className="text-sm text-muted-foreground">Founder, GauntletCI</span>
              <span className="text-muted-foreground/40 text-sm">·</span>
              <time className="text-sm text-muted-foreground" dateTime="2026-05-07">May 7, 2026</time>
            </div>
          </div>

          {/* Lead */}
          <section className="space-y-4">
            <p className="text-lg text-muted-foreground leading-relaxed">
              Jellyfin PR #16062 is titled "Query Performance Improvements." It touched 126 files, added 27,810 lines, and removed 3,932. It was reviewed, approved, and merged on May 3, 2026.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              We ran GauntletCI against the diff after it merged. In 660 milliseconds, it produced <strong className="text-foreground">129 findings across 13 rules</strong>. Eleven of those findings were <strong className="text-foreground">block-level</strong>: the kind that should stop a merge.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              Nobody caught them in code review. The tests passed. The PR shipped.
            </p>
          </section>

          {/* What is GauntletCI */}
          <section className="space-y-5">
            <h2 className="text-2xl font-bold tracking-tight">What GauntletCI Is</h2>
            <div className="space-y-4 text-muted-foreground leading-relaxed">
              <p>
                GauntletCI is a diff-first Behavioral Change Risk detector for .NET. It does not run tests. It does not compile code. It does not use a language model to evaluate your changes. It runs a set of deterministic rules against the diff and produces findings that are reproducible every time.
              </p>
              <p>
                <strong className="text-foreground">The same diff produces the same findings. Always.</strong>
              </p>
              <p>
                It answers one question: <em>did this change introduce behavior that is not properly validated?</em>
              </p>
            </div>
          </section>

          {/* The PR */}
          <section className="space-y-5">
            <h2 className="text-2xl font-bold tracking-tight">The PR</h2>
            <div className="space-y-4 text-muted-foreground leading-relaxed">
              <p>
                Jellyfin is a free, open source media server written in .NET. PR #16062 was a significant refactor: query logic that previously ran in memory was moved to the database layer. The goal was performance. The change was substantial.
              </p>
              <p>The kind of PR where things go wrong in ways that look fine on the surface.</p>
            </div>
          </section>

          {/* What GauntletCI Found */}
          <section className="space-y-5">
            <h2 className="text-2xl font-bold tracking-tight">What GauntletCI Found</h2>
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              {findings.map((finding) => (
                <div key={finding.rule} className="rounded-lg border border-border bg-card p-5 space-y-3">
                  <div className="flex items-center justify-between">
                    <span className="text-sm font-mono font-semibold text-cyan-400">{finding.rule}</span>
                    <span className={`text-xs font-semibold px-2 py-1 rounded ${
                      finding.severity === "Block" 
                        ? "bg-red-500/20 text-red-300" 
                        : "bg-orange-500/20 text-orange-300"
                    }`}>
                      {finding.severity}
                    </span>
                  </div>
                  <h3 className="font-semibold text-foreground">{finding.title}</h3>
                  <div className="text-xs font-semibold bg-cyan-500/10 text-cyan-300 px-2 py-1 rounded w-fit">
                    {finding.count} findings
                  </div>
                  <p className="text-sm text-muted-foreground">{finding.description}</p>
                  <div className="bg-muted p-3 rounded border-l-3 border-cyan-500/50 text-sm text-muted-foreground">
                    <strong className="text-foreground">Impact:</strong> {finding.impact}
                  </div>
                </div>
              ))}
            </div>
          </section>

          {/* What the Numbers Mean */}
          <section className="space-y-5">
            <h2 className="text-2xl font-bold tracking-tight">What the Numbers Mean</h2>
            <div className="space-y-4 text-muted-foreground leading-relaxed">
              <p>
                <strong className="text-foreground">129 findings. 11 block-level. 660 milliseconds.</strong>
              </p>
              <p>The PR had human reviewers. It had automated tests. It passed both. It merged.</p>
              <p>
                GauntletCI does not replace reviewers or tests. Reviewers check <em>intent</em>. Tests check <em>known behavior</em>. GauntletCI checks something different: whether the <em>behavioral impact</em> of the change is verified.
              </p>
              <p>
                A reviewer looking at a LINQ query inside a loop in a 27,000-line diff might not recognize it as a performance regression. A test suite that was written against the old in-memory behavior will not catch a behavioral shift when the logic moves to the database layer. GauntletCI looks at the diff and asks whether the change introduced patterns that are structurally risky, regardless of whether the tests pass.
              </p>
              <p>
                <strong className="text-foreground">That is the gap it fills.</strong> Not better tests. Not smarter reviewers. A different question asked at a different time.
              </p>
            </div>
          </section>

          {/* Try It Yourself */}
          <section className="space-y-5">
            <h2 className="text-2xl font-bold tracking-tight">Try It Yourself</h2>
            <div className="space-y-4 text-muted-foreground leading-relaxed">
              <p>
                The GauntletCI-Demo repo contains six always-open scenario PRs against a realistic ASP.NET Core OrderService. Each one buries a single risky change in a plausible multi-file diff. GauntletCI runs on every PR: you can read the workflow output without installing anything.
              </p>
              <p>If you want to run it locally:</p>
              <pre className="bg-muted border border-border rounded p-4 overflow-x-auto">
                <code className="font-mono text-sm text-muted-foreground">{`dotnet tool install -g GauntletCI
gauntletci analyze --staged`}</code>
              </pre>
              <p>No configuration required. No code leaves your machine. No LLM in the detection path.</p>
              <p>If you want to catch the kind of issues described in this post before they merge rather than after, that is the point.</p>
              <ul className="space-y-2">
                <li>
                  <Link href="https://github.com/EricCogen/GauntletCI" className="text-cyan-400 hover:text-cyan-300 font-semibold">
                    GauntletCI on GitHub
                  </Link>
                </li>
                <li>
                  <Link href="https://github.com/EricCogen/GauntletCI-Demo/pulls" className="text-cyan-400 hover:text-cyan-300 font-semibold">
                    Live demo PRs
                  </Link>
                </li>
                <li>
                  <Link href="https://github.com/jellyfin/jellyfin/pull/16062" className="text-cyan-400 hover:text-cyan-300 font-semibold">
                    Original Jellyfin PR #16062
                  </Link>
                </li>
              </ul>
            </div>
          </section>

          {/* One More Thing */}
          <section className="space-y-5">
            <h2 className="text-2xl font-bold tracking-tight">One More Thing</h2>
            <div className="space-y-4 text-muted-foreground leading-relaxed">
              <p>
                If you work on or contribute to Jellyfin: this analysis was performed against the public diff of PR #16062 as an independent validation exercise. The findings are documented and reproducible. The diff is public. Anyone can verify them.
              </p>
              <p>
                The intent is not to criticize the Jellyfin team. A PR of this size and complexity, touching core data access paths across 126 files, is exactly the kind of change where this class of issue is hardest to catch in review. That is the point.
              </p>
            </div>
          </section>

          {/* Rules Applied */}
          <div className="border-t border-border pt-12">
            <RulesApplied ids={findings.map((f) => f.rule)} />
          </div>

        </div>
      </main>

      <Footer />
    </>
  );
}

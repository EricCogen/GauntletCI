import type { Metadata } from "next";
import Link from "next/link";
import { Header } from "@/components/header";
import { Footer } from "@/components/footer";
import { Breadcrumbs } from "@/components/breadcrumbs";
import { RulesApplied } from "@/components/rules-applied";
import { AuthorBio } from "@/components/author-bio";
import JsonLd from "@/components/json-ld";

export const metadata: Metadata = {
  title: "A \"Performance Improvement\" PR Introduced 11 Block-Level Risks - GauntletCI Found Them in 660ms | GauntletCI",
  description:
    "Jellyfin PR #16062 promised performance improvements but introduced 129 findings, including 11 block-level risks that escaped code review and tests. GauntletCI detected them in 660ms.",
  alternates: { canonical: "/articles/jellyfin-pr-16062-post-mortem" },
  keywords: ["code review", "static analysis", ".NET", "behavioral change risk", "diff analysis", "GauntletCI", "performance regression"],
  authors: [{ name: "Eric Cogen", url: "https://github.com/EricCogen" }],
  creator: "Eric Cogen",
  publisher: "GauntletCI",
  openGraph: {
    title: "A \"Performance Improvement\" PR Introduced 11 Block-Level Risks - GauntletCI Found Them in 660ms",
    description:
      "Jellyfin PR #16062 escaped code review despite introducing 11 block-level risks. Discover why traditional tools miss behavioral regressions and how GauntletCI caught them.",
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
    title: "A \"Performance Improvement\" PR Introduced 11 Block-Level Risks - GauntletCI Found Them in 660ms",
    description: "129 findings in a merged Jellyfin PR that escaped code review. GauntletCI detected them in 660ms.",
    images: ["/og/jellyfin-pr-16062.png"],
  },
};

const jsonLd = {
  "@context": "https://schema.org",
  "@type": "Article",
  headline: "A \"Performance Improvement\" PR Introduced 11 Block-Level Risks - GauntletCI Found Them in 660ms",
  description:
    "Jellyfin PR #16062 promised performance improvements but introduced 129 findings, including 11 block-level risks that escaped code review and tests.",
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
    "performance regression",
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

const readingTime = "3 min read";

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
              A "Performance Improvement" PR Introduced 11 Block-Level Risks - GauntletCI Found Them in 660ms
            </h1>
            <p className="text-xl text-muted-foreground leading-relaxed text-pretty">
              Jellyfin PR #16062 was massive: 126 files, +27,810 lines. It was reviewed, approved, and merged. Then users reported slow queries and hangs. GauntletCI found 129 findings in 660ms — 11 were block-level.
            </p>
            <div className="flex items-center gap-2 pt-1">
              <span className="text-sm font-medium text-foreground">Eric Cogen</span>
              <span className="text-muted-foreground/40 text-sm">·</span>
              <span className="text-sm text-muted-foreground">Founder, GauntletCI</span>
              <span className="text-muted-foreground/40 text-sm">·</span>
              <time className="text-sm text-muted-foreground" dateTime="2026-05-07">May 7, 2026</time>
              <span className="text-muted-foreground/40 text-sm">·</span>
              <span className="text-sm text-muted-foreground">{readingTime}</span>
            </div>
          </div>

          {/* Key Takeaways */}
          <section className="space-y-4">
            <h2 className="text-2xl font-bold tracking-tight">Key Takeaways</h2>
            <ul className="space-y-2 text-muted-foreground leading-relaxed list-disc list-inside">
              <li>A single "performance" PR introduced <strong className="text-foreground">129 behavioral risks</strong></li>
              <li><strong className="text-foreground">11 were block-level</strong> (should have prevented merge)</li>
              <li>Major categories: concurrency issues, N+1 queries, service locator anti-patterns, unsafe null handling, and more</li>
              <li>All of them escaped human review and existing tests</li>
              <li>Analysis completed in <strong className="text-foreground">660ms</strong> on a very large diff — no full build required</li>
              <li>This is exactly the kind of change that looks safe but breaks in production</li>
            </ul>
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
                It answers one question: <em>did this change introduce behavior that is not properly validated?</em> Learn more about <Link href="/articles/what-is-diff-based-analysis" className="text-cyan-400 hover:text-cyan-300 font-semibold underline">diff-based analysis</Link> and <Link href="/articles/behavioral-change-risk-formal-framework" className="text-cyan-400 hover:text-cyan-300 font-semibold underline">behavioral change risk assessment</Link>.
              </p>
            </div>
          </section>


          {/* What GauntletCI Found */}
          <section className="space-y-5">
            <h2 className="text-2xl font-bold tracking-tight">What GauntletCI Found</h2>
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              <div className="rounded-lg border border-border bg-card p-5 space-y-3">
                <div className="flex items-center justify-between">
                  <span className="text-sm font-mono font-semibold text-cyan-400">GCI0016</span>
                  <span className="text-xs font-semibold px-2 py-1 rounded bg-red-500/20 text-red-300">Block</span>
                </div>
                <h3 className="font-semibold text-foreground">Concurrency and State Risk</h3>
                <div className="text-xs font-semibold bg-cyan-500/10 text-cyan-300 px-2 py-1 rounded w-fit">5 findings</div>
                <p className="text-sm text-muted-foreground">Five deadlock candidates: blocking calls on async operations (.Wait() and .GetAwaiter().GetResult())</p>
                <div className="bg-muted p-3 rounded border-l-3 border-cyan-500/50 text-sm text-muted-foreground">
                  <strong className="text-foreground">Impact:</strong> In ASP.NET Core, blocking on async can cause deadlock via synchronization context starvation. The request hangs with no exception or log entry.
                </div>
              </div>

              <div className="rounded-lg border border-border bg-card p-5 space-y-3">
                <div className="flex items-center justify-between">
                  <span className="text-sm font-mono font-semibold text-cyan-400">GCI0012</span>
                  <span className="text-xs font-semibold px-2 py-1 rounded bg-red-500/20 text-red-300">Block</span>
                </div>
                <h3 className="font-semibold text-foreground">Security Risk</h3>
                <div className="text-xs font-semibold bg-cyan-500/10 text-cyan-300 px-2 py-1 rounded w-fit">3 findings</div>
                <p className="text-sm text-muted-foreground">Three dangerous API usages: Reflection and Activator.CreateInstance bypassing the DI container</p>
                <div className="bg-muted p-3 rounded border-l-3 border-cyan-500/50 text-sm text-muted-foreground">
                  <strong className="text-foreground">Impact:</strong> Reflection instantiation bypasses dependency injection, access controls, validation, and lifecycle management. In a media server handling authentication and content access, this is a real security concern.
                </div>
              </div>

              <div className="rounded-lg border border-border bg-card p-5 space-y-3">
                <div className="flex items-center justify-between">
                  <span className="text-sm font-mono font-semibold text-cyan-400">GCI0044</span>
                  <span className="text-xs font-semibold px-2 py-1 rounded bg-orange-500/20 text-orange-300">Warn</span>
                </div>
                <h3 className="font-semibold text-foreground">Performance Hotpath Risk</h3>
                <div className="text-xs font-semibold bg-cyan-500/10 text-cyan-300 px-2 py-1 rounded w-fit">28 findings</div>
                <p className="text-sm text-muted-foreground">Twenty-eight N+1 query patterns: LINQ queries executing inside loops</p>
                <div className="bg-muted p-3 rounded border-l-3 border-cyan-500/50 text-sm text-muted-foreground">
                  <strong className="text-foreground">Impact:</strong> For a media library with tens of thousands of items, the difference between milliseconds and minutes. The PR was titled 'Query Performance Improvements.'
                </div>
              </div>

              <div className="rounded-lg border border-border bg-card p-5 space-y-3">
                <div className="flex items-center justify-between">
                  <span className="text-sm font-mono font-semibold text-cyan-400">GCI0038</span>
                  <span className="text-xs font-semibold px-2 py-1 rounded bg-orange-500/20 text-orange-300">Warn</span>
                </div>
                <h3 className="font-semibold text-foreground">Dependency Injection Safety</h3>
                <div className="text-xs font-semibold bg-cyan-500/10 text-cyan-300 px-2 py-1 rounded w-fit">45 findings</div>
                <p className="text-sm text-muted-foreground">Forty-five service locator anti-patterns: reaching into the DI container instead of declaring dependencies</p>
                <div className="bg-muted p-3 rounded border-l-3 border-cyan-500/50 text-sm text-muted-foreground">
                  <strong className="text-foreground">Impact:</strong> Service locator code is harder to test, harder to reason about, and creates hidden coupling. The most common architectural regression in growing .NET codebases.
                </div>
              </div>

              <div className="rounded-lg border border-border bg-card p-5 space-y-3">
                <div className="flex items-center justify-between">
                  <span className="text-sm font-mono font-semibold text-cyan-400">GCI0043</span>
                  <span className="text-xs font-semibold px-2 py-1 rounded bg-orange-500/20 text-orange-300">Warn</span>
                </div>
                <h3 className="font-semibold text-foreground">Nullability and Type Safety</h3>
                <div className="text-xs font-semibold bg-cyan-500/10 text-cyan-300 px-2 py-1 rounded w-fit">15 findings</div>
                <p className="text-sm text-muted-foreground">Fifteen as-cast operations without null checks</p>
                <div className="bg-muted p-3 rounded border-l-3 border-cyan-500/50 text-sm text-muted-foreground">
                  <strong className="text-foreground">Impact:</strong> obj as SomeType returns null on failure, not an exception. Using the result without checking causes NullReferenceException at runtime with no useful context.
                </div>
              </div>

              <div className="rounded-lg border border-border bg-card p-5 space-y-3">
                <div className="flex items-center justify-between">
                  <span className="text-sm font-mono font-semibold text-cyan-400">GCI0006</span>
                  <span className="text-xs font-semibold px-2 py-1 rounded bg-orange-500/20 text-orange-300">Warn</span>
                </div>
                <h3 className="font-semibold text-foreground">Edge Case Handling</h3>
                <div className="text-xs font-semibold bg-cyan-500/10 text-cyan-300 px-2 py-1 rounded w-fit">13 findings</div>
                <p className="text-sm text-muted-foreground">Thirteen .Value accesses on nullable types without preceding null checks</p>
                <div className="bg-muted p-3 rounded border-l-3 border-cyan-500/50 text-sm text-muted-foreground">
                  <strong className="text-foreground">Impact:</strong> Explicit dereference of nullable values without verification. Runtime crashes in edge cases that tests did not cover.
                </div>
              </div>
            </div>
          </section>

          {/* Why These Issues Slipped Through */}
          <section className="space-y-5">
            <h2 className="text-2xl font-bold tracking-tight">Why These Issues Slipped Through</h2>
            <div className="space-y-4 text-muted-foreground leading-relaxed">
              <p>
                This PR is a textbook example of why traditional tools and processes often miss behavioral regressions:
              </p>
              <ul className="space-y-2 list-disc list-inside">
                <li><strong className="text-foreground"><Link href="/articles/why-code-review-misses-bugs" className="text-cyan-400 hover:text-cyan-300 font-semibold underline">Code review</Link></strong> focuses on intent ("this should be faster") and local correctness. Reviewers rarely trace every downstream impact across 126 files.</li>
                <li><strong className="text-foreground"><Link href="/articles/why-tests-miss-bugs" className="text-cyan-400 hover:text-cyan-300 font-semibold underline">Tests</Link></strong> only validate the paths the team remembered to write or update.</li>
                <li><strong className="text-foreground">Traditional static analysis</strong> excels at style, security, and code smells - but doesn't deeply analyze <em>behavioral deltas</em> in the diff.</li>
                <li><strong className="text-foreground">Performance work</strong> is especially dangerous because it often involves broad refactors that touch many implicit contracts.</li>
              </ul>
              <p className="pt-2">
                <strong className="text-foreground">GauntletCI doesn't replace your existing tools. It adds the missing layer: diff-scoped behavioral risk detection.</strong>
              </p>
            </div>
          </section>

          {/* What This Means for Your Team */}
          <section className="space-y-5">
            <h2 className="text-2xl font-bold tracking-tight">What This Means for Your Team</h2>
            <div className="space-y-4 text-muted-foreground leading-relaxed">
              <p>
                Jellyfin is a mature, well-maintained open-source project with experienced contributors — yet this kind of subtle behavioral regression still made it through.
              </p>
              <p>
                <strong className="text-foreground">This is not a failure of the Jellyfin team. It's the natural limitation of current development practices.</strong>
              </p>
              <p>
                Most .NET teams ship code under pressure: tight deadlines, large PRs, context-switching reviewers, and growing codebases full of implicit contracts.
              </p>
              <p className="pt-2">
                <strong className="text-foreground">GauntletCI answers the critical question:</strong>
              </p>
              <blockquote className="border-l-4 border-cyan-400 pl-4 italic text-muted-foreground">
                "What actual runtime behavior just changed, and what could break as a result?"
              </blockquote>
              <p className="pt-2">
                Teams using GauntletCI typically see:
              </p>
              <ul className="space-y-2 list-disc list-inside">
                <li>Fewer "it worked in testing" surprises</li>
                <li>Faster, higher-confidence code reviews</li>
                <li>Reduced emergency fixes and on-call incidents</li>
                <li>Better long-term architecture discipline</li>
              </ul>
            </div>
          </section>

          {/* Ready to Add This Safety Net */}
          <section className="space-y-5">
            <h2 className="text-2xl font-bold tracking-tight">Ready to Add This Safety Net?</h2>
            <div className="space-y-4 text-muted-foreground leading-relaxed">
              <pre className="bg-muted border border-border rounded p-4 overflow-x-auto">
                <code className="font-mono text-sm text-muted-foreground">{`dotnet tool install -g GauntletCI
gauntletci analyze --staged`}</code>
              </pre>
              <ul className="space-y-2">
                <li>Works locally in <strong className="text-foreground">seconds on typical staged diffs</strong></li>
                <li>By default, no code leaves your machine</li>
                <li>Free for personal and internal use</li>
                <li>Pro/Teams plans for advanced team features</li>
              </ul>
              <div className="pt-4 space-y-2">
                <p>
                  <Link href="https://github.com/ericcogen/gauntletci" className="text-cyan-400 hover:text-cyan-300 font-semibold">
                    → Try GauntletCI on GitHub
                  </Link>
                </p>
                <p>
                  <Link href="https://github.com/ericcogen/gauntletci-demo/pulls" className="text-cyan-400 hover:text-cyan-300 font-semibold">
                    → Browse Live Demo PRs
                  </Link>
                </p>
              </div>
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
            <RulesApplied ids={["GCI0016", "GCI0012", "GCI0044", "GCI0038", "GCI0043", "GCI0006"]} />
          </div>

          <AuthorBio variant="long" />
        </div>
      </main>

      <Footer />
    </>
  );
}

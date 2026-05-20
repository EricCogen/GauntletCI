import type { Metadata } from "next";
import JsonLd from "@/components/json-ld";
import { Header } from "@/components/header";
import { Footer } from "@/components/footer";
import { Breadcrumbs } from "@/components/breadcrumbs";
import Link from "next/link";
import { Pin } from "lucide-react";

const PAGE_SIZE = 10;

type Article = {
  href: string;
  title: string;
  description: string;
  tags: string[];
  readTime: string;
  pinned?: boolean;
};

// Newest articles first: add new entries at the bottom, display is reversed
const articles: Article[] = [
  {
    href: "/articles/the-asymmetry-of-change",
    title: "The Asymmetry of Change: Why Your Tests Are Looking the Wrong Way",
    description:
      "Why passing tests don't guarantee correct behavior. How diff-scanning can close the gap between code changes and test validation.",
    tags: ["Testing", "CI", "Diff-Based Analysis"],
    readTime: "12 min read",
    pinned: true,
  },
  {
    href: "/articles/can-ai-code-review-be-deterministic",
    title: "Can AI Code Review Tools Ever Be Deterministic?",
    description:
      "Exploring determinism vs. probabilistic judgment in code review. Why repeatable engineering evidence matters more than helpful suggestions.",
    tags: ["AI", "Code Review", "Determinism"],
    readTime: "12 min read",
    pinned: true,
  },
  {
    href: "/articles/why-code-review-misses-bugs",
    title: "Why Code Review Misses Bugs",
    description:
      "Code review catches style and obvious logic errors. It routinely misses behavioral drift, contract changes, and implicit assumptions -- not because reviewers are careless, but because diffs hide what was removed.",
    tags: ["Code Review", "Process"],
    readTime: "6 min read",
  },
  {
    href: "/articles/why-tests-miss-bugs",
    title: "Why Tests Miss Bugs",
    description:
      "Unit and integration tests catch what they're written to catch. They systematically miss behavioral regressions, cascading failures, and the side effects of change.",
    tags: ["Testing", "QA"],
    readTime: "7 min read",
  },
  {
    href: "/articles/behavioral-change-risk-formal-framework",
    title: "Behavioral Change Risk: A Formal Framework",
    description:
      "How to model and measure change risk scientifically. A repeatable framework for detecting behavioral regressions that elude code review and automated testing.",
    tags: ["Formal Methods", "Risk", "Analysis"],
    readTime: "11 min read",
  },
  {
    href: "/articles/detect-breaking-changes-before-merge",
    title: "Detect Breaking Changes Before Merge",
    description:
      "Breaking changes hide in diffs. How to mechanically detect signature changes, type changes, and behavioral contract violations without waiting for production failure.",
    tags: ["Breaking Changes", "API Design"],
    readTime: "8 min read",
  },
  {
    href: "/articles/what-is-diff-based-analysis",
    title: "What is Diff-Based Analysis?",
    description:
      "The first principles of analyzing code by what changed, not by what exists. Why focusing on change is the only deterministic way to find certain classes of bugs.",
    tags: ["Analysis", "Methodology"],
    readTime: "9 min read",
  },
  {
    href: "/articles/the-asymmetry-of-change",
    title: "The Asymmetry of Change: Why Your Tests Are Looking the Wrong Way",
    description:
      "Why passing tests don't guarantee correct behavior. How diff-scanning can close the gap between code changes and test validation.",
    tags: ["Testing", "CI", "Diff-Based Analysis"],
    readTime: "12 min read",
  },
  {
    href: "/articles/jellyfin-pr-16062-post-mortem",
    title: "Jellyfin PR #16062: Post-Mortem Analysis of 6,650+ Risk Signals",
    description:
      "Large-scale case study of 6,650+ unique behavioral risks in a merged PR. How code review and CI missed contract changes in 65 modified public methods and 400+ internal signatures.",
    tags: ["Case Study", "Jellyfin", "Post-Mortem"],
    readTime: "11 min read",
    pinned: true,
  },
  {
    href: "/articles/corpus-report-2025",
    title: "610+ Open Source .NET PRs: Behavioral Risk Corpus Analysis",
    description:
      "Analysis of 610 high-quality merged PRs across 50+ open-source .NET projects. Behavioral findings catalog: signature changes, async/await risks, and hidden breaking changes that code review and tests missed.",
    tags: ["Corpus", "Analysis", "Enterprise"],
    readTime: "13 min read",
    pinned: true,
  },
  {
    href: "/articles/azure-sdk-pr-57223-risk-analysis",
    title: "Azure SDK PR #57223: 40K+ Risk Signals Across Framework Versions",
    description:
      "Deep-dive analysis of 40K+ findings across 3 .NET framework targets. How a single PR can trigger explosive risk signals when analyzed for compatibility, demonstrating the complexity of multi-target SDK changes.",
    tags: ["Azure", "SDK", "Multi-Target"],
    readTime: "10 min read",
    pinned: true,
  },
  {
    href: "/articles/sonarqube-alternative-behavioral-gating",
    title: "Beyond SonarQube: A Behavioral Alternative to Code Smell Detection",
    description:
      "Why linter rules and code smells miss behavioral regressions. A case for deterministic behavioral analysis as a gating criterion instead of counting violations.",
    tags: ["SonarQube", "Linting", "Alternatives"],
    readTime: "9 min read",
  },
  {
    href: "/articles/case-studies",
    title: "Enterprise Case Studies: Real-World Behavioral Change Risk",
    description:
      "Collection of real production failures, missed code reviews, and test blind spots. How companies are using behavioral analysis to catch regressions that escaped traditional CI/CD.",
    tags: ["Case Studies", "Enterprise"],
    readTime: "15 min read",
  },
  {
    href: "/articles/log4net-pr-201-analysis",
    title: "log4net PR #201: 3,753+ Risk Signals in Logging Infrastructure",
    description:
      "Enterprise logging library overhaul with 3,753 behavioral risks. Signature changes dominate (1,269), with API exposure and abstraction bypass patterns affecting consumers.",
    tags: ["log4net", "Logging", "Enterprise"],
    readTime: "10 min read",
  },
  {
    href: "/articles/google-api-pr-3150-analysis",
    title: "Google API PR #3150: 3,548+ Risk Signals in Auto-Generated Clients",
    description:
      "Resource lifecycle risk patterns (712 findings) in auto-generated client libraries. Batch request handling and pagination changes in widely-used API surface.",
    tags: ["Google Cloud", "API Design", "Code Generation"],
    readTime: "10 min read",
  },
  {
    href: "/articles/stackexchange-redis-pr-3028",
    title: "StackExchange.Redis PR #3028: 3,097+ Risk Signals in Production Caching",
    description:
      "Async/await pattern refactoring (1,311 findings) in popular Redis client. Production caching risk implications and customer dependency mapping.",
    tags: ["Redis", "Async", "Production Systems"],
    readTime: "10 min read",
  },
  {
    href: "/articles/grpc-dotnet-pr-2531",
    title: "gRPC-dotnet PR #2531: 2,600+ Risk Signals in Distributed RPC Framework",
    description:
      "RPC service definition restructuring with 2,600 behavioral risks. Signature changes and API exposure modifications in framework used by millions of microservices.",
    tags: ["gRPC", "Distributed Systems", "RPC", "Microservices"],
    readTime: "9 min read",
  },
  {
    href: "/articles/anglesharp-pr-1159-analysis",
    title: "AngleSharp PR #1159: 1,793+ Risk Signals in HTML Parsing Engine",
    description:
      "HTML and CSS parser refactoring with 1,793 behavioral risks. Signature changes and DOM API modifications in web scraping and content processing library.",
    tags: ["AngleSharp", "HTML Parsing", "Web", "API Design"],
    readTime: "9 min read",
  },
];

export async function generateStaticParams() {
  const sortedArticles = [...articles].reverse();
  const totalArticles = sortedArticles.length;
  const totalPages = Math.ceil(totalArticles / PAGE_SIZE);
  
  return Array.from({ length: totalPages }, (_, i) => ({
    page: String(i + 1),
  }));
}

export function generateMetadata({ params }: { params: { page: string } }): Metadata {
  const pageNum = parseInt(params.page, 10) || 1;
  const canonical = pageNum === 1 ? "/articles" : `/articles/p/${pageNum}`;
  
  return {
    title: `Articles${pageNum > 1 ? ` - Page ${pageNum}` : ""} | GauntletCI -- .NET Change Risk and Code Review`,
    description:
      "Technical articles on behavioral regressions in .NET, why code review and tests miss certain bugs, and how diff-based analysis catches what other tools skip.",
    alternates: { canonical },
  };
}

const jsonLd = {
  "@context": "https://schema.org",
  "@type": "CollectionPage",
  name: "GauntletCI Articles",
  description: "Technical articles on .NET change risk, code review blind spots, and diff-based static analysis.",
  url: "https://gauntletci.com/articles",
};

export default function ArticlesPageRoute({ params }: { params: { page: string } }) {
  const pageParam = parseInt(params.page, 10);
  const currentPage = Math.max(1, pageParam || 1);

  // Sort: pinned first, then by order
  const sortedArticles = [...articles].reverse();
  const pinnedArticles = sortedArticles.filter((a) => a.pinned);
  const regularArticles = sortedArticles.filter((a) => !a.pinned);
  const allArticles = [...pinnedArticles, ...regularArticles];

  const totalPages = Math.ceil(allArticles.length / PAGE_SIZE);
  const validPage = Math.min(currentPage, totalPages || 1);
  const start = (validPage - 1) * PAGE_SIZE;
  const visible = allArticles.slice(start, start + PAGE_SIZE);

  return (
    <>
      <JsonLd data={jsonLd} />
      <div className="min-h-screen">
        <Header />

        <div className="mx-auto max-w-4xl px-4 sm:px-6 lg:px-8 pt-28 pb-20">
          <Breadcrumbs />
          <div className="mb-12">
            <h1 className="text-4xl sm:text-5xl font-bold tracking-tight text-balance">
              Articles
            </h1>
            <p className="mt-4 text-lg text-muted-foreground max-w-2xl text-pretty">
              Technical writing on behavioral regressions, code review blind spots,
              and why certain bugs only show up in production.
            </p>
          </div>

          {/* Articles Grid */}
          <div className="space-y-6">
            {visible.map((article) => (
              <Link
                key={article.href}
                href={article.href}
                className={`group block rounded-xl border bg-card/30 hover:bg-card/60 transition-all p-6 relative ${
                  article.pinned
                    ? "border-cyan-400/60 hover:border-cyan-400/80 bg-cyan-400/5 hover:bg-cyan-400/10"
                    : "border-border hover:border-cyan-500/30"
                }`}
              >
                {/* Pinned indicator */}
                {article.pinned && (
                  <div className="absolute bottom-4 right-4 text-cyan-400">
                    <Pin size={18} fill="currentColor" />
                  </div>
                )}

                <div className="flex flex-wrap items-center gap-2 mb-3">
                  {article.tags.map((tag) => (
                    <span
                      key={tag}
                      className="text-xs font-medium px-2 py-0.5 rounded-full bg-secondary text-muted-foreground"
                    >
                      {tag}
                    </span>
                  ))}
                  <span className="text-xs text-muted-foreground/50 ml-auto">
                    {article.readTime}
                  </span>
                </div>
                <h2 className="text-xl font-semibold text-foreground group-hover:text-cyan-400 transition-colors mb-2 pr-8">
                  {article.title}
                </h2>
                <p className="text-sm text-muted-foreground leading-relaxed">
                  {article.description}
                </p>
                <p className="mt-4 text-xs text-cyan-400/70 group-hover:text-cyan-400 transition-colors">
                  Read article →
                </p>
              </Link>
            ))}
          </div>

          {/* SEO-Friendly Pagination with Static Links */}
          {totalPages > 1 && (
            <div className="flex flex-col items-center justify-center mt-12 pt-8 border-t border-border">
              <div className="flex items-center gap-2 flex-wrap justify-center mb-6">
                {/* Previous link */}
                {validPage > 1 ? (
                  <Link
                    href={validPage === 2 ? "/articles" : `/articles/p/${validPage - 1}`}
                    className="px-4 py-2 text-sm font-medium rounded-lg border border-border bg-card/30 hover:bg-card/60 hover:border-cyan-500/30 transition-all"
                  >
                    ← Previous
                  </Link>
                ) : (
                  <span className="px-4 py-2 text-sm font-medium rounded-lg border border-border bg-card/30 opacity-30 cursor-not-allowed">
                    ← Previous
                  </span>
                )}

                {/* Page numbers */}
                <div className="flex gap-1">
                  {Array.from({ length: totalPages }, (_, i) => i + 1).map((pageNum) => (
                    <Link
                      key={pageNum}
                      href={pageNum === 1 ? "/articles" : `/articles/p/${pageNum}`}
                      className={`px-3 py-2 text-sm font-medium rounded-lg transition-all ${
                        pageNum === validPage
                          ? "bg-cyan-500/20 border border-cyan-500/50 text-cyan-400 font-semibold"
                          : "border border-border bg-card/30 hover:bg-card/60 hover:border-cyan-500/30"
                      }`}
                    >
                      {pageNum}
                    </Link>
                  ))}
                </div>

                {/* Next link */}
                {validPage < totalPages ? (
                  <Link
                    href={`/articles/p/${validPage + 1}`}
                    className="px-4 py-2 text-sm font-medium rounded-lg border border-border bg-card/30 hover:bg-card/60 hover:border-cyan-500/30 transition-all"
                  >
                    Next →
                  </Link>
                ) : (
                  <span className="px-4 py-2 text-sm font-medium rounded-lg border border-border bg-card/30 opacity-30 cursor-not-allowed">
                    Next →
                  </span>
                )}
              </div>

              <span className="text-sm text-muted-foreground">
                Page {validPage} of {totalPages}
              </span>
            </div>
          )}
        </div>

        <Footer />
      </div>
    </>
  );
}

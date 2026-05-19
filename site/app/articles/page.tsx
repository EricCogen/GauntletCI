import type { Metadata } from "next";
import JsonLd from "@/components/json-ld";
import { Header } from "@/components/header";
import { Footer } from "@/components/footer";
import { ArticleList } from "./ArticleList";
import { Breadcrumbs } from "@/components/breadcrumbs";

export const metadata: Metadata = {
  title: "Articles | GauntletCI -- .NET Change Risk and Code Review",
  description:
    "Technical articles on behavioral regressions in .NET, why code review and tests miss certain bugs, and how diff-based analysis catches what other tools skip.",
  alternates: { canonical: "/articles" },
};

const jsonLd = {
  "@context": "https://schema.org",
  "@type": "CollectionPage",
  name: "GauntletCI Articles",
  description: "Technical articles on .NET change risk, code review blind spots, and diff-based static analysis.",
  url: "https://gauntletci.com/articles",
};

// Newest articles first: add new entries at the bottom, display is reversed
const articles = [
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
      "Tests pass but bugs still reach production. Learn the categories of risk that escape test suites and why a green build is not the same as safe code.",
    tags: ["Testing", "Behavioral Drift"],
    readTime: "7 min read",
  },
  {
    href: "/articles/what-is-diff-based-analysis",
    title: "What Is Diff-Based Analysis?",
    description:
      "Diff-based analysis examines only the lines you changed, not the entire codebase. This approach is faster, more precise, and more actionable than full-codebase scanning -- and it catches a different class of bugs.",
    tags: ["Analysis", "Architecture"],
    readTime: "5 min read",
  },
  {
    href: "/articles/detect-breaking-changes-before-merge",
    title: "Detect Breaking Changes Before Merge",
    description:
      "Breaking changes in .NET code are often invisible at compile time. Learn the patterns that break callers at runtime -- removed null guards, enum member removal, serialization contract changes -- and how to catch them pre-commit.",
    tags: [".NET", "Breaking Changes"],
    readTime: "8 min read",
  },
  {
    href: "/articles/behavioral-change-risk-formal-framework",
    title: "Behavioral Change Risk: A Formal Framework",
    description:
      "A formal definition of Behavioral Change Risk (BCR) and the Behavioral Change Risk Validation (BCRV) methodology. Formalizes the validation gap that exists whenever a code change expands the behavior space beyond what any test is positioned to see.",
    tags: ["Research", "BCR", "Methodology"],
    readTime: "18 min read",
  },
  {
    href: "/articles/case-studies",
    title: "OSS Case Studies",
    description:
      "Five real .NET open-source pull requests where GauntletCI flags swallowed exceptions, broken APIs, concurrency bugs, and hardcoded configuration before they reach production.",
    tags: ["Case Studies", "Real Bugs"],
    readTime: "5 min read",
  },
  {
    href: "/articles/jellyfin-pr-16062-post-mortem",
    title: "Jellyfin PR #16062 Post-Mortem: 129 Findings Across 13 Rules",
    description:
      "Detailed analysis of GauntletCI findings from Jellyfin PR #16062. Documents all detected issues across behavioral drift, dependency concerns, and code quality metrics.",
    tags: ["Case Study", "Analysis", "Real Bugs"],
    readTime: "8 min read",
  },
  {
    href: "/articles/sonarqube-alternative-behavioral-gating",
    title: "Beyond SonarQube: Building a Behavioral Audit Layer for .NET PR Gating",
    description:
      "SonarQube catches code smells and security patterns. But it misses the semantic drift that breaks production systems. Explore how a deterministic Roslyn-based behavioral audit layer fills that gap.",
    tags: ["SonarQube", ".NET", "Code Quality", "PR Gating"],
    readTime: "9 min read",
  },
  {
    href: "/articles/corpus-report-2025",
    title: "GauntletCI Corpus Analysis 2025: 610 Merged PRs, 40K+ Findings",
    description:
      "Comprehensive analysis of 610 enterprise .NET pull requests across 10 repositories. Documents the distribution of behavioral risks: signature changes, API exposure violations, null safety issues, and resource lifecycle problems.",
    tags: ["Corpus Analysis", "Enterprise .NET", "Findings"],
    readTime: "10 min read",
    pinned: true,
  },
  {
    href: "/articles/azure-sdk-pr-57223-risk-analysis",
    title: "Azure SDK PR #57223: 6,650+ Unique Behavioral Risk Signals Across 3 Framework Versions",
    description:
      "Microsoft's Azure SDK PR #57223 generated 6,650+ unique behavioral risk signals across .NET 10.0, 8.0, and .NET Standard 2.0. Deep-dive into API exposure violations, breaking signature changes, and multiframework compatibility risks.",
    tags: ["Azure SDK", ".NET", "API Design", "Breaking Changes"],
    readTime: "12 min read",
    pinned: true,
  },
  {
    href: "/articles/log4net-pr-201-analysis",
    title: "Apache log4net PR #201: 3,753+ Risk Signals in Logging Infrastructure",
    description:
      "Enterprise logging refactoring with 3,753 behavioral risk signals. Analyze signature changes, API exposure violations, and reflection-based access patterns in foundational logging library.",
    tags: ["log4net", "Logging", "Infrastructure", "Enterprise"],
    readTime: "9 min read",
  },
  {
    href: "/articles/google-api-pr-3150-analysis",
    title: "Google API .NET Client PR #3150: 3,548+ Risk Signals in Auto-Generated APIs",
    description:
      "Auto-generated API client regeneration with 3,548 behavioral risks. Resource lifecycle issues, API exposure changes, and null dereference risks in widely-used Google API bindings.",
    tags: ["Google APIs", "Auto-Generated Code", "API Design"],
    readTime: "9 min read",
  },
  {
    href: "/articles/stackexchange-redis-pr-3028",
    title: "StackExchange.Redis PR #3028: 3,097+ Risk Signals in Production Caching",
    description:
      "Major async/await refactoring in production caching library. Analysis of 1,300+ concurrent operation pattern changes, signature modifications, and null dereference risks.",
    tags: ["StackExchange.Redis", "Caching", "Async", "Production Systems"],
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

export default function ArticlesPage() {
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

          <ArticleList articles={[...articles].reverse()} />
        </div>

        <Footer />
      </div>
    </>
  );
}

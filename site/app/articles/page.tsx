import type { Metadata } from "next";
import { Header } from "@/components/header";
import { Footer } from "@/components/footer";
import { ArticleList } from "./ArticleList";

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
    href: "/why-code-review-misses-bugs",
    title: "Why Code Review Misses Bugs",
    description:
      "Code review catches style and obvious logic errors. It routinely misses behavioral drift, contract changes, and implicit assumptions -- not because reviewers are careless, but because diffs hide what was removed.",
    tags: ["Code Review", "Process"],
    readTime: "6 min read",
  },
  {
    href: "/why-tests-miss-bugs",
    title: "Why Tests Miss Bugs",
    description:
      "Tests pass but bugs still reach production. Learn the categories of risk that escape test suites and why a green build is not the same as safe code.",
    tags: ["Testing", "Behavioral Drift"],
    readTime: "7 min read",
  },
  {
    href: "/what-is-diff-based-analysis",
    title: "What Is Diff-Based Analysis?",
    description:
      "Diff-based analysis examines only the lines you changed, not the entire codebase. This approach is faster, more precise, and more actionable than full-codebase scanning -- and it catches a different class of bugs.",
    tags: ["Analysis", "Architecture"],
    readTime: "5 min read",
  },
  {
    href: "/detect-breaking-changes-before-merge",
    title: "Detect Breaking Changes Before Merge",
    description:
      "Breaking changes in .NET code are often invisible at compile time. Learn the patterns that break callers at runtime -- removed null guards, enum member removal, serialization contract changes -- and how to catch them pre-commit.",
    tags: [".NET", "Breaking Changes"],
    readTime: "8 min read",
  },
  {
    href: "/behavioral-change-risk-formal-framework",
    title: "Behavioral Change Risk: A Formal Framework",
    description:
      "A formal definition of Behavioral Change Risk (BCR) and the Behavioral Change Risk Validation (BCRV) methodology. Formalizes the validation gap that exists whenever a code change expands the behavior space beyond what any test is positioned to see.",
    tags: ["Research", "BCR", "Methodology"],
    readTime: "18 min read",
  },
  {
    href: "/case-studies",
    title: "OSS Case Studies",
    description:
      "Five real .NET open-source pull requests where GauntletCI flags swallowed exceptions, broken APIs, concurrency bugs, and hardcoded configuration before they reach production.",
    tags: ["Case Studies", "Real Bugs"],
    readTime: "5 min read",
  },
];

export default function ArticlesPage() {
  return (
    <>
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLd) }}
      />
      <div className="min-h-screen">
        <Header />

        <div className="mx-auto max-w-4xl px-4 sm:px-6 lg:px-8 pt-28 pb-20">
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

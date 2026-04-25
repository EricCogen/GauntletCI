export type Article = {
  slug: string;
  href: string;
  title: string;
  description: string;
  ruleIds: string[];
};

export const articles: Article[] = [
  {
    slug: "why-tests-miss-bugs",
    href: "/why-tests-miss-bugs",
    title: "Why Tests Miss Bugs",
    description:
      "Tests pass but bugs still reach production. The categories of risk that escape test suites and why a green build is not the same as safe code.",
    ruleIds: ["GCI0003", "GCI0006", "GCI0032", "GCI0041"],
  },
  {
    slug: "why-code-review-misses-bugs",
    href: "/why-code-review-misses-bugs",
    title: "Why Code Review Misses Bugs",
    description:
      "Code review catches style and obvious logic errors. It routinely misses behavioral drift, contract changes, and implicit assumptions.",
    ruleIds: ["GCI0001", "GCI0003", "GCI0036", "GCI0046"],
  },
  {
    slug: "detect-breaking-changes-before-merge",
    href: "/detect-breaking-changes-before-merge",
    title: "Detect Breaking Changes Before Merge",
    description:
      "How to catch removed public APIs, signature changes, and serialization breaks at commit time instead of in downstream consumers.",
    ruleIds: ["GCI0004", "GCI0021", "GCI0047", "GCI0052"],
  },
  {
    slug: "behavioral-change-risk-formal-framework",
    href: "/behavioral-change-risk-formal-framework",
    title: "A Formal Framework for Behavioral Change Risk",
    description:
      "A structured taxonomy for behavioral, contract, concurrency, and side-effect risk in code diffs.",
    ruleIds: ["GCI0003", "GCI0036", "GCI0016", "GCI0007"],
  },
  {
    slug: "what-is-diff-based-analysis",
    href: "/what-is-diff-based-analysis",
    title: "What Is Diff-Based Analysis?",
    description:
      "Diff-based analysis evaluates only what changed in a commit. Why that scope is the right unit of risk for pre-commit checks.",
    ruleIds: ["GCI0001", "GCI0003", "GCI0004"],
  },
];

export function articlesForRule(ruleId: string): Article[] {
  return articles.filter((a) =>
    a.ruleIds.some((id) => id.toLowerCase() === ruleId.toLowerCase())
  );
}

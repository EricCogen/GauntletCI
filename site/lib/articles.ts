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

export type CaseStudy = {
  slug: string;
  href: string;
  repo: string;
  pr: string;
  title: string;
  description: string;
  ruleIds: string[];
};

export const caseStudies: CaseStudy[] = [
  {
    slug: "stackexchange-redis-swallowed-exception",
    href: "/case-studies/stackexchange-redis-swallowed-exception",
    repo: "StackExchange/StackExchange.Redis",
    pr: "PR#2995",
    title: "Swallowed Exception in StackExchange.Redis",
    description:
      "A bare catch {} block silently drops all exceptions in the message dispatch loop.",
    ruleIds: ["GCI0007"],
  },
  {
    slug: "newtonsoft-json-assignment-in-getter",
    href: "/case-studies/newtonsoft-json-assignment-in-getter",
    repo: "JamesNK/Newtonsoft.Json",
    pr: "PR#1950",
    title: "Assignment in Getter - Newtonsoft.Json",
    description:
      "Mutation inside a property getter breaks the side-effect-free contract.",
    ruleIds: ["GCI0036", "GCI0004"],
  },
  {
    slug: "efcore-breaking-api-removal",
    href: "/case-studies/efcore-breaking-api-removal",
    repo: "dotnet/efcore",
    pr: "PR#38024",
    title: "Breaking API Removal in EF Core",
    description:
      "Public API removed without Obsolete - breaks all EF Core provider authors.",
    ruleIds: ["GCI0004", "GCI0003"],
  },
  {
    slug: "nunit-thread-sleep-async",
    href: "/case-studies/nunit-thread-sleep-async",
    repo: "nunit/nunit",
    pr: "PR#5192",
    title: "Thread.Sleep in Async Context - NUnit",
    description:
      "Thread.Sleep in async context in the NUnit test framework source itself.",
    ruleIds: ["GCI0016"],
  },
  {
    slug: "azuread-hardcoded-authority",
    href: "/case-studies/azuread-hardcoded-authority",
    repo: "AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet",
    pr: "PR#3410",
    title: "Hardcoded Authority URL - Azure AD",
    description: "Hardcoded authority URL in production identity model code.",
    ruleIds: ["GCI0010", "GCI0003"],
  },
];

export function caseStudiesForRule(ruleId: string): CaseStudy[] {
  return caseStudies.filter((cs) =>
    cs.ruleIds.some((id) => id.toLowerCase() === ruleId.toLowerCase())
  );
}

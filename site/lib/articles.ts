export type Article = {
  slug: string;
  href: string;
  title: string;
  description: string;
  ruleIds: string[];
  tags: string[];
  readTime: string;
  pinned?: boolean;
};

export const articles: Article[] = [
  {
    slug: "the-asymmetry-of-change",
    href: "/articles/the-asymmetry-of-change",
    title: "The Asymmetry of Change: Why Your Tests Are Looking the Wrong Way",
    description:
      "Why passing tests don't guarantee correct behavior. How diff-scanning can close the gap between code changes and test validation.",
    ruleIds: ["GCI0003", "GCI0041", "GCI0044"],
    tags: ["Testing", "CI", "Diff-Based Analysis"],
    readTime: "12 min read",
    pinned: true,
  },
  {
    slug: "why-tests-miss-bugs",
    href: "/articles/why-tests-miss-bugs",
    title: "Why Tests Miss Bugs",
    description:
      "Tests pass but bugs still reach production. The categories of risk that escape test suites and why a green build is not the same as safe code.",
    ruleIds: ["GCI0003", "GCI0006", "GCI0032", "GCI0041"],
    tags: ["Testing", "QA"],
    readTime: "7 min read",
  },
  {
    slug: "why-code-review-misses-bugs",
    href: "/articles/why-code-review-misses-bugs",
    title: "Why Code Review Misses Bugs",
    description:
      "Code review catches style and obvious logic errors. It routinely misses behavioral drift, contract changes, and implicit assumptions.",
    ruleIds: ["GCI0001", "GCI0003", "GCI0036", "GCI0046"],
    tags: ["Code Review", "Process"],
    readTime: "6 min read",
  },
  {
    slug: "detect-breaking-changes-before-merge",
    href: "/articles/detect-breaking-changes-before-merge",
    title: "Detect Breaking Changes Before Merge",
    description:
      "How to catch removed public APIs, signature changes, and serialization breaks at commit time instead of in downstream consumers.",
    ruleIds: ["GCI0004", "GCI0021", "GCI0047", "GCI0052"],
    tags: ["Breaking Changes", "API Design"],
    readTime: "8 min read",
  },
  {
    slug: "behavioral-change-risk-formal-framework",
    href: "/articles/behavioral-change-risk-formal-framework",
    title: "A Formal Framework for Behavioral Change Risk",
    description:
      "A structured taxonomy for behavioral, contract, concurrency, and side-effect risk in code diffs.",
    ruleIds: ["GCI0003", "GCI0036", "GCI0016", "GCI0007"],
    tags: ["Formal Methods", "Risk", "Analysis"],
    readTime: "11 min read",
  },
  {
    slug: "what-is-diff-based-analysis",
    href: "/articles/what-is-diff-based-analysis",
    title: "What Is Diff-Based Analysis?",
    description:
      "Diff-based analysis evaluates only what changed in a commit. Why that scope is the right unit of risk for pre-commit checks.",
    ruleIds: ["GCI0001", "GCI0003", "GCI0004"],
    tags: ["Analysis", "Methodology"],
    readTime: "9 min read",
  },
  {
    slug: "can-ai-code-review-be-deterministic",
    href: "/articles/can-ai-code-review-be-deterministic",
    title: "Can AI Code Review Tools Ever Be Deterministic?",
    description:
      "Exploring the difference between helpful AI review and trustworthy engineering controls. Why determinism matters more than you think.",
    ruleIds: ["GCI0016", "GCI0012", "GCI0044"],
    tags: ["AI", "Code Review", "Determinism"],
    readTime: "12 min read",
    pinned: true,
  },
  {
    slug: "jellyfin-pr-16062-post-mortem",
    href: "/articles/jellyfin-pr-16062-post-mortem",
    title: "A \"Performance Improvement\" PR Introduced 11 Block-Level Risks",
    description:
      "Jellyfin PR #16062 escaped code review despite introducing 11 block-level risks. Discover why traditional tools miss behavioral regressions.",
    ruleIds: ["GCI0016", "GCI0012", "GCI0044"],
    tags: ["Case Study", "Jellyfin", "Post-Mortem"],
    readTime: "11 min read",
  },
  {
    slug: "azure-sdk-pr-57223-risk-analysis",
    href: "/articles/azure-sdk-pr-57223-risk-analysis",
    title: "How Azure SDK PR #57223 Introduced 6,650+ Unique Risk Signals",
    description:
      "Azure SDK PR #57223 generated 6,650+ unique behavioral risk signals across 3 framework versions. See why traditional tools missed them.",
    ruleIds: ["GCI0004", "GCI0003", "GCI0006", "GCI0024", "GCI0047"],
    tags: ["Azure", "SDK", "Multi-Target"],
    readTime: "10 min read",
  },
  {
    slug: "sonarqube-alternative-behavioral-gating",
    href: "/articles/sonarqube-alternative-behavioral-gating",
    title: "Beyond SonarQube: A Behavioral Alternative to Code Smell Detection",
    description:
      "Why linter rules and code smells miss behavioral regressions. A case for deterministic behavioral analysis as a gating criterion instead of counting violations.",
    ruleIds: ["GCI0003", "GCI0004", "GCI0041", "GCI0046"],
    tags: ["SonarQube", "Linting", "Alternatives"],
    readTime: "9 min read",
  },
  {
    slug: "case-studies",
    href: "/articles/case-studies",
    title: "Enterprise Case Studies: Real-World Behavioral Change Risk",
    description:
      "Collection of real production failures, missed code reviews, and test blind spots. How companies are using behavioral analysis to catch regressions that escaped traditional CI/CD.",
    ruleIds: [],
    tags: ["Case Studies", "Enterprise"],
    readTime: "15 min read",
  },
  {
    slug: "log4net-pr-201-analysis",
    href: "/articles/log4net-pr-201-analysis",
    title: "log4net PR #201: 3,753+ Risk Signals in a Major Enterprise Refactor",
    description:
      "Large-scale logging framework refactoring introducing thousands of behavioral changes across multiple code paths.",
    ruleIds: ["GCI0003", "GCI0004", "GCI0016"],
    tags: ["log4net", "Logging", "Enterprise"],
    readTime: "10 min read",
  },
  {
    slug: "google-api-pr-3150-analysis",
    href: "/articles/google-api-pr-3150-analysis",
    title: "Google API PR #3150 Analysis",
    description:
      "Behavioral risk analysis of a major Google API library pull request.",
    ruleIds: ["GCI0003", "GCI0004", "GCI0006"],
    tags: ["Google Cloud", "API Design", "Code Generation"],
    readTime: "10 min read",
  },
  {
    slug: "stackexchange-redis-pr-3028",
    href: "/articles/stackexchange-redis-pr-3028",
    title: "StackExchange.Redis PR #3028 Analysis",
    description:
      "Behavioral change risk in a critical infrastructure library pull request.",
    ruleIds: ["GCI0003", "GCI0004", "GCI0016"],
    tags: ["Redis", "Async", "Production Systems"],
    readTime: "10 min read",
  },
  {
    slug: "grpc-dotnet-pr-2531",
    href: "/articles/grpc-dotnet-pr-2531",
    title: "gRPC .NET PR #2531 Analysis",
    description:
      "Behavioral risk signals in a fundamental RPC framework pull request.",
    ruleIds: ["GCI0003", "GCI0004", "GCI0006"],
    tags: ["gRPC", "Distributed Systems", "RPC", "Microservices"],
    readTime: "9 min read",
  },
  {
    slug: "anglesharp-pr-1159-analysis",
    href: "/articles/anglesharp-pr-1159-analysis",
    title: "AngleSharp PR #1159 Analysis",
    description:
      "HTML parser library pull request introducing behavioral changes.",
    ruleIds: ["GCI0003", "GCI0004", "GCI0006"],
    tags: ["AngleSharp", "HTML Parsing", "Web", "API Design"],
    readTime: "9 min read",
  },
  {
    slug: "corpus-report-2025",
    href: "/articles/corpus-report-2025",
    title: "State of Behavioral Change Risk in .NET",
    description:
      "A field report from 610 merged C# PRs across 61 repositories, with raw findings, high-confidence findings, and outlier disclosure.",
    ruleIds: ["GCI0003", "GCI0004", "GCI0006", "GCI0015", "GCI0016", "GCI0024", "GCI0010", "GCI0036"],
    tags: ["Corpus", "Analysis", ".NET"],
    readTime: "11 min read",
  },
  {
    slug: "best-ai-code-review-tools",
    href: "/articles/best-ai-code-review-tools",
    title: "Best AI Code Review Tools for Pull Requests",
    description:
      "How to evaluate AI code review tools by evidence quality, repeatability, CI fit, noise control, and merge-gate safety.",
    ruleIds: ["GCI0003", "GCI0012", "GCI0041", "GCI0044"],
    tags: ["AI", "Code Review Tools", "Buyers Guide"],
    readTime: "8 min read",
  },
  {
    slug: "what-is-pull-request-risk-analysis",
    href: "/articles/what-is-pull-request-risk-analysis",
    title: "What Is Pull Request Risk Analysis?",
    description:
      "Pull request risk analysis evaluates how a diff changes behavior, contracts, tests, runtime safety, and production blast radius before merge.",
    ruleIds: ["GCI0001", "GCI0003", "GCI0004", "GCI0032"],
    tags: ["Risk Analysis", "Pull Requests", "Methodology"],
    readTime: "7 min read",
  },
  {
    slug: "ci-quality-gate-for-pull-requests",
    href: "/articles/ci-quality-gate-for-pull-requests",
    title: "CI Quality Gate for Pull Requests",
    description:
      "A practical framework for designing CI quality gates that block risky pull requests instead of only enforcing style, coverage, and known vulnerabilities.",
    ruleIds: ["GCI0003", "GCI0004", "GCI0007", "GCI0041"],
    tags: ["CI", "Quality Gates", "Pull Requests"],
    readTime: "7 min read",
  },
  {
    slug: "automated-code-review-tools-github",
    href: "/articles/automated-code-review-tools-github",
    title: "Automated Code Review Tools for GitHub Pull Requests",
    description:
      "How GitHub teams should choose automated code review tools for PR comments, required checks, Actions workflows, and deterministic merge protection.",
    ruleIds: ["GCI0001", "GCI0003", "GCI0016", "GCI0046"],
    tags: ["GitHub", "Automation", "Code Review"],
    readTime: "8 min read",
  },
  {
    slug: "best-code-review-tools-github",
    href: "/articles/best-code-review-tools-github",
    title: "Best Code Review Tools for GitHub",
    description:
      "A GitHub-focused guide to choosing code review tools across human review, AI assistants, security scanners, static analysis, and PR risk gates.",
    ruleIds: ["GCI0003", "GCI0004", "GCI0012", "GCI0041"],
    tags: ["GitHub", "Code Review Tools", "Buyers Guide"],
    readTime: "8 min read",
  },
  {
    slug: "coderabbit-alternative",
    href: "/articles/coderabbit-alternative",
    title: "CodeRabbit Alternative: Deterministic Pull Request Risk Analysis",
    description:
      "A buyer-focused comparison for teams evaluating AI pull request reviewers and deterministic PR risk analysis before merge.",
    ruleIds: ["GCI0001", "GCI0003", "GCI0041", "GCI0046"],
    tags: ["CodeRabbit", "Alternatives", "AI Code Review"],
    readTime: "7 min read",
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
    href: "/articles/case-studies/stackexchange-redis-swallowed-exception",
    repo: "StackExchange/StackExchange.Redis",
    pr: "PR#2995",
    title: "Swallowed Exception in StackExchange.Redis",
    description:
      "A bare catch {} block silently drops all exceptions in the message dispatch loop.",
    ruleIds: ["GCI0007"],
  },
  {
    slug: "newtonsoft-json-assignment-in-getter",
    href: "/articles/case-studies/newtonsoft-json-assignment-in-getter",
    repo: "JamesNK/Newtonsoft.Json",
    pr: "PR#1950",
    title: "Assignment in Getter - Newtonsoft.Json",
    description:
      "Mutation inside a property getter breaks the side-effect-free contract.",
    ruleIds: ["GCI0036", "GCI0004"],
  },
  {
    slug: "efcore-breaking-api-removal",
    href: "/articles/case-studies/efcore-breaking-api-removal",
    repo: "dotnet/efcore",
    pr: "PR#38024",
    title: "Breaking API Removal in EF Core",
    description:
      "Public API removed without Obsolete - breaks all EF Core provider authors.",
    ruleIds: ["GCI0004", "GCI0003"],
  },
  {
    slug: "nunit-thread-sleep-async",
    href: "/articles/case-studies/nunit-thread-sleep-async",
    repo: "nunit/nunit",
    pr: "PR#5192",
    title: "Thread.Sleep in Async Context - NUnit",
    description:
      "Thread.Sleep in async context in the NUnit test framework source itself.",
    ruleIds: ["GCI0016"],
  },
  {
    slug: "azuread-hardcoded-authority",
    href: "/articles/case-studies/azuread-hardcoded-authority",
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

import type { Metadata } from "next";
import { AlertTriangle, Database, Cpu, Shield, Activity, GitBranch, TestTube } from "lucide-react";

export const metadata: Metadata = {
  title: "Rule Library | GauntletCI Docs",
  description:
    "35 deterministic rules for detecting behavioral regressions, security risks, breaking changes, and code quality issues in C# and .NET pull request diffs.",
  alternates: { canonical: "/docs/rules" },
};

type Rule = {
  id: string;
  name: string;
  description: string;
  severity: "Block" | "Warn" | "Info";
};

type Category = {
  slug: string;
  title: string;
  tagline: string;
  icon: React.ElementType;
  color: string;
  badgeColor: string;
  rules: Rule[];
};

const categories: Category[] = [
  {
    slug: "behavior",
    title: "Behavior and Contracts",
    tagline: "Logic changes, API contracts, and behavioral shifts that tests may not exercise",
    icon: AlertTriangle,
    color: "text-amber-400",
    badgeColor: "bg-amber-400/10 text-amber-400 ring-amber-400/20",
    rules: [
      {
        id: "GCI0001",
        name: "Diff Integrity",
        severity: "Block",
        description:
          "Detects unrelated changes, formatting churn, and mixed scope within a single diff.",
      },
      {
        id: "GCI0003",
        name: "Behavioral Change Detection",
        severity: "Block",
        description:
          "Detects removed logic lines and changed method signatures that alter runtime behavior without corresponding test updates.",
      },
      {
        id: "GCI0004",
        name: "Breaking Change Risk",
        severity: "Block",
        description:
          "Detects removed public APIs and changed public method signatures that may break callers.",
      },
      {
        id: "GCI0006",
        name: "Edge Case Handling",
        severity: "Warn",
        description:
          "Detects potential null dereferences and missing validation in added code.",
      },
      {
        id: "GCI0021",
        name: "Data and Schema Compatibility",
        severity: "Block",
        description:
          "Detects removed serialization attributes and enum member removals that may break wire formats or persisted data.",
      },
      {
        id: "GCI0036",
        name: "Pure Context Mutation",
        severity: "Block",
        description:
          "Detects assignment operators inside property getter blocks or methods decorated with [Pure], indicating unexpected side effects.",
      },
      {
        id: "GCI0047",
        name: "Naming and Contract Alignment",
        severity: "Info",
        description:
          "Detects method renames where the new CRUD verb semantically contradicts the old verb, signaling an intent mismatch.",
      },
    ],
  },
  {
    slug: "security",
    title: "Security",
    tagline: "Credential exposure, unsafe APIs, PII leaks, and supply chain risks",
    icon: Shield,
    color: "text-red-400",
    badgeColor: "bg-red-400/10 text-red-400 ring-red-400/20",
    rules: [
      {
        id: "GCI0010",
        name: "Hardcoding and Configuration",
        severity: "Block",
        description:
          "Detects hardcoded IPs, URLs, connection strings, secrets, and environment names committed to source.",
      },
      {
        id: "GCI0012",
        name: "Security Risk",
        severity: "Block",
        description:
          "Detects SQL injection patterns, weak crypto algorithms (MD5, SHA1, DES), dangerous APIs (Assembly.Load, Process.Start), and credential exposure.",
      },
      {
        id: "GCI0029",
        name: "PII Entity Logging Leak",
        severity: "Warn",
        description:
          "Detects PII-sensitive terms (email, SSN, password, etc.) appearing inside log calls in added lines.",
      },
      {
        id: "GCI0048",
        name: "Insecure Random in Security Context",
        severity: "Info",
        description:
          "Detects System.Random instantiation within 5 lines of security-sensitive identifiers such as token, apikey, salt, or password. System.Random is not cryptographically secure.",
      },
      {
        id: "GCI0053",
        name: "Lockfile Changed Without Source Review",
        severity: "Warn",
        description:
          "Fires when a diff contains only lockfile changes with no accompanying source-file edits, which can hide malicious dependency upgrades.",
      },
    ],
  },
  {
    slug: "data",
    title: "Data Integrity",
    tagline: "Truncation risks, idempotency gaps, and unsafe data operations",
    icon: Database,
    color: "text-blue-400",
    badgeColor: "bg-blue-400/10 text-blue-400 ring-blue-400/20",
    rules: [
      {
        id: "GCI0015",
        name: "Data Integrity Risk",
        severity: "Block",
        description:
          "Detects unchecked casts, mass assignment without validation, and SQL ON CONFLICT IGNORE patterns that silently discard errors.",
      },
      {
        id: "GCI0022",
        name: "Idempotency and Retry Safety",
        severity: "Warn",
        description:
          "Detects HTTP POST endpoints without idempotency keys and raw INSERT statements without upsert guards, which are unsafe under retry logic.",
      },
      {
        id: "GCI0049",
        name: "Float and Double Equality Comparison",
        severity: "Info",
        description:
          "Detects direct equality (== / !=) comparisons involving floating-point values, which produce unreliable results due to precision loss.",
      },
      {
        id: "GCI0050",
        name: "SQL Column Truncation Risk",
        severity: "Info",
        description:
          "Detects short nvarchar(N) or varchar(N) column definitions that may silently truncate data when real-world values exceed the column width.",
      },
    ],
  },
  {
    slug: "concurrency",
    title: "Async and Concurrency",
    tagline: "Blocking calls, resource leaks, deadlock risks, and unsafe async patterns",
    icon: Cpu,
    color: "text-purple-400",
    badgeColor: "bg-purple-400/10 text-purple-400 ring-purple-400/20",
    rules: [
      {
        id: "GCI0016",
        name: "Concurrency and State Risk",
        severity: "Block",
        description:
          "Detects async void methods, blocking async calls (.Result, .Wait()), static mutable state, and patterns that introduce deadlock risk.",
      },
      {
        id: "GCI0024",
        name: "Resource Lifecycle",
        severity: "Warn",
        description:
          "Detects disposable resources allocated without a using statement or try/finally disposal, leading to connection and handle leaks.",
      },
      {
        id: "GCI0032",
        name: "Uncaught Exception Path",
        severity: "Block",
        description:
          "Fires when throw new is added without a corresponding Assert.Throws or Should().Throw assertion in the test suite.",
      },
      {
        id: "GCI0039",
        name: "External Service Safety",
        severity: "Block",
        description:
          "Detects unsafe HTTP client usage and external service call patterns that lack timeout, cancellation, or retry configuration.",
      },
    ],
  },
  {
    slug: "observability",
    title: "Observability and Error Handling",
    tagline: "Swallowed exceptions, silent failures, and nullable contract violations",
    icon: Activity,
    color: "text-cyan-400",
    badgeColor: "bg-cyan-400/10 text-cyan-400 ring-cyan-400/20",
    rules: [
      {
        id: "GCI0007",
        name: "Error Handling Integrity",
        severity: "Block",
        description:
          "Detects swallowed exceptions (empty catch blocks) and exception handling patterns that hide failures from callers and operators.",
      },
      {
        id: "GCI0043",
        name: "Nullability and Type Safety",
        severity: "Info",
        description:
          "Detects null-forgiving operator (!) overuse, pragma warning disables for nullable, and unchecked as-casts that bypass the type system.",
      },
    ],
  },
  {
    slug: "architecture",
    title: "Architecture and Design",
    tagline: "DI anti-patterns, layer violations, complexity, and supply chain drift",
    icon: GitBranch,
    color: "text-green-400",
    badgeColor: "bg-green-400/10 text-green-400 ring-green-400/20",
    rules: [
      {
        id: "GCI0035",
        name: "Architecture Layer Guard",
        severity: "Warn",
        description:
          "Checks added using directives against configured forbidden import pairs, enforcing architectural boundaries at commit time.",
      },
      {
        id: "GCI0038",
        name: "Dependency Injection Safety",
        severity: "Warn",
        description:
          "Detects DI anti-patterns: service locator usage, direct instantiation of injectable types, and captive dependency violations.",
      },
      {
        id: "GCI0045",
        name: "Complexity Control",
        severity: "Info",
        description:
          "Detects over-engineering: single-use interfaces, abstract classes without abstract members, and unnecessary indirection added in the diff.",
      },
      {
        id: "GCI0046",
        name: "Pattern Consistency Deviation",
        severity: "Info",
        description:
          "Detects mixed sync/async naming conventions and service locator anti-patterns introduced inconsistently within the same file.",
      },
      {
        id: "GCI0052",
        name: "Dependency Bot API Drift",
        severity: "Block",
        description:
          "Fires when a dependency bot PR (Dependabot, Renovate, Snyk) contains both a lockfile change and a public API method signature change in C# files.",
      },
    ],
  },
  {
    slug: "quality",
    title: "Code Quality and Test Gaps",
    tagline: "TODO stubs, test assertion gaps, and performance regressions",
    icon: TestTube,
    color: "text-orange-400",
    badgeColor: "bg-orange-400/10 text-orange-400 ring-orange-400/20",
    rules: [
      {
        id: "GCI0041",
        name: "Test Quality Gaps",
        severity: "Warn",
        description:
          "Detects low-quality test patterns: silenced tests ([Ignore]/[Skip]), uninformative method names, and test methods missing any assertions.",
      },
      {
        id: "GCI0042",
        name: "TODO and Stub Detection",
        severity: "Info",
        description:
          "Fires when added lines in non-test files contain TODO, FIXME, HACK markers, or throw new NotImplementedException, indicating unfinished work.",
      },
      {
        id: "GCI0044",
        name: "Performance Hotpath Risk",
        severity: "Info",
        description:
          "Detects Thread.Sleep, LINQ queries inside loops, and unbounded collection growth inside loops that degrade throughput in hot paths.",
      },
    ],
  },
];

const totalRules = categories.reduce((sum, c) => sum + c.rules.length, 0);

const jsonLd = {
  "@context": "https://schema.org",
  "@type": "ItemList",
  "name": "GauntletCI Detection Rules",
  "description": "35 deterministic rules for detecting behavioral regressions, security risks, breaking changes, and code quality issues in C# .NET diffs.",
  "url": "https://gauntletci.com/docs/rules",
  "numberOfItems": totalRules,
  "itemListElement": categories.flatMap((cat, ci) =>
    cat.rules.map((rule, ri) => ({
      "@type": "ListItem",
      "position": ci * 10 + ri + 1,
      "name": `${rule.id} ${rule.name}`,
      "url": `https://gauntletci.com/docs/rules#${rule.id}`,
      "description": rule.description,
    }))
  ),
};

function SeverityBadge({ severity }: { severity: Rule["severity"] }) {
  const styles = {
    Block: "bg-red-500/10 text-red-400 ring-red-400/20",
    Warn:  "bg-yellow-500/10 text-yellow-400 ring-yellow-400/20",
    Info:  "bg-muted text-muted-foreground ring-border",
  };
  return (
    <span className={`inline-flex items-center rounded-md px-2 py-0.5 text-xs font-medium ring-1 ring-inset ${styles[severity]}`}>
      {severity}
    </span>
  );
}

export default function RulesPage() {
  return (
    <>
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLd) }} />
      <div className="space-y-10">
      <div>
        <p className="text-sm font-semibold text-cyan-400 uppercase tracking-widest mb-3">
          Rule Library
        </p>
        <h1 className="text-4xl font-bold tracking-tight mb-4">
          {totalRules} deterministic detection rules
        </h1>
        <p className="text-lg text-muted-foreground">
          Every rule targets a specific class of behavioral, security, or structural risk in your
          diff. Rules run locally in under one second. No rule sends code to any external service.
        </p>
        <div className="mt-6 flex flex-wrap gap-2">
          {categories.map((cat) => (
            <a
              key={cat.slug}
              href={`#${cat.slug}`}
              className="inline-flex items-center gap-1.5 rounded-full border border-border px-3 py-1 text-xs font-medium text-muted-foreground hover:text-foreground hover:border-foreground/40 transition-colors"
            >
              <cat.icon className={`h-3 w-3 ${cat.color}`} />
              {cat.title}
              <span className="ml-0.5 text-muted-foreground/60">({cat.rules.length})</span>
            </a>
          ))}
        </div>
      </div>

      {categories.map((cat) => (
        <section key={cat.slug} id={cat.slug} className="pt-4">
          <div className="flex items-center gap-3 mb-1">
            <cat.icon className={`h-5 w-5 ${cat.color}`} />
            <h2 className="text-xl font-bold tracking-tight">{cat.title}</h2>
          </div>
          <p className="text-sm text-muted-foreground mb-5">{cat.tagline}</p>
          <div className="grid gap-3 sm:grid-cols-2">
            {cat.rules.map((rule) => (
              <div
                key={rule.id}
                id={rule.id}
                className="rounded-xl border border-border bg-card p-4 hover:border-border/80 transition-colors"
              >
                <div className="flex items-start justify-between gap-3 mb-3">
                  <span
                    className={`inline-flex items-center rounded-md px-2 py-0.5 text-xs font-mono font-semibold ring-1 ring-inset ${cat.badgeColor}`}
                  >
                    {rule.id}
                  </span>
                  <SeverityBadge severity={rule.severity} />
                </div>
                <h3 className="text-sm font-semibold text-foreground mb-1.5">{rule.name}</h3>
                <p className="text-xs text-muted-foreground leading-relaxed">{rule.description}</p>
              </div>
            ))}
          </div>
        </section>
      ))}

      <div className="border-t border-border pt-6">
        <p className="text-sm text-muted-foreground">
          Rules are implemented in{" "}
          <code className="bg-muted px-1 rounded text-xs">GauntletCI.Core/Rules/Implementations/</code>.
          All rules are enabled by default and can be individually disabled or reconfigured in{" "}
          <code className="bg-muted px-1 rounded text-xs">.gauntletci.json</code>.
          See the{" "}
          <a href="/docs/configuration" className="text-cyan-400 hover:underline">
            Configuration docs
          </a>{" "}
          for details.
        </p>
      </div>
    </>
  );
}

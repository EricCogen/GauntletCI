import type { Metadata } from "next";
import Link from "next/link";
import JsonLd from "@/components/json-ld";
import { Header } from "@/components/header";
import { Footer } from "@/components/footer";

export const metadata: Metadata = {
  title: "Case Study: Breaking API Removal in Entity Framework Core | GauntletCI",
  description:
    "GauntletCI catches public API removal without [Obsolete] in dotnet/efcore PR#38024. Breaking change for all third-party EF Core providers. Rule GCI0004.",
  alternates: { canonical: "/case-studies/efcore-breaking-api-removal" },
  openGraph: { images: [{ url: "/og/case-studies.png", width: 1200, height: 630 }] },
};

const jsonLd = {
  "@context": "https://schema.org",
  "@type": "Article",
  headline: "Case Study: Breaking API Removal in Entity Framework Core",
  description:
    "GauntletCI catches public API removal without [Obsolete] in dotnet/efcore PR#38024. Breaking change for all third-party EF Core providers. Rule GCI0004.",
  url: "https://gauntletci.com/case-studies/efcore-breaking-api-removal",
  author: { "@type": "Organization", name: "GauntletCI" },
  publisher: { "@type": "Organization", name: "GauntletCI", url: "https://gauntletci.com" },
  datePublished: "2025-05-03"
};

const lineColor: Record<string, string> = {
  added: "bg-green-500/10 text-green-300",
  removed: "bg-red-500/10 text-red-300",
  context: "text-muted-foreground/60",
};

const linePrefix: Record<string, string> = {
  added: "+",
  removed: "-",
  context: " ",
};

const diff: { type: string; line: string }[] = [
  { type: "context", line: "// Removed from EFCore public API without [Obsolete] first:" },
  { type: "removed", line: "public static IServiceCollection AddEntityFrameworkRelational(" },
  { type: "removed", line: "    this IServiceCollection serviceCollection)" },
  { type: "removed", line: "public IMutableEntityType FindEntityType(Type type);" },
  { type: "removed", line: "public IEntityType? FindEntityType(string name, string definingNavigationName," },
  { type: "removed", line: "    IEntityType definingEntityType);" },
];

const finding = [
  "[GCI0004] Breaking Change Risk (6 occurrences)",
  "Location : src/EFCore/Infrastructure/...",
  "Summary  : Public API removed without [Obsolete] - breaking for consumers.",
  "Evidence :",
  "  - Removed: public static IServiceCollection AddEntityFrameworkRelational(...)",
  "  - Removed: public IMutableEntityType FindEntityType(Type type)",
  "Why      : Removing public members is a breaking change for any consumers of this API.",
  "Action   : Mark as [Obsolete] first and schedule removal in a future major version.",
].join("\n");

export default function EFCoreBreakingApiRemovalPage() {
  return (
    <>
      <JsonLd data={jsonLd} />
      <Header />
      <main className="min-h-screen bg-background pt-24">
        <div className="mx-auto max-w-4xl px-4 sm:px-6 lg:px-8 py-16 sm:py-20 space-y-16">

          {/* Hero */}
          <div className="space-y-5 border-b border-border pb-12">
            <div className="flex items-center justify-between">
              <p className="text-sm font-semibold uppercase tracking-widest text-cyan-400">
                Case Study
              </p>
              <Link
                href="/case-studies"
                className="text-sm text-muted-foreground hover:text-cyan-400 transition-colors"
              >
                &larr; All case studies
              </Link>
            </div>
            <h1 className="text-4xl sm:text-5xl font-bold tracking-tight text-balance">
              Breaking API Removal in Entity Framework Core
            </h1>
            <div className="flex flex-wrap items-center gap-3">
              <span className="font-mono text-sm text-muted-foreground">
                dotnet/efcore
              </span>
              <a
                href="https://github.com/dotnet/efcore/pull/38024"
                target="_blank"
                rel="noopener noreferrer"
                className="font-mono text-sm text-cyan-400 hover:text-cyan-300 transition-colors"
              >
                PR#38024 &nearr;
              </a>
            </div>
            <div className="flex flex-wrap gap-2">
              <Link
                href="/docs/rules/GCI0004"
                className="font-mono text-xs font-medium px-2 py-0.5 rounded bg-cyan-500/10 text-cyan-400 border border-cyan-500/20 hover:bg-cyan-500/20 transition-colors"
              >
                GCI0004
              </Link>
              <Link
                href="/docs/rules/GCI0003"
                className="font-mono text-xs font-medium px-2 py-0.5 rounded bg-cyan-500/10 text-cyan-400 border border-cyan-500/20 hover:bg-cyan-500/20 transition-colors"
              >
                GCI0003
              </Link>
              <span className="text-xs font-semibold text-red-400 bg-red-500/10 border border-red-500/20 px-2 py-0.5 rounded-full">
                BLOCK
              </span>
              <span className="text-xs font-medium px-2 py-0.5 rounded-full bg-secondary text-muted-foreground">
                Breaking Changes
              </span>
              <span className="text-xs font-medium px-2 py-0.5 rounded-full bg-secondary text-muted-foreground">
                API Contracts
              </span>
            </div>
          </div>

          {/* Context */}
          <section className="space-y-4">
            <h2 className="text-2xl font-bold tracking-tight">Context</h2>
            <p className="text-muted-foreground leading-relaxed">
              Entity Framework Core PR#38024 removed several public API members
              without first marking them{" "}
              <code className="font-mono text-sm text-foreground/80 bg-muted px-1 py-0.5 rounded">
                [Obsolete]
              </code>
              . These included internal-by-convention but public-by-keyword types
              that third-party EF Core database provider authors relied on. The
              removal is a hard breaking change: any provider that compiled against
              the previous version will fail at runtime with{" "}
              <code className="font-mono text-sm text-foreground/80 bg-muted px-1 py-0.5 rounded">
                MissingMethodException
              </code>
              .
            </p>
          </section>

          {/* Diff */}
          <section className="space-y-4">
            <h2 className="text-2xl font-bold tracking-tight">Diff evidence</h2>
            <div className="rounded-xl border border-border overflow-hidden">
              <div className="border-b border-border bg-card/60 px-4 py-2 flex items-center gap-2">
                <div className="flex gap-1.5">
                  <div className="w-2.5 h-2.5 rounded-full bg-red-500/40" />
                  <div className="w-2.5 h-2.5 rounded-full bg-amber-500/40" />
                  <div className="w-2.5 h-2.5 rounded-full bg-green-500/40" />
                </div>
                <span className="text-xs font-mono text-muted-foreground/40 ml-1">
                  src/EFCore/Infrastructure/EntityFrameworkRelationalServicesBuilder.cs
                </span>
              </div>
              <div className="p-4 font-mono text-xs leading-relaxed space-y-0.5 bg-background/50">
                {diff.map((line, i) => (
                  <div
                    key={i}
                    className={`flex gap-2 px-2 py-0.5 rounded ${lineColor[line.type]}`}
                  >
                    <span className="shrink-0 w-3 select-none">
                      {linePrefix[line.type]}
                    </span>
                    <span className="whitespace-pre">{line.line}</span>
                  </div>
                ))}
              </div>
              <div className="border-t border-border bg-red-500/5 px-4 py-3">
                <pre className="text-xs font-mono text-red-400 leading-relaxed whitespace-pre-wrap">
                  {finding}
                </pre>
              </div>
            </div>
          </section>

          {/* Why it matters */}
          <section className="space-y-4">
            <h2 className="text-2xl font-bold tracking-tight">Why it matters</h2>
            <p className="text-muted-foreground leading-relaxed">
              Entity Framework Core is the primary ORM for .NET applications, with
              hundreds of third-party database providers (SQLite, PostgreSQL, MySQL,
              Oracle, Cosmos DB, and more). Each provider compiles against the
              EFCore public API. Removing public methods without an Obsolete
              deprecation period means provider authors get no compiler warning -
              their packages compile against the old API, ship to NuGet, and fail
              at runtime when used with the new EFCore version. Users see{" "}
              <code className="font-mono text-sm text-foreground/80 bg-muted px-1 py-0.5 rounded">
                MissingMethodException
              </code>{" "}
              with no actionable error message pointing to the root cause.
            </p>
          </section>

          {/* Rule links */}
          <section className="border-t border-border pt-10 space-y-4">
            <h2 className="text-lg font-semibold">Detection rules</h2>
            <ul className="space-y-2 text-sm text-muted-foreground">
              <li>
                <Link
                  href="/docs/rules/GCI0004"
                  className="text-cyan-400 hover:text-cyan-300 transition-colors"
                >
                  GCI0004 - Breaking Change Risk
                </Link>{" "}
                - flags public API removal without deprecation.
              </li>
              <li>
                <Link
                  href="/docs/rules/GCI0003"
                  className="text-cyan-400 hover:text-cyan-300 transition-colors"
                >
                  GCI0003 - Behavioral Change Detection
                </Link>{" "}
                - flags changes that alter externally observable behavior.
              </li>
            </ul>
          </section>

          {/* Navigation */}
          <div className="border-t border-border pt-10 flex flex-col sm:flex-row gap-4">
            <Link
              href="/case-studies"
              className="inline-flex items-center gap-2 rounded-lg border border-border bg-card px-6 py-3 text-sm font-semibold hover:bg-card/80 transition-colors"
            >
              &larr; All case studies
            </Link>
            <Link
              href="/docs"
              className="inline-flex items-center justify-center gap-2 rounded-lg bg-cyan-500 px-6 py-3 text-sm font-semibold text-background hover:bg-cyan-400 transition-colors"
            >
              Get started free
            </Link>
          </div>

        </div>
      </main>
      <Footer />
    </>
  );
}

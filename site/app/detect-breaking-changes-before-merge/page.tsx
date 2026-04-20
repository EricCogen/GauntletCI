import type { Metadata } from "next";
import Link from "next/link";
import { Header } from "@/components/header";
import { Footer } from "@/components/footer";

export const metadata: Metadata = {
  title: "Detect Breaking Changes Before Merge | GauntletCI",
  description:
    "Breaking changes in .NET code are often invisible at compile time. Learn the patterns that break callers at runtime and how to detect them before the PR merges.",
  alternates: { canonical: "/detect-breaking-changes-before-merge" },
};

const patterns = [
  {
    category: "Public API surface",
    items: [
      { name: "Removed public method", detail: "Callers that compile today fail at runtime after deploy if the method existed in a referenced assembly." },
      { name: "Changed method signature", detail: "Adding required parameters or changing parameter types breaks callers that used the old signature at runtime." },
      { name: "Removed interface member", detail: "Classes that implement the interface still compile. Classes in other assemblies that call the removed member fail at runtime." },
    ],
  },
  {
    category: "Serialization contracts",
    items: [
      { name: "Removed [JsonPropertyName] attribute", detail: "The property is no longer mapped from its wire name. Existing payloads silently deserialize to null or default." },
      { name: "Renamed property without attribute", detail: "The serialized name changes. Previously stored or transmitted JSON fails to deserialize." },
      { name: "Changed property type", detail: "JSON deserializers throw or silently coerce. Strongly typed consumers fail at runtime." },
    ],
  },
  {
    category: "Dependency injection and service registration",
    items: [
      { name: "Removed service registration", detail: "Code that resolves the service at runtime receives null or throws InvalidOperationException." },
      { name: "Changed constructor signature", detail: "DI containers that resolve by convention fail to construct the type at runtime." },
      { name: "Changed service lifetime", detail: "Scoped services injected into singletons produce runtime errors or subtle state sharing bugs." },
    ],
  },
  {
    category: "Database and storage",
    items: [
      { name: "Removed EF Core entity property", detail: "Migrations that are not deployed before the application update cause runtime query failures." },
      { name: "Changed column type without migration", detail: "Data read from the old schema fails to map to the new type at runtime." },
    ],
  },
];

const jsonLd = {
  "@context": "https://schema.org",
  "@type": "Article",
  "headline": "Detect Breaking Changes Before Merge",
  "description": "Breaking changes in .NET code are often invisible at compile time. Learn the patterns that break callers at runtime and how to detect them before the PR merges.",
  "url": "https://gauntletci.com/detect-breaking-changes-before-merge",
  "publisher": { "@type": "Organization", "name": "GauntletCI", "url": "https://gauntletci.com" },
};

export default function DetectBreakingChangesPage() {
  return (
    <>
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLd) }} />
      <Header />
      <main className="min-h-screen bg-background pt-24">

        <div className="mx-auto max-w-4xl px-4 sm:px-6 lg:px-8 py-16 sm:py-20 space-y-16">

          {/* Hero */}
          <div className="space-y-5 border-b border-border pb-12">
            <p className="text-sm font-semibold uppercase tracking-widest text-cyan-400">The problem</p>
            <h1 className="text-4xl sm:text-5xl font-bold tracking-tight text-balance">
              Detect breaking changes before merge
            </h1>
            <p className="text-xl text-muted-foreground leading-relaxed text-pretty">
              Breaking changes in .NET code are often invisible at compile time. The compiler
              says green. The tests pass. Production fails the moment the first real request hits
              the changed code path.
            </p>
          </div>

          {/* Why the compiler is not enough */}
          <section className="space-y-5">
            <h2 className="text-2xl font-bold tracking-tight">Why the compiler is not enough</h2>
            <p className="text-muted-foreground leading-relaxed">
              The .NET compiler catches type errors and missing references within a project. It
              does not verify runtime contracts. A method signature change may compile successfully
              if all call sites in the repository are updated - but external consumers, serialized
              payloads in databases or queues, and dynamically resolved services have no compile-time
              check.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              These are not rare edge cases. They are the normal state of any system with more than
              one service, any persistence layer, or any public API surface. The compiler's
              success guarantee is narrow. The runtime failure surface is wide.
            </p>
          </section>

          {/* Pattern categories */}
          <section className="space-y-8">
            <h2 className="text-2xl font-bold tracking-tight">Breaking change patterns in .NET</h2>
            {patterns.map((group) => (
              <div key={group.category} className="space-y-3">
                <h3 className="text-base font-semibold text-muted-foreground uppercase tracking-wide text-sm">{group.category}</h3>
                <div className="space-y-2">
                  {group.items.map((item) => (
                    <div key={item.name} className="flex gap-4 rounded-lg border border-border bg-card p-4">
                      <div className="shrink-0 mt-0.5">
                        <div className="w-1.5 h-1.5 rounded-full bg-red-500 mt-1.5" />
                      </div>
                      <div className="space-y-1">
                        <p className="text-sm font-semibold text-foreground">{item.name}</p>
                        <p className="text-xs text-muted-foreground leading-relaxed">{item.detail}</p>
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            ))}
          </section>

          {/* When to detect */}
          <section className="space-y-5 border-t border-border pt-12">
            <h2 className="text-2xl font-bold tracking-tight">The right time to detect is before the commit</h2>
            <p className="text-muted-foreground leading-relaxed">
              Finding a breaking change in post-deploy monitoring means a rollback, an incident,
              and a post-mortem. Finding it in code review means a comment and a revision. Finding
              it before the commit is created means a fix before anyone else is involved.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              GauntletCI analyzes the staged diff and flags the structural patterns above as
              high-severity findings. The analysis is deterministic - the same diff produces
              the same findings every time. There is no model to tune and no threshold to configure
              for the structural rules.
            </p>
            <div className="grid sm:grid-cols-2 gap-4">
              <div className="rounded-lg border border-border bg-card/50 p-4">
                <p className="text-sm font-semibold text-foreground mb-1">Analyzed in milliseconds</p>
                <p className="text-xs text-muted-foreground leading-relaxed">
                  GauntletCI reads the staged diff, not the entire codebase. Analysis completes
                  in under a second for typical commits.
                </p>
              </div>
              <div className="rounded-lg border border-border bg-card/50 p-4">
                <p className="text-sm font-semibold text-foreground mb-1">No CI pipeline required</p>
                <p className="text-xs text-muted-foreground leading-relaxed">
                  Runs as a pre-commit hook. The developer sees the risk before the commit is
                  created, not after the PR is opened.
                </p>
              </div>
            </div>
          </section>

          {/* CTAs */}
          <div className="border-t border-border pt-10 flex flex-col sm:flex-row gap-4">
            <Link
              href="/docs"
              className="inline-flex items-center justify-center gap-2 rounded-lg bg-cyan-500 px-6 py-3 text-sm font-semibold text-background hover:bg-cyan-400 transition-colors"
            >
              Try GauntletCI free
            </Link>
            <Link
              href="/docs/rules"
              className="inline-flex items-center gap-2 rounded-lg border border-border bg-card px-6 py-3 text-sm font-semibold hover:bg-card/80 transition-colors"
            >
              View all detection rules
            </Link>
          </div>

        </div>
      </main>
      <Footer />
    </>
  );
}

import type { Metadata } from "next";
import Link from "next/link";
import JsonLd from "@/components/json-ld";
import { Header } from "@/components/header";
import { Footer } from "@/components/footer";

export const metadata: Metadata = {
  title: "OSS Case Studies | GauntletCI Catches Real Bugs",
  description:
    "Five real .NET open-source pull requests where GauntletCI catches swallowed exceptions, broken APIs, concurrency bugs, and hardcoded configuration before merge.",
  alternates: { canonical: "/case-studies" },
  openGraph: { images: [{ url: "/og/case-studies.png", width: 1200, height: 630 }] },
};

const jsonLd = {
  "@context": "https://schema.org",
  "@type": "CollectionPage",
  name: "GauntletCI OSS Case Studies",
  description:
    "Five real .NET open-source pull requests where GauntletCI catches swallowed exceptions, broken APIs, concurrency bugs, and hardcoded configuration before merge.",
  url: "https://gauntletci.com/case-studies",
};

const studies = [
  {
    href: "/case-studies/stackexchange-redis-swallowed-exception",
    repo: "StackExchange/StackExchange.Redis",
    pr: "PR#2995",
    rules: ["GCI0007"],
    severity: "BLOCK",
    summary:
      "Swallowed exception silences connection failures in production Redis client",
    tags: ["Error Handling", "Concurrency"],
  },
  {
    href: "/case-studies/newtonsoft-json-assignment-in-getter",
    repo: "JamesNK/Newtonsoft.Json",
    pr: "PR#1950",
    rules: ["GCI0036", "GCI0004"],
    severity: "BLOCK",
    summary:
      "Mutation inside a property getter breaks the side-effect-free contract",
    tags: ["API Contracts", "Behavioral Correctness"],
  },
  {
    href: "/case-studies/efcore-breaking-api-removal",
    repo: "dotnet/efcore",
    pr: "PR#38024",
    rules: ["GCI0004", "GCI0003"],
    severity: "BLOCK",
    summary:
      "Public API removed without Obsolete - breaks all EF Core provider authors",
    tags: ["Breaking Changes", "API Contracts"],
  },
  {
    href: "/case-studies/nunit-thread-sleep-async",
    repo: "nunit/nunit",
    pr: "PR#5192",
    rules: ["GCI0016"],
    severity: "BLOCK",
    summary:
      "Thread.Sleep in async context in the NUnit test framework source itself",
    tags: ["Concurrency", "Async"],
  },
  {
    href: "/case-studies/azuread-hardcoded-authority",
    repo: "AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet",
    pr: "PR#3410",
    rules: ["GCI0010", "GCI0003"],
    severity: "BLOCK",
    summary:
      "Hardcoded authority URL in production identity model code",
    tags: ["Configuration", "Security"],
  },
];

export default function CaseStudiesPage() {
  return (
    <>
      <JsonLd data={jsonLd} />
      <Header />
      <main className="min-h-screen bg-background pt-24">
        <div className="mx-auto max-w-4xl px-4 sm:px-6 lg:px-8 py-16 sm:py-20 space-y-16">

          {/* Hero */}
          <div className="space-y-5 border-b border-border pb-12">
            <p className="text-sm font-semibold uppercase tracking-widest text-cyan-400">
              Real-world evidence
            </p>
            <h1 className="text-4xl sm:text-5xl font-bold tracking-tight text-balance">
              OSS Case Studies
            </h1>
            <p className="text-xl text-muted-foreground leading-relaxed text-pretty">
              Five real pull requests from the most-downloaded .NET open-source
              libraries. Each one contains a finding that GauntletCI would block
              before merge - a swallowed exception, a broken public API, a
              concurrency hazard, or hardcoded configuration.
            </p>
          </div>

          {/* Case study cards */}
          <div className="space-y-6">
            {studies.map((study) => (
              <Link
                key={study.href}
                href={study.href}
                className="group block rounded-xl border border-border bg-card/30 hover:bg-card/60 hover:border-cyan-500/30 transition-all p-6"
              >
                <div className="flex flex-wrap items-center gap-2 mb-3">
                  <span className="font-mono text-xs text-muted-foreground/60">
                    {study.repo}
                  </span>
                  <span className="text-xs text-muted-foreground/40">/</span>
                  <span className="font-mono text-xs text-muted-foreground/60">
                    {study.pr}
                  </span>
                  <span className="ml-auto text-xs font-semibold text-red-400 bg-red-500/10 border border-red-500/20 px-2 py-0.5 rounded-full">
                    {study.severity}
                  </span>
                </div>

                <h2 className="text-xl font-semibold text-foreground group-hover:text-cyan-400 transition-colors mb-2">
                  {study.summary}
                </h2>

                <div className="flex flex-wrap gap-2 mt-3">
                  {study.rules.map((rule) => (
                    <span
                      key={rule}
                      className="font-mono text-xs font-medium px-2 py-0.5 rounded bg-cyan-500/10 text-cyan-400 border border-cyan-500/20"
                    >
                      {rule}
                    </span>
                  ))}
                  {study.tags.map((tag) => (
                    <span
                      key={tag}
                      className="text-xs font-medium px-2 py-0.5 rounded-full bg-secondary text-muted-foreground"
                    >
                      {tag}
                    </span>
                  ))}
                </div>

                <p className="mt-4 text-xs text-cyan-400/70 group-hover:text-cyan-400 transition-colors">
                  Read case study →
                </p>
              </Link>
            ))}
          </div>

          {/* Context section */}
          <section className="border-t border-border pt-12 space-y-4">
            <h2 className="text-xl font-bold tracking-tight">
              Why these projects?
            </h2>
            <p className="text-muted-foreground leading-relaxed">
              These are not contrived examples. Each finding comes from a real pull
              request to a library with hundreds of millions of downloads. The bugs
              listed are not theoretical - they represent classes of production
              incident that have affected downstream applications.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              GauntletCI analyzes the diff, not the full codebase. These findings
              would have been produced in seconds, deterministically, on every pull
              request against these repositories.
            </p>
          </section>

          {/* CTAs */}
          <div className="border-t border-border pt-10 flex flex-col sm:flex-row gap-4">
            <Link
              href="/docs"
              className="inline-flex items-center justify-center gap-2 rounded-lg bg-cyan-500 px-6 py-3 text-sm font-semibold text-background hover:bg-cyan-400 transition-colors"
            >
              Get started free
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

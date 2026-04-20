import type { Metadata } from "next";
import Link from "next/link";
import { Header } from "@/components/header";
import { Footer } from "@/components/footer";

export const metadata: Metadata = {
  title: "Why Code Review Misses Bugs | Code Review Blind Spots",
  description:
    "Code review catches style and obvious logic errors. It routinely misses behavioral drift, contract changes, and implicit assumptions. Here is why.",
  alternates: { canonical: "/why-code-review-misses-bugs" },
};

const blindSpots = [
  {
    title: "Reviewers see what is there, not what was removed",
    body: "Diffs show additions in green and deletions in red, but attention naturally goes to the new code. A removed null guard, a deleted validation step, or a missing error handler is easy to miss in a large diff. The code still compiles and looks complete because the new path is complete - it just no longer handles the cases the deleted lines covered.",
  },
  {
    title: "Context switching limits depth",
    body: "A reviewer working through a 400-line diff across 12 files cannot hold the full behavior of every changed function in working memory. Review depth degrades with diff size. The most risky changes are often buried in the middle of a large PR where attention is thinnest.",
  },
  {
    title: "Implicit contracts are invisible",
    body: "When a method changes its parameter type or a public interface removes a member, the reviewer checks that all call sites in the PR were updated. They cannot easily verify external consumers, serialized payloads, or stored procedures that rely on the old shape. These implicit contracts exist in the runtime, not the diff.",
  },
  {
    title: "Async and concurrency risk is not visible",
    body: "An async void method, a .Result call, or a static mutable field looks like normal code to a reviewer who is not specifically scanning for concurrency anti-patterns. These issues require specialized knowledge and deliberate attention. They rarely appear in checklist-driven reviews.",
  },
  {
    title: "Social pressure compresses review time",
    body: "PRs that have been waiting get approved faster. PRs from senior engineers receive less scrutiny. PRs marked urgent or blocking get rubber-stamped. The review process has social dynamics that work against rigor precisely when risk is highest.",
  },
  {
    title: "Security patterns require specialized knowledge",
    body: "Spotting SQL injection risk, identifying weak cryptographic primitives, or recognizing a PII leak in a log statement requires active security awareness. Most reviewers are not in that mode for every PR. The pattern has to be obvious to be caught.",
  },
];

const jsonLd = {
  "@context": "https://schema.org",
  "@type": "Article",
  "headline": "Why Code Review Misses Bugs",
  "description": "Code review catches style and obvious logic errors. It routinely misses behavioral drift, contract changes, and implicit assumptions. Here is why.",
  "url": "https://gauntletci.com/why-code-review-misses-bugs",
  "publisher": { "@type": "Organization", "name": "GauntletCI", "url": "https://gauntletci.com" },
};

export default function WhyCodeReviewMissesBugsPage() {
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
              Code review blind spots
            </h1>
            <p className="text-xl text-muted-foreground leading-relaxed text-pretty">
              Code review is not a reliable safety net for behavioral risk. It is excellent
              at catching obvious errors and enforcing style. It is structurally limited at
              catching the changes that cause production incidents.
            </p>
          </div>

          {/* Why review alone is not enough */}
          <section className="space-y-5">
            <h2 className="text-2xl font-bold tracking-tight">Why review alone is not enough</h2>
            <p className="text-muted-foreground leading-relaxed">
              Code review was designed for human oversight of intent and readability. It works
              well for "does this code do what the author meant?" It works poorly for "does this
              change break an assumption made somewhere else in the system?"
            </p>
            <p className="text-muted-foreground leading-relaxed">
              The second question requires automated analysis of the diff's structural properties -
              not human pattern recognition under time pressure. Reviewers catch what they can see.
              Behavioral drift, contract changes, and removed safety checks are often invisible.
            </p>
          </section>

          {/* Blind spots */}
          <section className="space-y-5">
            <h2 className="text-2xl font-bold tracking-tight">6 categories review consistently misses</h2>
            <div className="space-y-4">
              {blindSpots.map((spot, i) => (
                <div key={spot.title} className="flex gap-4 rounded-xl border border-border bg-card p-5">
                  <span className="shrink-0 text-xs font-mono text-muted-foreground/40 mt-0.5 w-5">
                    {String(i + 1).padStart(2, "0")}
                  </span>
                  <div className="space-y-2">
                    <h3 className="font-semibold text-foreground">{spot.title}</h3>
                    <p className="text-sm text-muted-foreground leading-relaxed">{spot.body}</p>
                  </div>
                </div>
              ))}
            </div>
          </section>

          {/* Not a replacement */}
          <section className="space-y-5 border-t border-border pt-12">
            <h2 className="text-2xl font-bold tracking-tight">Automation is not a replacement for review</h2>
            <p className="text-muted-foreground leading-relaxed">
              Code review provides intent verification, domain knowledge, mentorship, and team
              alignment. No tool replaces that. What automation can do is close the structural
              blind spots - the patterns that require exhaustive diff analysis, not human judgment.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              GauntletCI runs before the commit is created and flags the behavioral and structural
              risks that reviewers are likely to miss. The goal is not to remove review - it is
              to ensure that by the time a PR opens, the obvious structural risks are already handled.
            </p>
            <div className="rounded-lg border border-cyan-500/20 bg-cyan-500/5 p-5">
              <p className="text-sm text-cyan-300 leading-relaxed">
                When reviewers do not have to spot async anti-patterns, missing null guards, or
                removed error handlers, they can spend their attention on what humans do best:
                verifying intent, catching design problems, and sharing context.
              </p>
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
              href="/why-tests-miss-bugs"
              className="inline-flex items-center gap-2 rounded-lg border border-border bg-card px-6 py-3 text-sm font-semibold hover:bg-card/80 transition-colors"
            >
              Also: Why tests miss bugs
            </Link>
          </div>

        </div>
      </main>
      <Footer />
    </>
  );
}

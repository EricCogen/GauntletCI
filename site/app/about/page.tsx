import type { Metadata } from "next";
import Link from "next/link";
import { Header } from "@/components/header";
import { Footer } from "@/components/footer";
import { AuthorBio } from "@/components/author-bio";
import { Button } from "@/components/ui/button";
import { ArrowRight, AlertCircle, Target, Zap } from "lucide-react";
import { addUtmParams } from "@/lib/utils";

export const metadata: Metadata = {
  title: "About Eric Cogen | Founder of GauntletCI",
  description:
    "Eric Cogen spent twenty years writing .NET in production. GauntletCI is the deterministic pre-commit checklist he wishes he had run before every commit.",
  alternates: { canonical: "/about" },
};

const personJsonLd = {
  "@context": "https://schema.org",
  "@type": "Person",
  name: "Eric Cogen",
  url: "https://gauntletci.com/about",
  jobTitle: "Founder, GauntletCI",
  worksFor: {
    "@type": "Organization",
    name: "GauntletCI",
    url: "https://gauntletci.com",
  },
  sameAs: [
    "https://github.com/EricCogen",
    "https://github.com/EricCogen/GauntletCI",
  ],
};

export default function AboutPage() {
  return (
    <>
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{ __html: JSON.stringify(personJsonLd) }}
      />
      <Header />
      <main className="min-h-screen bg-background pt-24">
        <div className="mx-auto max-w-3xl px-4 sm:px-6 lg:px-8 py-12">
          <Link
            href="/"
            className="text-sm text-muted-foreground hover:text-foreground"
          >
            ← Back home
          </Link>

          <div className="mt-10 mb-12">
            <h1 className="text-5xl font-bold tracking-tight">Built on scar tissue.</h1>
            <p className="mt-4 text-xl text-muted-foreground max-w-2xl leading-relaxed">
              Twenty years of watching production fail taught me something that no testing framework ever could: the bugs that destroy systems aren't the ones tests catch. They're the ones no one thought to look for. The assumptions hiding in plain sight.
            </p>
          </div>

          <AuthorBio variant="long" />

          <section className="mt-16">
            <h2 className="text-3xl font-bold tracking-tight mb-8">
              Why this matters
            </h2>
            <div className="grid grid-cols-1 md:grid-cols-3 gap-6 mb-8">
              <div className="rounded-lg border border-border bg-card/30 p-6">
                <div className="flex items-start gap-3">
                  <AlertCircle className="h-5 w-5 text-cyan-400 mt-1 flex-shrink-0" />
                  <div>
                    <h3 className="font-semibold text-foreground mb-2">Tests verify the happy path</h3>
                    <p className="text-sm text-muted-foreground">
                      Green builds ship broken code constantly. Tests check what you expect to happen, not what might happen instead. They can't catch assumptions you didn't know you were making.
                    </p>
                  </div>
                </div>
              </div>
              <div className="rounded-lg border border-border bg-card/30 p-6">
                <div className="flex items-start gap-3">
                  <Target className="h-5 w-5 text-cyan-400 mt-1 flex-shrink-0" />
                  <div>
                    <h3 className="font-semibold text-foreground mb-2">Code review doesn't scale</h3>
                    <p className="text-sm text-muted-foreground">
                      Humans reviewing diffs at scale see syntax, miss semantics. A renamed method that swapped behavior. A removed guard clause in line 42 of a 500-line diff. Fatigue is real. Attention breaks.
                    </p>
                  </div>
                </div>
              </div>
              <div className="rounded-lg border border-border bg-card/30 p-6">
                <div className="flex items-start gap-3">
                  <Zap className="h-5 w-5 text-cyan-400 mt-1 flex-shrink-0" />
                  <div>
                    <h3 className="font-semibold text-foreground mb-2">Machines see patterns humans miss</h3>
                    <p className="text-sm text-muted-foreground">
                      Deterministic rules don't get tired. Don't get distracted. Don't skip the boring checks at 11 p.m. on a Friday. They run every time. Every diff. No exceptions.
                    </p>
                  </div>
                </div>
              </div>
            </div>
          </section>

          <section className="mt-16">
            <h2 className="text-3xl font-bold tracking-tight mb-8">
              Where to go next
            </h2>
            <ul className="space-y-4 text-sm">
              <li>
                <Link
                  href="/docs/rules"
                  className="text-cyan-400 hover:underline font-semibold flex items-center gap-2"
                >
                  Browse the 30 deterministic rules
                  <ArrowRight className="h-3.5 w-3.5" />
                </Link>
                <p className="text-muted-foreground text-xs mt-1">
                  Every rule maps to a real production failure. See what GauntletCI catches.
                </p>
              </li>
              <li>
                <Link
                  href="/why-tests-miss-bugs"
                  className="text-cyan-400 hover:underline font-semibold flex items-center gap-2"
                >
                  Why tests miss bugs
                  <ArrowRight className="h-3.5 w-3.5" />
                </Link>
                <p className="text-muted-foreground text-xs mt-1">
                  The six categories of risk that escape even comprehensive test suites and CI systems.
                </p>
              </li>
              <li>
                <Link
                  href="/why-code-review-misses-bugs"
                  className="text-cyan-400 hover:underline font-semibold flex items-center gap-2"
                >
                  Why code review misses bugs
                  <ArrowRight className="h-3.5 w-3.5" />
                </Link>
                <p className="text-muted-foreground text-xs mt-1">
                  The cognitive limits of human diff review at scale, and why bots matter.
                </p>
              </li>
            </ul>
          </section>

          <section className="mt-16 rounded-lg border border-cyan-500/20 bg-gradient-to-r from-cyan-500/5 to-transparent p-8">
            <h2 className="text-2xl font-bold mb-4">
              The full story
            </h2>
            <p className="text-muted-foreground mb-6 leading-relaxed">
              Want the real narrative? Twenty years of production disasters, every escalation call at midnight, each alert that didn't fire, the bugs that slipped through code review, the fixes that introduced regressions. This is the origin story—not the polished pitch, but the actual scars that demanded a solution. Every rule in GauntletCI came from something that broke. Read how.
            </p>
            <Link
              href="https://github.com/EricCogen/GauntletCI/blob/main/STORY.md"
              target="_blank"
              rel="noopener noreferrer"
              className="text-cyan-400 hover:underline font-semibold flex items-center gap-2 w-fit"
            >
              Read STORY.md on GitHub
              <ArrowRight className="h-3.5 w-3.5" />
            </Link>
          </section>

           <section className="mt-16 rounded-xl border border-border bg-gradient-to-r from-cyan-500/5 to-blue-500/5 p-8 text-center">
            <h2 className="text-2xl font-bold mb-3">Stop catching bugs at 2 a.m.</h2>
            <p className="text-muted-foreground mb-6">
              GauntletCI runs in under 2 minutes from install to your first diff audit. No account. No setup. No compromise. Just the thirty rules that actually matter, running on every commit before anything can break.
            </p>
            <Button size="lg" asChild className="bg-cyan-500 hover:bg-cyan-600 text-black font-semibold">
              <Link href={addUtmParams("/#quickstart", "about", "cta_button", "install_now")}>
                Get Started — It Takes 2 Minutes
                <ArrowRight className="ml-2 h-4 w-4" />
              </Link>
            </Button>
          </section>
        </div>
      </main>
      <Footer />
    </>
  );
}

import type { Metadata } from "next";
import Link from "next/link";
import { Header } from "@/components/header";
import { Footer } from "@/components/footer";
import { AuthorBio } from "@/components/author-bio";

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
            &larr; Back home
          </Link>

          <h1 className="mt-6 text-4xl font-bold tracking-tight">About</h1>
          <p className="mt-3 text-lg text-muted-foreground">
            Why GauntletCI exists, and who built it.
          </p>

          <AuthorBio variant="long" />

          <section className="mt-12">
            <h2 className="text-2xl font-bold tracking-tight mb-4">
              Where to go next
            </h2>
            <ul className="space-y-3 text-sm">
              <li>
                <Link
                  href="/docs/rules"
                  className="text-cyan-400 hover:underline"
                >
                  Browse the 30 deterministic rules
                </Link>
                <span className="text-muted-foreground">
                  {" "}-- every rule maps to a real production failure.
                </span>
              </li>
              <li>
                <Link
                  href="/why-tests-miss-bugs"
                  className="text-cyan-400 hover:underline"
                >
                  Why tests miss bugs
                </Link>
                <span className="text-muted-foreground">
                  {" "}-- the six categories of risk that escape green builds.
                </span>
              </li>
              <li>
                <Link
                  href="/why-code-review-misses-bugs"
                  className="text-cyan-400 hover:underline"
                >
                  Why code review misses bugs
                </Link>
                <span className="text-muted-foreground">
                  {" "}-- the human limits of diff review at scale.
                </span>
              </li>
              <li>
                <Link
                  href="https://github.com/EricCogen/GauntletCI/blob/main/STORY.md"
                  target="_blank"
                  rel="noopener noreferrer"
                  className="text-cyan-400 hover:underline"
                >
                  The original STORY.md
                </Link>
                <span className="text-muted-foreground">
                  {" "}-- the long-form scar tissue narrative on GitHub.
                </span>
              </li>
            </ul>
          </section>
        </div>
      </main>
      <Footer />
    </>
  );
}

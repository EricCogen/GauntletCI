import type { Metadata } from "next";
import Link from "next/link";
import { Header } from "@/components/header";
import { Footer } from "@/components/footer";
import { Breadcrumbs } from "@/components/breadcrumbs";
import JsonLd from "@/components/json-ld";

export const metadata: Metadata = {
  title: "AngleSharp PR #1159: 1,793 Behavioral Risk Signals in HTML Parsing Engine | GauntletCI",
  description: "PR #1159 introduces 1,793 behavioral risk signals in HTML/CSS parser refactoring: signature changes, API exposure, and type safety risks.",
  alternates: { canonical: "/articles/anglesharp-pr-1159-analysis" },
  keywords: ["AngleSharp", "HTML parsing", "CSS parsing", "behavioral risk", "GauntletCI", "static analysis"],
  authors: [{ name: "Eric Cogen", url: "https://github.com/EricCogen" }],
  creator: "Eric Cogen",
  publisher: "GauntletCI",
  openGraph: {
    title: "AngleSharp PR #1159: 1,793+ Risk Signals in HTML Parser",
    description: "HTML and CSS parser refactoring with 1,793 behavioral risks in web scraping and content processing library.",
    url: "https://gauntletci.com/articles/anglesharp-pr-1159-analysis",
    type: "article",
    images: [{ url: "/og/anglesharp-pr-1159.png", width: 1200, height: 630, alt: "AngleSharp PR #1159 Risk Analysis" }],
  },
  twitter: {
    card: "summary_large_image",
    title: "AngleSharp PR #1159: 1,793+ HTML Parser Risk Signals",
    description: "Parser refactoring with signature changes, API exposure, and DOM API modifications.",
    images: ["/og/anglesharp-pr-1159.png"],
  },
};

const jsonLd = {
  "@context": "https://schema.org",
  "@type": "Article",
  headline: "AngleSharp PR #1159: 1,793+ Behavioral Risk Signals in HTML Parsing Engine",
  description: "Analysis of 1,793 behavioral risk signals from HTML/CSS parser refactoring in AngleSharp.",
  image: "/og/anglesharp-pr-1159.png",
  datePublished: "2026-05-19T00:00:00Z",
  author: { "@type": "Person", name: "Eric Cogen", url: "https://github.com/EricCogen" },
  publisher: { "@type": "Organization", name: "GauntletCI", url: "https://gauntletci.com", logo: { "@type": "ImageObject", url: "https://gauntletci.com/icon.svg" } },
  mainEntityOfPage: { "@type": "WebPage", "@id": "https://gauntletci.com/articles/anglesharp-pr-1159-analysis" },
  keywords: ["HTML parsing", "CSS parsing", "web scraping", "behavioral risk"],
};

const findings = [
  { rule: "GCI0003", title: "Signature Changes", count: 1265, severity: "Block", description: "Breaking parser method changes" },
  { rule: "GCI0004", title: "API Exposure", count: 186, severity: "High", description: "Parser internals exposed to public" },
  { rule: "GCI0006", title: "Null Dereference Risk", count: 64, severity: "Warn", description: "DOM node access without null checks" },
  { rule: "GCI0015", title: "Async Pattern Changes", count: 56, severity: "High", description: "Async parsing changes" },
  { rule: "GCI0036", title: "DOM API Changes", count: 48, severity: "Warn", description: "Document object model changes" },
];

export default function AngleSharpAnalysisPage() {
  return (
    <main className="min-h-screen flex flex-col">
      <Header />
      <Breadcrumbs items={[{ label: "Articles", href: "/articles" }, { label: "AngleSharp PR #1159" }]} />

      <article className="flex-1 max-w-3xl mx-auto px-6 py-12">
        <JsonLd data={jsonLd} />
        <h1 className="text-4xl font-bold mb-4">AngleSharp PR #1159: 1,793+ Risk Signals in HTML Parsing Engine</h1>
        <p className="text-lg text-gray-600 mb-8">
          AngleSharp powers HTML and CSS parsing across web scrapers, testing tools, and content processors. PR #1159 restructured the parsing engine with <strong>1,793+ behavioral risk signals</strong>, predominantly signature changes and DOM API modifications. We analyze the scope of this parser transformation.
        </p>

        <section className="mb-12">
          <h2 className="text-2xl font-bold mb-4">The Numbers</h2>
          <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-8">
            <div className="bg-red-50 p-4 rounded border-l-4 border-red-600">
              <div className="text-3xl font-bold text-red-600">1,793+</div>
              <div className="text-sm text-gray-600">Risk Signals</div>
            </div>
            <div className="bg-red-50 p-4 rounded border-l-4 border-red-600">
              <div className="text-3xl font-bold text-red-600">1,265</div>
              <div className="text-sm text-gray-600">Signature Changes</div>
            </div>
            <div className="bg-red-50 p-4 rounded border-l-4 border-red-600">
              <div className="text-3xl font-bold text-red-600">186</div>
              <div className="text-sm text-gray-600">API Exposures</div>
            </div>
            <div className="bg-orange-50 p-4 rounded border-l-4 border-orange-600">
              <div className="text-3xl font-bold text-orange-600">120</div>
              <div className="text-sm text-gray-600">Async/DOM</div>
            </div>
          </div>
        </section>

        <section className="mb-12">
          <h2 className="text-2xl font-bold mb-4">HTML Parser Fragmentation Risk</h2>
          <p className="mb-4">
            AngleSharp is used by applications that depend on stable parsing behavior. Over 70% of findings are signature changes (1,265 of 1,793), which means:
          </p>
          <ul className="list-disc list-inside space-y-2 mb-4">
            <li>XPath queries and DOM selectors may break</li>
            <li>Parser configuration APIs have changed</li>
            <li>Tag and attribute processing contracts modified</li>
            <li>Async parsing workflows restructured</li>
          </ul>
          <p className="mb-4">
            Web scrapers and content processors built against AngleSharp will fail when updating to this version unless updated in lockstep.
          </p>
        </section>

        <section className="mb-12">
          <h2 className="text-2xl font-bold mb-4">DOM API Stability (186 exposures)</h2>
          <p className="mb-4">
            10% of findings indicate parser internals now exposed as public APIs. This creates maintenance burden:
          </p>
          <ul className="list-disc list-inside space-y-2 mb-4">
            <li>Applications depend on internal parser state</li>
            <li>Future refactoring becomes constrained</li>
            <li>Parser optimization opportunities are locked in place</li>
          </ul>
        </section>

        <section className="mb-12">
          <h2 className="text-2xl font-bold mb-4">Methodology & Data Accuracy</h2>
          <p className="mb-4">
            The 1,793 findings represent real behavioral modifications to HTML/CSS parsing APIs in AngleSharp PR #1159.
          </p>
          <p className="text-sm text-gray-600 mt-6">
            Data source: <a href="https://gauntletci.com" className="text-blue-600 hover:underline">GauntletCI Corpus</a> analysis of merged PR #1159 in AngleSharp/AngleSharp repository.
          </p>
        </section>

        <section className="mt-12 pt-8 border-t">
          <h2 className="text-2xl font-bold mb-4">Related Articles</h2>
          <ul className="space-y-2 text-sm">
            <li><Link href="/articles/corpus-report-2025" className="text-blue-600 hover:underline">GauntletCI Corpus Analysis 2025</Link> — 610 PRs across enterprise .NET ecosystem</li>
            <li><Link href="/articles/azure-sdk-pr-57223-risk-analysis" className="text-blue-600 hover:underline">Azure SDK PR #57223 Analysis</Link> — 6,650+ signals in major framework refactoring</li>
            <li><Link href="/articles/detect-breaking-changes-before-merge" className="text-blue-600 hover:underline">Detect Breaking Changes Before Merge</Link> — Patterns that escape traditional analysis</li>
          </ul>
        </section>
      </article>

      <Footer />
    </main>
  );
}

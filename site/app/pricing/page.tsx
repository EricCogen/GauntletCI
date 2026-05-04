import type { Metadata } from "next";
import Link from "next/link";
import { Header } from "@/components/header";
import { Pricing } from "@/components/pricing";
import { Footer } from "@/components/footer";

export const metadata: Metadata = {
  title: "Pricing | GauntletCI -- Free to start, pay when your team needs it",
  description:
    "GauntletCI is free forever for local use. Pro adds AI enrichment and MCP integration. Teams adds CI enforcement and GitHub inline comments. No account required to start.",
  alternates: { canonical: "/pricing" },
  openGraph: { images: [{ url: '/og/pricing.png', width: 1200, height: 630 }] },
};

const jsonLd = {
  "@context": "https://schema.org",
  "@type": "SoftwareApplication",
  name: "GauntletCI",
  applicationCategory: "DeveloperApplication",
  operatingSystem: "Windows, macOS, Linux",
  offers: [
    {
      "@type": "Offer",
      name: "Community",
      price: "0",
      priceCurrency: "USD",
      description: "Deterministic change-risk detection. Local-first, forever free. No account required.",
    },
    {
      "@type": "Offer",
      name: "Pro",
      price: "19",
      priceCurrency: "USD",
      description: "Everything in Community plus local LLM enrichment, baseline delta mode, and MCP server integration.",
    },
    {
      "@type": "Offer",
      name: "Teams",
      price: "49",
      priceCurrency: "USD",
      description: "Everything in Pro plus CI gate with GitHub inline comments, Slack/Teams notifications, and architecture policy enforcement.",
    },
    {
      "@type": "Offer",
      name: "Enterprise",
      description: "Everything in Teams plus incident correlation, postmortem analysis, and complete audit trail export. Custom pricing.",
    },
  ],
};

export default function PricingPage() {
  return (
    <>
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLd) }}
      />
      <div className="min-h-screen">
        <Header />

        <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8 pt-16 pb-4">
          <Link href="/" className="text-sm text-muted-foreground hover:text-cyan-400 transition-colors">
            ← Back to home
          </Link>
          <h1 className="mt-6 text-4xl sm:text-5xl font-bold tracking-tight text-balance">
            Simple, transparent pricing
          </h1>
          <p className="mt-4 text-lg text-muted-foreground max-w-2xl text-pretty">
            Start free -- no account, no telemetry, no install gate.
            Upgrade when your team needs CI enforcement, AI enrichment, or collaboration features.
          </p>
        </div>

        <Pricing standalone />

        <Footer />
      </div>
    </>
  );
}

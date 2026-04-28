import type { Metadata } from "next";
import { WhyExists } from "@/components/why-exists";
import { WhoItsFor } from "@/components/who-its-for";
import { HowItWorks } from "@/components/how-it-works";
import { Header } from "@/components/header";
import { Hero } from "@/components/hero";
import { Stats } from "@/components/stats";
import { VisibilityGap } from "@/components/visibility-gap";
import { BehavioralChangeRisk } from "@/components/behavioral-change-risk";
import { TerminalBlock } from "@/components/terminal-block";
import { ProofExample } from "@/components/proof-example";
import { Features } from "@/components/features";
import { FeaturesBenefits } from "@/components/features-benefits";
import { Comparison } from "@/components/comparison";
import { Integrations } from "@/components/integrations";
import { ProvenReliability } from "@/components/proven-reliability";
import { WhyTeams } from "@/components/why-teams";
import { QuickStart } from "@/components/quick-start";
import { DetectionRules } from "@/components/detection-rules";
import { Pricing } from "@/components/pricing";
import { Faq } from "@/components/faq";
import { SecurityPrivacy } from "@/components/security-privacy";
import { DefinitionBlock } from "@/components/definition-block";
import { Footer } from "@/components/footer";

export const metadata: Metadata = {
  title: "GauntletCI | Pre-Commit Behavioral Change Risk Detection for .NET",
  description:
    "GauntletCI is a local-first Behavioral Change Risk engine for C# and .NET teams. Detect behavioral regressions, contract breaks, and logic shifts in your diff -- before they reach code review.",
  alternates: { canonical: "/" },
  openGraph: { images: [{ url: '/og/home.png', width: 1200, height: 630 }] },
};

export default function Home() {
  return (
    <main className="min-h-screen">
      <Header />
      <Hero />
      <Stats />
      <VisibilityGap />
      <BehavioralChangeRisk />
      <TerminalBlock />
      <HowItWorks />
      <ProofExample />
      <Features />
      <FeaturesBenefits />
      <Comparison />
      <ProvenReliability />
      <DetectionRules />
      <WhyTeams />
      <WhoItsFor />
      <WhyExists />
      <Integrations />
      <QuickStart />
      <Faq />
      <SecurityPrivacy />
      <Pricing />
      <DefinitionBlock />
      <Footer />
    </main>
  );
}

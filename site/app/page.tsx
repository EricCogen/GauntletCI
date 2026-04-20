import { Header } from "@/components/header";
import { Hero } from "@/components/hero";
import { Stats } from "@/components/stats";
import { VisibilityGap } from "@/components/visibility-gap";
import { TerminalBlock } from "@/components/terminal-block";
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

export default function Home() {
  return (
    <main className="min-h-screen">
      <Header />
      <Hero />
      <Stats />
      <VisibilityGap />
      <TerminalBlock />
      <Features />
      <FeaturesBenefits />
      <Integrations />
      <Comparison />
      <ProvenReliability />
      <WhyTeams />
      <QuickStart />
      <DetectionRules />
      <Faq />
      <SecurityPrivacy />
      <Pricing />
      <DefinitionBlock />
      <Footer />
    </main>
  );
}

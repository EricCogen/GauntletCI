import { Header } from "@/components/header";
import { Hero } from "@/components/hero";
import { Stats } from "@/components/stats";
import { Features } from "@/components/features";
import { FeaturesBenefits } from "@/components/features-benefits";
import { ProvenReliability } from "@/components/proven-reliability";
import { QuickStart } from "@/components/quick-start";
import { DetectionRules } from "@/components/detection-rules";
import { Pricing } from "@/components/pricing";
import { Footer } from "@/components/footer";

export default function Home() {
  return (
    <main className="min-h-screen">
      <Header />
      <Hero />
      <Stats />
      <Features />
      <FeaturesBenefits />
      <ProvenReliability />
      <QuickStart />
      <DetectionRules />
      <Pricing />
      <Footer />
    </main>
  );
}

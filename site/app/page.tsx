import { Header } from "@/components/header";
import { Hero } from "@/components/hero";
import { Stats } from "@/components/stats";
import { Features } from "@/components/features";
import { ProvenReliability } from "@/components/proven-reliability";
import { QuickStart } from "@/components/quick-start";
import { DetectionRules } from "@/components/detection-rules";
import { Footer } from "@/components/footer";

export default function Home() {
  return (
    <main className="min-h-screen">
      <Header />
      <Hero />
      <Stats />
      <Features />
      <ProvenReliability />
      <QuickStart />
      <DetectionRules />
      <Footer />
    </main>
  );
}

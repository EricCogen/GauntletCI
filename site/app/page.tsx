import type { Metadata } from "next";
import JsonLd from "@/components/json-ld";
import { PipelineDiagram } from "@/components/pipeline-diagram";
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

const faqSchema = {
  "@context": "https://schema.org",
  "@type": "FAQPage",
  "mainEntity": [
    {
      "@type": "Question",
      "name": "How does GauntletCI compare to SonarQube?",
      "acceptedAnswer": {
        "@type": "Answer",
        "text": "SonarQube analyzes the entire codebase on a schedule and requires a server, account, and network access. GauntletCI analyzes only the lines that changed in the current diff, runs entirely on the developer's machine in under one second, requires no account or cloud connection, and installs as a pre-commit hook. SonarQube is a full codebase quality platform; GauntletCI is a focused change-risk detector that runs before a commit is ever created."
      }
    },
    {
      "@type": "Question",
      "name": "How does GauntletCI compare to Semgrep?",
      "acceptedAnswer": {
        "@type": "Answer",
        "text": "Semgrep is a pattern-matching engine that scans files or the full codebase. GauntletCI scopes every rule to the changed diff, meaning it only flags risks introduced by the current change, not pre-existing issues in unchanged files. GauntletCI also includes incident correlation, local LLM enrichment, and baseline delta mode, which Semgrep does not offer. GauntletCI's free tier includes all rules with no account required."
      }
    },
    {
      "@type": "Question",
      "name": "How does GauntletCI compare to Snyk?",
      "acceptedAnswer": {
        "@type": "Answer",
        "text": "Snyk is primarily a dependency and container vulnerability scanner that requires cloud connectivity and a Snyk account. GauntletCI detects behavioral, structural, and security risks in first-party code changes, not dependency vulnerabilities, and runs 100% locally with no data transmitted. GauntletCI is suitable for air-gapped environments and organizations with strict data residency requirements; Snyk is not."
      }
    },
    {
      "@type": "Question",
      "name": "How does GauntletCI compare to CodeQL / GitHub Advanced Security?",
      "acceptedAnswer": {
        "@type": "Answer",
        "text": "CodeQL performs deep semantic analysis of the full codebase and requires compilation and significant compute time, typically minutes per run. GauntletCI analyzes only the changed diff in under one second with no compilation step, making it suitable as a pre-commit hook in the developer's local workflow. CodeQL is better for periodic deep security audits; GauntletCI is better for fast, continuous feedback on every commit."
      }
    },
    {
      "@type": "Question",
      "name": "How does GauntletCI compare to Code Climate?",
      "acceptedAnswer": {
        "@type": "Answer",
        "text": "Code Climate is a SaaS platform that analyzes full repositories for maintainability and test coverage trends over time. It requires uploading code to a cloud service. GauntletCI is fully local, diff-scoped, and focused on change risk rather than codebase health metrics. GauntletCI does not require a cloud account, never transmits code, and produces results in under one second, making it a complement to, not a replacement for, Code Climate's longitudinal reporting."
      }
    },
    {
      "@type": "Question",
      "name": "Does GauntletCI work in air-gapped or offline environments?",
      "acceptedAnswer": {
        "@type": "Answer",
        "text": "Yes. GauntletCI runs entirely on the local machine. No diff, finding, file path, or telemetry is transmitted. The optional local LLM enrichment feature (--with-llm) uses a locally hosted Ollama model, with no network call. This makes GauntletCI suitable for classified, regulated, or air-gapped environments where cloud-based analysis tools are prohibited."
      }
    },
    {
      "@type": "Question",
      "name": "What is baseline delta mode and do other tools offer it?",
      "acceptedAnswer": {
        "@type": "Answer",
        "text": "Baseline delta mode (gauntletci baseline capture) snapshots all current findings and suppresses them from future runs. Only net-new findings (risks introduced after the baseline) are reported. This eliminates alert fatigue from pre-existing issues in legacy codebases. SonarQube offers a similar 'new code' concept but requires server setup. Semgrep, Snyk, CodeQL, and Code Climate do not offer equivalent pre-commit baseline suppression."
      }
    }
  ]
}

export default function Home() {
  return (
    <main className="min-h-screen">
      <JsonLd data={faqSchema} />
      <Header />
      <Hero />
      <Stats />
      <VisibilityGap />
      <PipelineDiagram />
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

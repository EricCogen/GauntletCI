import type { Metadata } from 'next'
import { Geist, Geist_Mono } from 'next/font/google'
import { Analytics } from '@vercel/analytics/next'
import './globals.css'

const jsonLd = {
  "@context": "https://schema.org",
  "@type": "SoftwareApplication",
  "name": "GauntletCI",
  "applicationCategory": "DeveloperApplication",
  "operatingSystem": "Windows, macOS, Linux",
  "description": "Deterministic pre-commit change-risk detection for .NET. Sub-second analysis, runs locally, high-signal output.",
  "url": "https://ericcogen.github.io/GauntletCI",
  "offers": [
    {
      "@type": "Offer",
      "name": "Community",
      "description": "Local-first, forever free. Includes all detection rules, pre-commit hook, per-repo configuration, and 100% local execution.",
      "price": "0",
      "priceCurrency": "USD",
      "availability": "https://schema.org/InStock",
      "category": "Free"
    },
    {
      "@type": "Offer",
      "name": "Pro",
      "description": "Everything in Community plus baseline delta mode, local LLM enrichment, and AI assistant integration (MCP Server).",
      "price": "19",
      "priceCurrency": "USD",
      "availability": "https://schema.org/PreOrder",
      "priceSpecification": {
        "@type": "UnitPriceSpecification",
        "price": "19",
        "priceCurrency": "USD",
        "billingDuration": {
          "@type": "QuantitativeValue",
          "value": 1,
          "unitCode": "MON"
        }
      }
    },
    {
      "@type": "Offer",
      "name": "Teams",
      "description": "Everything in Pro plus CI gate with GitHub inline comments, Slack/Teams notifications, Jira/Linear ticket context, and architecture policy enforcement.",
      "price": "49",
      "priceCurrency": "USD",
      "availability": "https://schema.org/PreOrder",
      "priceSpecification": {
        "@type": "UnitPriceSpecification",
        "price": "49",
        "priceCurrency": "USD",
        "billingDuration": {
          "@type": "QuantitativeValue",
          "value": 1,
          "unitCode": "MON"
        }
      }
    },
    {
      "@type": "Offer",
      "name": "Enterprise",
      "description": "Everything in Teams plus incident correlation, postmortem analysis, and complete audit trail export. Custom pricing.",
      "availability": "https://schema.org/InStock",
      "category": "Enterprise"
    }
  ]
}

const _geist = Geist({ subsets: ["latin"] });
const _geistMono = Geist_Mono({ subsets: ["latin"] });

export const metadata: Metadata = {
  title: 'GauntletCI - Pre-commit Change-Risk Detection',
  description: 'Analyze what changed in your code and flag unverified behavioral changes before they reach code review. Sub-second analysis, runs locally, high-signal output.',
  generator: 'v0.app',
  icons: {
    icon: [
      {
        url: '/icon-light-32x32.png',
        media: '(prefers-color-scheme: light)',
      },
      {
        url: '/icon-dark-32x32.png',
        media: '(prefers-color-scheme: dark)',
      },
      {
        url: '/icon.svg',
        type: 'image/svg+xml',
      },
    ],
    apple: '/apple-icon.png',
  },
}

const jsonLdFaq = {
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
        "text": "Semgrep is a pattern-matching engine that scans files or the full codebase. GauntletCI scopes every rule to the changed diff, meaning it only flags risks introduced by the current change — not pre-existing issues in unchanged files. GauntletCI also includes incident correlation, local LLM enrichment, and baseline delta mode, which Semgrep does not offer. GauntletCI's free tier includes all rules with no account required."
      }
    },
    {
      "@type": "Question",
      "name": "How does GauntletCI compare to Snyk?",
      "acceptedAnswer": {
        "@type": "Answer",
        "text": "Snyk is primarily a dependency and container vulnerability scanner that requires cloud connectivity and a Snyk account. GauntletCI detects behavioral, structural, and security risks in first-party code changes — not dependency vulnerabilities — and runs 100% locally with no data transmitted. GauntletCI is suitable for air-gapped environments and organizations with strict data residency requirements; Snyk is not."
      }
    },
    {
      "@type": "Question",
      "name": "How does GauntletCI compare to CodeQL / GitHub Advanced Security?",
      "acceptedAnswer": {
        "@type": "Answer",
        "text": "CodeQL performs deep semantic analysis of the full codebase and requires compilation and significant compute time — typically minutes per run. GauntletCI analyzes only the changed diff in under one second with no compilation step, making it suitable as a pre-commit hook in the developer's local workflow. CodeQL is better for periodic deep security audits; GauntletCI is better for fast, continuous feedback on every commit."
      }
    },
    {
      "@type": "Question",
      "name": "How does GauntletCI compare to Code Climate?",
      "acceptedAnswer": {
        "@type": "Answer",
        "text": "Code Climate is a SaaS platform that analyzes full repositories for maintainability and test coverage trends over time. It requires uploading code to a cloud service. GauntletCI is fully local, diff-scoped, and focused on change risk rather than codebase health metrics. GauntletCI does not require a cloud account, never transmits code, and produces results in under one second — making it a complement to, not a replacement for, Code Climate's longitudinal reporting."
      }
    },
    {
      "@type": "Question",
      "name": "Does GauntletCI work in air-gapped or offline environments?",
      "acceptedAnswer": {
        "@type": "Answer",
        "text": "Yes. GauntletCI runs entirely on the local machine. No diff, finding, file path, or telemetry is transmitted. The optional local LLM enrichment feature (--with-llm) uses a locally hosted Ollama model — also with no network call. This makes GauntletCI suitable for classified, regulated, or air-gapped environments where cloud-based analysis tools are prohibited."
      }
    },
    {
      "@type": "Question",
      "name": "What is baseline delta mode and do other tools offer it?",
      "acceptedAnswer": {
        "@type": "Answer",
        "text": "Baseline delta mode (gauntletci baseline capture) snapshots all current findings and suppresses them from future runs. Only net-new findings — risks introduced after the baseline — are reported. This eliminates alert fatigue from pre-existing issues in legacy codebases. SonarQube offers a similar 'new code' concept but requires server setup. Semgrep, Snyk, CodeQL, and Code Climate do not offer equivalent pre-commit baseline suppression."
      }
    }
  ]
}


export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode
}>) {
  return (
    <html lang="en" className="bg-background">
      <body className="font-sans antialiased bg-background text-foreground">
        <script
          type="application/ld+json"
          dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLd) }}
        />
        <script
          type="application/ld+json"
          dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLdFaq) }}
        />
        {children}
        {process.env.NODE_ENV === 'production' && <Analytics />}
      </body>
    </html>
  )
}

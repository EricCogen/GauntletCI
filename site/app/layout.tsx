import type { Metadata } from 'next'
import { Geist, Geist_Mono } from 'next/font/google'
import './globals.css'

const organizationSchema = {
  "@context": "https://schema.org",
  "@type": "Organization",
  "name": "GauntletCI",
  "url": "https://gauntletci.com",
  "logo": "https://gauntletci.com/icon-512x512.png",
  "description": "Deterministic pre-commit change-risk detection for .NET. Sub-second analysis, runs locally, high-signal output.",
  "sameAs": [
    "https://github.com/gauntletci/gauntletci"
  ]
}

const softwareSchema = {
  "@context": "https://schema.org",
  "@type": "SoftwareApplication",
  "name": "GauntletCI",
  "applicationCategory": "DeveloperApplication",
  "operatingSystem": "Windows, macOS, Linux",
  "description": "Deterministic pre-commit change-risk detection for .NET. Sub-second analysis, runs locally, high-signal output.",
  "url": "https://gauntletci.com",
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
      "availability": "https://schema.org/InStock",
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
      "availability": "https://schema.org/InStock",
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
  metadataBase: new URL('https://gauntletci.com'),
  title: 'GauntletCI: Catch Breaking Changes Before They Merge',
  description: 'Find regressions that pass tests and code review. 100% local execution. No cloud. Sub-second diff analysis for C# and .NET.',
  alternates: { canonical: '/' },
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
      {
        url: '/icon-512x512.png',
        sizes: '512x512',
        type: 'image/png',
      },
    ],
    apple: '/apple-icon.png',
  },
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
          dangerouslySetInnerHTML={{ __html: JSON.stringify(organizationSchema) }}
        />
        <script
          type="application/ld+json"
          dangerouslySetInnerHTML={{ __html: JSON.stringify(softwareSchema) }}
        />
        {children}
      </body>
    </html>
  )
}

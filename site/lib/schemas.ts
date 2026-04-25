/**
 * Shared JSON-LD schema helpers for GauntletCI pages.
 * Multiple <script type="application/ld+json"> blocks per page are valid per spec.
 */

export const softwareApplicationSchema = {
  "@context": "https://schema.org",
  "@type": "SoftwareApplication",
  "name": "GauntletCI",
  "description":
    "Deterministic pre-commit change-risk detection for .NET. Catches behavioral regressions, breaking changes, and removed safety checks in diffs before code review.",
  "applicationCategory": "DeveloperApplication",
  "operatingSystem": "Windows, Linux, macOS",
  "offers": {
    "@type": "Offer",
    "price": "0",
    "priceCurrency": "USD",
  },
  "author": {
    "@type": "Person",
    "name": "Eric Cogen",
    "url": "https://gauntletci.com/about",
  },
  "url": "https://gauntletci.com",
  "downloadUrl": "https://www.nuget.org/packages/GauntletCI",
};

export function buildFaqSchema(faqs: Array<{ q: string; a: string }>) {
  return {
    "@context": "https://schema.org",
    "@type": "FAQPage",
    "mainEntity": faqs.map(({ q, a }) => ({
      "@type": "Question",
      "name": q,
      "acceptedAnswer": {
        "@type": "Answer",
        "text": a,
      },
    })),
  };
}

export function buildRuleFaqSchema(rule: {
  id: string;
  name: string;
  description: string;
  whyExists: string;
  severity: string;
}) {
  const severityNote =
    rule.severity === "Block"
      ? "Block rules cause GauntletCI to exit with code 1, stopping the commit or CI pipeline step."
      : rule.severity === "Warn"
      ? "Warn rules are reported but do not block the commit unless exitOn is set to Warn in .gauntletci.json."
      : "Info rules are informational and do not affect exit code.";

  return buildFaqSchema([
    {
      q: `What does the ${rule.name} rule detect?`,
      a: rule.description,
    },
    {
      q: `Why does the ${rule.name} rule exist?`,
      a: rule.whyExists,
    },
    {
      q: `What severity is ${rule.id}?`,
      a: `${rule.id} is a ${rule.severity} severity rule. ${severityNote}`,
    },
    {
      q: `How do I disable ${rule.id}?`,
      a: `Add the rule to .gauntletci.json: { "rules": { "${rule.id}": { "enabled": false } } }. You can also override the severity to Block or Warn without disabling the rule. See the Configuration docs at https://gauntletci.com/docs/configuration for the full schema.`,
    },
  ]);
}

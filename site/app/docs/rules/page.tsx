import type { Metadata } from "next";
import { rules } from "@/lib/rules";
import { softwareApplicationSchema, buildFaqSchema } from "@/lib/schemas";
import { Breadcrumbs } from "@/components/breadcrumbs";
import { RuleExplorer } from "./rule-explorer";

export const metadata: Metadata = {
  title: "Rule Library | GauntletCI Docs",
  description:
    "36 documented detection rules (34 active by default) for behavioral regressions, security risks, breaking changes, and code quality issues in C# and .NET pull request diffs.",
  alternates: { canonical: "/docs/rules" },
};

const totalRules = rules.length;

const jsonLd = {
  "@context": "https://schema.org",
  "@type": "ItemList",
  name: "GauntletCI Detection Rules",
  description:
    "34 deterministic rules — and growing — for detecting behavioral regressions, security risks, breaking changes, and code quality issues in C# .NET diffs.",
  url: "https://gauntletci.com/docs/rules",
  numberOfItems: totalRules,
  itemListElement: rules.map((rule, idx) => ({
    "@type": "ListItem",
    position: idx + 1,
    name: `${rule.id} ${rule.name}`,
    url: `https://gauntletci.com/docs/rules/${rule.id}`,
    description: rule.description,
  })),
};

const faqSchema = buildFaqSchema([
  {
    q: "How many detection rules does GauntletCI have?",
    a: "GauntletCI includes 36 documented detection rules; 34 run by default (GCI0054 and GCI0055 are disabled unless re-enabled). Coverage includes behavioral regressions, breaking API changes, security risks, test gaps, and architecture violations in .NET diffs.",
  },
  {
    q: "What categories of rules does GauntletCI have?",
    a: "Rules are organized into categories including Behavioral Changes, Breaking API Changes, Security Risks, Test Coverage, and Architecture. Each rule targets a specific class of risk in your diff.",
  },
  {
    q: "Can I disable specific rules?",
    a: 'Yes. Add the rule ID to .gauntletci.json to disable it: { "rules": { "GCI0001": { "enabled": false } } }. All rules are enabled by default.',
  },
  {
    q: "What does a Block severity rule mean?",
    a: "A Block severity rule causes GauntletCI to exit with code 1 when the finding is detected, stopping the commit or failing the CI pipeline step. Warn severity rules are reported but do not block by default.",
  },
  {
    q: "Do rules send my code to a server?",
    a: "No. All rules run locally and deterministically. No code, diff content, or findings are transmitted to any external service.",
  },
]);

export default function RulesPage() {
  return (
    <>
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLd) }}
      />
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{ __html: JSON.stringify(softwareApplicationSchema) }}
      />
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{ __html: JSON.stringify(faqSchema) }}
      />
      <div className="space-y-10">
        <Breadcrumbs />
        <div>
          <p className="text-sm font-semibold text-cyan-400 uppercase tracking-widest mb-3">
            Rule Library
          </p>
          <h1 className="text-4xl font-bold tracking-tight mb-4">
            {totalRules} deterministic detection rules — and growing
          </h1>
          <p className="text-lg text-muted-foreground">
            Every rule targets a specific class of behavioral, security, or
            structural risk in your diff. Rules run locally; typical staged diffs complete in seconds.
            Core detection does not send code to external services unless you enable optional integrations.
          </p>
        </div>

        <RuleExplorer />

        <div className="border-t border-border pt-6">
          <p className="text-sm text-muted-foreground">
            Rules are implemented in{" "}
            <code className="bg-muted px-1 rounded text-xs">
              GauntletCI.Core/Rules/Implementations/
            </code>
            . All rules are enabled by default and can be individually disabled
            or reconfigured in{" "}
            <code className="bg-muted px-1 rounded text-xs">
              .gauntletci.json
            </code>
            . See the{" "}
            <a
              href="/docs/configuration"
              className="text-cyan-400 hover:underline"
            >
              Configuration docs
            </a>{" "}
            for details.
          </p>
        </div>
      </div>
    </>
  );
}

import type { Metadata } from "next";
import Link from "next/link";
import { categories, rules, rulesByCategory, type Rule } from "@/lib/rules";

export const metadata: Metadata = {
  title: "Rule Library | GauntletCI Docs",
  description:
    "30+ deterministic rules for detecting behavioral regressions, security risks, breaking changes, and code quality issues in C# and .NET pull request diffs.",
  alternates: { canonical: "/docs/rules" },
};

const totalRules = rules.length;

const jsonLd = {
  "@context": "https://schema.org",
  "@type": "ItemList",
  name: "GauntletCI Detection Rules",
  description:
    "30+ deterministic rules for detecting behavioral regressions, security risks, breaking changes, and code quality issues in C# .NET diffs.",
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

function SeverityBadge({ severity }: { severity: Rule["severity"] }) {
  const styles = {
    Block: "bg-red-500/10 text-red-400 ring-red-400/20",
    Warn: "bg-yellow-500/10 text-yellow-400 ring-yellow-400/20",
    Info: "bg-muted text-muted-foreground ring-border",
  };
  return (
    <span
      className={`inline-flex items-center rounded-md px-2 py-0.5 text-xs font-medium ring-1 ring-inset ${styles[severity]}`}
    >
      {severity}
    </span>
  );
}

export default function RulesPage() {
  return (
    <>
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLd) }}
      />
      <div className="space-y-10">
        <div>
          <p className="text-sm font-semibold text-cyan-400 uppercase tracking-widest mb-3">
            Rule Library
          </p>
          <h1 className="text-4xl font-bold tracking-tight mb-4">
            {totalRules} deterministic detection rules
          </h1>
          <p className="text-lg text-muted-foreground">
            Every rule targets a specific class of behavioral, security, or
            structural risk in your diff. Rules run locally in under one second.
            No rule sends code to any external service.
          </p>
          <div className="mt-6 flex flex-wrap gap-2">
            {categories.map((cat) => (
              <a
                key={cat.slug}
                href={`#${cat.slug}`}
                className="inline-flex items-center gap-1.5 rounded-full border border-border px-3 py-1 text-xs font-medium text-muted-foreground hover:text-foreground hover:border-foreground/40 transition-colors"
              >
                <cat.icon className={`h-3 w-3 ${cat.color}`} />
                {cat.title}
                <span className="ml-0.5 text-muted-foreground/60">
                  ({rulesByCategory(cat.slug).length})
                </span>
              </a>
            ))}
          </div>
        </div>

        {categories.map((cat) => {
          const catRules = rulesByCategory(cat.slug);
          if (catRules.length === 0) return null;
          return (
            <section key={cat.slug} id={cat.slug} className="pt-4">
              <div className="flex items-center gap-3 mb-1">
                <cat.icon className={`h-5 w-5 ${cat.color}`} />
                <h2 className="text-xl font-bold tracking-tight">{cat.title}</h2>
              </div>
              <p className="text-sm text-muted-foreground mb-5">{cat.tagline}</p>
              <div className="grid gap-3 sm:grid-cols-2">
                {catRules.map((rule) => (
                  <Link
                    key={rule.id}
                    href={`/docs/rules/${rule.id}`}
                    id={rule.id}
                    className="block rounded-xl border border-border bg-card p-4 hover:border-cyan-500/40 hover:bg-card/80 transition-colors"
                  >
                    <div className="flex items-start justify-between gap-3 mb-3">
                      <span
                        className={`inline-flex items-center rounded-md px-2 py-0.5 text-xs font-mono font-semibold ring-1 ring-inset ${cat.badgeColor}`}
                      >
                        {rule.id}
                      </span>
                      <SeverityBadge severity={rule.severity} />
                    </div>
                    <h3 className="text-sm font-semibold text-foreground mb-1.5">
                      {rule.name}
                    </h3>
                    <p className="text-xs text-muted-foreground leading-relaxed">
                      {rule.description}
                    </p>
                  </Link>
                ))}
              </div>
            </section>
          );
        })}

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

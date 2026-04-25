import type { Metadata } from "next";
import Link from "next/link";
import { notFound } from "next/navigation";
import { ArrowLeft } from "lucide-react";
import {
  getCategory,
  getRule,
  rules,
  type Rule,
} from "@/lib/rules";
import { articlesForRule } from "@/lib/articles";

type Params = { ruleId: string };

export function generateStaticParams(): Params[] {
  return rules.map((r) => ({ ruleId: r.id }));
}

export const dynamicParams = false;

export async function generateMetadata({
  params,
}: {
  params: Promise<Params>;
}): Promise<Metadata> {
  const { ruleId } = await params;
  const rule = getRule(ruleId);
  if (!rule) {
    return {
      title: "Rule not found | GauntletCI",
      robots: { index: false, follow: false },
    };
  }
  const title = `${rule.id} ${rule.name} | GauntletCI Rule`;
  const description = rule.description;
  const url = `/docs/rules/${rule.id}`;
  return {
    title,
    description,
    alternates: { canonical: url },
    openGraph: {
      title,
      description,
      url,
      type: "article",
    },
    twitter: {
      card: "summary_large_image",
      title,
      description,
    },
  };
}

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

export default async function RuleDetailPage({
  params,
}: {
  params: Promise<Params>;
}) {
  const { ruleId } = await params;
  const rule = getRule(ruleId);
  if (!rule) notFound();

  const category = getCategory(rule.categorySlug);
  const related = (rule.relatedIds ?? [])
    .map((id) => getRule(id))
    .filter((r): r is Rule => Boolean(r));
  const discussedIn = articlesForRule(rule.id);

  const jsonLd = {
    "@context": "https://schema.org",
    "@type": "TechArticle",
    headline: `${rule.id}: ${rule.name}`,
    description: rule.description,
    url: `https://gauntletci.com/docs/rules/${rule.id}`,
    inLanguage: "en",
    isPartOf: {
      "@type": "TechArticle",
      name: "GauntletCI Rule Library",
      url: "https://gauntletci.com/docs/rules",
    },
    about: category.title,
    keywords: [
      rule.id,
      rule.name,
      category.title,
      ".NET",
      "C#",
      "static analysis",
      "code review",
    ],
  };

  return (
    <>
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLd) }}
      />
      <div className="space-y-8">
        <nav className="text-sm text-muted-foreground">
          <Link href="/docs" className="hover:text-foreground">
            Docs
          </Link>
          <span className="mx-2">/</span>
          <Link href="/docs/rules" className="hover:text-foreground">
            Rules
          </Link>
          <span className="mx-2">/</span>
          <span className="text-foreground">{rule.id}</span>
        </nav>

        <div>
          <Link
            href="/docs/rules"
            className="inline-flex items-center gap-1.5 text-xs text-muted-foreground hover:text-foreground mb-4"
          >
            <ArrowLeft className="h-3 w-3" />
            All rules
          </Link>
          <div className="flex flex-wrap items-center gap-3 mb-3">
            <span
              className={`inline-flex items-center rounded-md px-2 py-0.5 text-xs font-mono font-semibold ring-1 ring-inset ${category.badgeColor}`}
            >
              {rule.id}
            </span>
            <SeverityBadge severity={rule.severity} />
            <Link
              href={`/docs/rules#${category.slug}`}
              className="inline-flex items-center gap-1.5 text-xs text-muted-foreground hover:text-foreground"
            >
              <category.icon className={`h-3 w-3 ${category.color}`} />
              {category.title}
            </Link>
          </div>
          <h1 className="text-4xl font-bold tracking-tight mb-3">
            {rule.name}
          </h1>
          <p className="text-lg text-muted-foreground">{rule.description}</p>
        </div>

        <section>
          <h2 className="text-xl font-bold tracking-tight mb-3">
            Why this rule exists
          </h2>
          <p className="text-base text-muted-foreground leading-relaxed">
            {rule.whyExists}
          </p>
        </section>

        <section>
          <h2 className="text-xl font-bold tracking-tight mb-3">
            Code example
          </h2>
          <div className="grid gap-4 md:grid-cols-2">
            <div className="rounded-xl border border-red-400/20 bg-red-500/5 p-4">
              <div className="text-xs font-semibold text-red-400 uppercase tracking-widest mb-2">
                Triggers the rule
              </div>
              <pre className="text-xs leading-relaxed overflow-x-auto">
                <code>{rule.example.bad}</code>
              </pre>
            </div>
            <div className="rounded-xl border border-green-400/20 bg-green-500/5 p-4">
              <div className="text-xs font-semibold text-green-400 uppercase tracking-widest mb-2">
                Passes the rule
              </div>
              <pre className="text-xs leading-relaxed overflow-x-auto">
                <code>{rule.example.good}</code>
              </pre>
            </div>
          </div>
        </section>

        <section>
          <h2 className="text-xl font-bold tracking-tight mb-3">
            Configuration
          </h2>
          <p className="text-sm text-muted-foreground mb-3">
            Disable or adjust the severity of this rule in{" "}
            <code className="bg-muted px-1 rounded text-xs">
              .gauntletci.json
            </code>
            :
          </p>
          <pre className="rounded-xl border border-border bg-card p-4 text-xs overflow-x-auto">
            <code>{`{
  "rules": {
    "${rule.id}": { "enabled": true, "severity": "${rule.severity}" }
  }
}`}</code>
          </pre>
          <p className="text-xs text-muted-foreground mt-3">
            See{" "}
            <Link
              href="/docs/configuration"
              className="text-cyan-400 hover:underline"
            >
              Configuration
            </Link>{" "}
            for the full schema.
          </p>
        </section>

        {related.length > 0 && (
          <section>
            <h2 className="text-xl font-bold tracking-tight mb-3">
              Related rules
            </h2>
            <div className="grid gap-3 sm:grid-cols-2">
              {related.map((r) => {
                const cat = getCategory(r.categorySlug);
                return (
                  <Link
                    key={r.id}
                    href={`/docs/rules/${r.id}`}
                    className="block rounded-xl border border-border bg-card p-4 hover:border-cyan-500/40 hover:bg-card/80 transition-colors"
                  >
                    <div className="flex items-start justify-between gap-3 mb-3">
                      <span
                        className={`inline-flex items-center rounded-md px-2 py-0.5 text-xs font-mono font-semibold ring-1 ring-inset ${cat.badgeColor}`}
                      >
                        {r.id}
                      </span>
                      <SeverityBadge severity={r.severity} />
                    </div>
                    <h3 className="text-sm font-semibold text-foreground mb-1.5">
                      {r.name}
                    </h3>
                    <p className="text-xs text-muted-foreground leading-relaxed">
                      {r.description}
                    </p>
                  </Link>
                );
              })}
            </div>
          </section>
        )}

        {discussedIn.length > 0 && (
          <section>
            <h2 className="text-xl font-bold tracking-tight mb-3">
              Discussed in
            </h2>
            <div className="grid gap-3 sm:grid-cols-2">
              {discussedIn.map((article) => (
                <Link
                  key={article.slug}
                  href={article.href}
                  className="block rounded-xl border border-border bg-card p-4 hover:border-cyan-500/40 hover:bg-card/80 transition-colors"
                >
                  <h3 className="text-sm font-semibold text-foreground mb-1.5">
                    {article.title}
                  </h3>
                  <p className="text-xs text-muted-foreground leading-relaxed">
                    {article.description}
                  </p>
                </Link>
              ))}
            </div>
          </section>
        )}

        <div className="border-t border-border pt-6">
          <p className="text-sm text-muted-foreground">
            Implemented in{" "}
            <code className="bg-muted px-1 rounded text-xs">
              src/GauntletCI.Core/Rules/Implementations/{rule.id}_*.cs
            </code>
            .
          </p>
        </div>
      </div>
    </>
  );
}

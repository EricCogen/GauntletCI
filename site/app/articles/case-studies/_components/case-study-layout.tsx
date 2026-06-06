import type { ReactNode } from "react";
import Link from "next/link";
import JsonLd from "@/components/json-ld";
import { Header } from "@/components/header";
import { Footer } from "@/components/footer";
import { RulesApplied } from "@/components/rules-applied";
import { AuthorBio } from "@/components/author-bio";

type DiffLineType = "added" | "removed" | "context";
type Tone = "red" | "yellow" | "cyan" | "green";

export type CaseStudySection = {
  title: string;
  children: ReactNode;
};

export type CaseStudyDiffLine = {
  type: DiffLineType;
  line: string;
};

export type CaseStudyStat = {
  label: string;
  value: string;
};

export type CaseStudyLayoutProps = {
  title: string;
  description: string;
  canonicalPath: string;
  repo: string;
  pr: string;
  prUrl: string;
  datePublished?: string;
  outcomeLabel: string;
  outcomeTone: Tone;
  tags: string[];
  ruleIds: string[];
  stats: CaseStudyStat[];
  sections: CaseStudySection[];
  diffTitle: string;
  diffFile: string;
  diffLines: readonly CaseStudyDiffLine[];
  findingTitle: string;
  findingBody: string;
  caveats: string[];
  nextActions: string[];
  sources: { label: string; href: string }[];
};

const lineColor: Record<DiffLineType, string> = {
  added: "bg-green-500/10 text-green-300",
  removed: "bg-red-500/10 text-red-300",
  context: "text-muted-foreground/60",
};

const linePrefix: Record<DiffLineType, string> = {
  added: "+",
  removed: "-",
  context: " ",
};

const toneClasses: Record<Tone, string> = {
  red: "text-red-400 bg-red-500/10 border-red-500/20",
  yellow: "text-yellow-300 bg-yellow-500/10 border-yellow-500/20",
  cyan: "text-cyan-400 bg-cyan-500/10 border-cyan-500/20",
  green: "text-emerald-400 bg-emerald-500/10 border-emerald-500/20",
};

export function CaseStudyLayout({
  title,
  description,
  canonicalPath,
  repo,
  pr,
  prUrl,
  datePublished = "2025-05-03",
  outcomeLabel,
  outcomeTone,
  tags,
  ruleIds,
  stats,
  sections,
  diffTitle,
  diffFile,
  diffLines,
  findingTitle,
  findingBody,
  caveats,
  nextActions,
  sources,
}: CaseStudyLayoutProps) {
  const jsonLd = {
    "@context": "https://schema.org",
    "@type": "Article",
    headline: title,
    description,
    url: `https://gauntletci.com${canonicalPath}`,
    author: { "@type": "Organization", name: "GauntletCI" },
    publisher: { "@type": "Organization", name: "GauntletCI", url: "https://gauntletci.com" },
    datePublished,
  };

  return (
    <>
      <JsonLd data={jsonLd} />
      <Header />
      <main className="min-h-screen bg-background pt-24">
        <div className="mx-auto max-w-4xl px-4 sm:px-6 lg:px-8 py-16 sm:py-20 space-y-16">
          <div className="space-y-5 border-b border-border pb-12">
            <div className="flex items-center justify-between gap-4">
              <p className="text-sm font-semibold uppercase tracking-widest text-cyan-400">
                Case Study
              </p>
              <Link
                href="/articles/case-studies"
                className="text-sm text-muted-foreground hover:text-cyan-400 transition-colors"
              >
                ← All case studies
              </Link>
            </div>

            <h1 className="text-4xl sm:text-5xl font-bold tracking-tight text-balance">{title}</h1>
            <p className="text-xl text-muted-foreground leading-relaxed text-pretty">{description}</p>

            <div className="flex flex-wrap items-center gap-3">
              <span className="font-mono text-sm text-muted-foreground">{repo}</span>
              <a
                href={prUrl}
                target="_blank"
                rel="noopener noreferrer"
                className="font-mono text-sm text-cyan-400 hover:text-cyan-300 transition-colors"
              >
                {pr} ↗
              </a>
            </div>

            <div className="flex flex-wrap gap-2">
              {ruleIds.map((rule) => (
                <Link
                  key={rule}
                  href={`/docs/rules/${rule}`}
                  className="font-mono text-xs font-medium px-2 py-0.5 rounded bg-cyan-500/10 text-cyan-400 border border-cyan-500/20 hover:bg-cyan-500/20 transition-colors"
                >
                  {rule}
                </Link>
              ))}
              <span className={`text-xs font-semibold border px-2 py-0.5 rounded-full ${toneClasses[outcomeTone]}`}>
                {outcomeLabel}
              </span>
              {tags.map((tag) => (
                <span key={tag} className="text-xs font-medium px-2 py-0.5 rounded-full bg-secondary text-muted-foreground">
                  {tag}
                </span>
              ))}
            </div>

            <div className="grid gap-3 sm:grid-cols-3">
              {stats.map((stat) => (
                <div key={stat.label} className="rounded-xl border border-border bg-card/40 p-4">
                  <p className="text-2xl font-bold text-cyan-400">{stat.value}</p>
                  <p className="mt-1 text-xs text-muted-foreground">{stat.label}</p>
                </div>
              ))}
            </div>
          </div>

          {sections.map((section) => (
            <section key={section.title} className="space-y-4">
              <h2 className="text-2xl font-bold tracking-tight">{section.title}</h2>
              <div className="space-y-4 text-muted-foreground leading-relaxed">{section.children}</div>
            </section>
          ))}

          <section className="space-y-4">
            <h2 className="text-2xl font-bold tracking-tight">{diffTitle}</h2>
            <div className="rounded-xl border border-border overflow-hidden">
              <div className="border-b border-border bg-card/60 px-4 py-2 flex items-center gap-2">
                <div className="flex gap-1.5">
                  <div className="w-2.5 h-2.5 rounded-full bg-red-500/40" />
                  <div className="w-2.5 h-2.5 rounded-full bg-amber-500/40" />
                  <div className="w-2.5 h-2.5 rounded-full bg-green-500/40" />
                </div>
                <span className="text-xs font-mono text-muted-foreground/40 ml-1">{diffFile}</span>
              </div>
              <div className="overflow-x-auto p-4 font-mono text-xs leading-relaxed space-y-0.5 bg-background/50">
                {diffLines.map((line, i) => (
                  <div key={`${i}-${line.line}`} className={`flex min-w-max gap-2 px-2 py-0.5 rounded ${lineColor[line.type]}`}>
                    <span className="shrink-0 w-3 select-none">{linePrefix[line.type]}</span>
                    <span className="whitespace-pre">{line.line}</span>
                  </div>
                ))}
              </div>
              <div className={`border-t px-4 py-3 ${toneClasses[outcomeTone]}`}>
                <p className="text-xs font-semibold uppercase tracking-widest">{findingTitle}</p>
                <pre className="mt-2 text-xs font-mono leading-relaxed whitespace-pre-wrap">{findingBody}</pre>
              </div>
            </div>
          </section>

          <section className="grid gap-6 md:grid-cols-2">
            <div className="rounded-xl border border-border bg-card/40 p-6 space-y-4">
              <h2 className="text-xl font-bold tracking-tight">Important context</h2>
              <ul className="space-y-3 text-sm text-muted-foreground leading-relaxed">
                {caveats.map((item) => (
                  <li key={item} className="flex gap-2">
                    <span className="mt-1.5 h-1.5 w-1.5 shrink-0 rounded-full bg-yellow-400" />
                    <span>{item}</span>
                  </li>
                ))}
              </ul>
            </div>

            <div className="rounded-xl border border-border bg-card/40 p-6 space-y-4">
              <h2 className="text-xl font-bold tracking-tight">Recommended review steps</h2>
              <ul className="space-y-3 text-sm text-muted-foreground leading-relaxed">
                {nextActions.map((item) => (
                  <li key={item} className="flex gap-2">
                    <span className="mt-1.5 h-1.5 w-1.5 shrink-0 rounded-full bg-cyan-400" />
                    <span>{item}</span>
                  </li>
                ))}
              </ul>
            </div>
          </section>

          <section className="border-t border-border pt-10 space-y-4">
            <h2 className="text-lg font-semibold">Sources</h2>
            <div className="flex flex-wrap gap-3">
              {sources.map((source) => (
                <a
                  key={source.href}
                  href={source.href}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="rounded-lg border border-border bg-card px-4 py-2 text-sm font-medium text-cyan-400 hover:bg-card/80 transition-colors"
                >
                  {source.label} ↗
                </a>
              ))}
            </div>
          </section>

          <div className="border-t border-border pt-10 flex flex-col sm:flex-row gap-4">
            <Link
              href="/articles/case-studies"
              className="inline-flex items-center gap-2 rounded-lg border border-border bg-card px-6 py-3 text-sm font-semibold hover:bg-card/80 transition-colors"
            >
              ← All case studies
            </Link>
            <Link
              href="/docs/rules"
              className="inline-flex items-center justify-center gap-2 rounded-lg bg-cyan-500 px-6 py-3 text-sm font-semibold text-background hover:bg-cyan-400 transition-colors"
            >
              View detection rules
            </Link>
          </div>

          {ruleIds.length > 0 ? <RulesApplied ids={ruleIds} /> : null}
          <AuthorBio variant="long" />
        </div>
      </main>
      <Footer />
    </>
  );
}

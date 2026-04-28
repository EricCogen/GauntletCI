import type { Metadata } from "next";
import Link from "next/link";
import { Header } from "@/components/header";
import { Footer } from "@/components/footer";

export const metadata: Metadata = {
  title: "Releases | GauntletCI",
  description:
    "GauntletCI version history. See what changed in each release - new detection rules, CLI improvements, and precision hardening.",
  alternates: { canonical: "/releases" },
};

type Entry = { text: string; link?: string };
type Section = { label: "Added" | "Changed" | "Fixed"; entries: Entry[] };
type Release = {
  version: string;
  date: string;
  tag: string;
  summary: string;
  sections: Section[];
  compareUrl?: string;
};

const releases: Release[] = [
  {
    version: "2.1.1",
    date: "2026-04-28",
    tag: "latest",
    summary: "NuGet README improvements and packaging cleanup.",
    compareUrl: "https://github.com/EricCogen/GauntletCI/compare/v2.0.4...HEAD",
    sections: [
      {
        label: "Changed",
        entries: [
          {
            text: "NuGet package now uses a dedicated nuget-readme.md with plain markdown and absolute image URLs for correct rendering on NuGet.org.",
          },
          {
            text: "Resized NuGet logo (200x262) added as GauntletCI-nuget.png.",
          },
          {
            text: "30 precision-hardened detection rules: per-rule corpus validation against 618 real .NET OSS pull requests. Rules GCI0003-GCI0047 updated with tighter guards and labeler alignment.",
          },
          {
            text: "action.yml marketplace readiness: input defaults corrected, gauntletci-version bumped to 2.1.0.",
          },
        ],
      },
      {
        label: "Added",
        entries: [
          {
            text: "GitLab CI and Bitbucket Pipelines integration snippets in the docs.",
            link: "/docs/integrations",
          },
          {
            text: "Per-rule detail pages at /docs/rules with real-world case study links.",
            link: "/docs/rules",
          },
        ],
      },
    ],
  },
  {
    version: "2.0.4",
    date: "2026-04-25",
    tag: "",
    summary: "Site infrastructure: search, rule detail pages, E2E tests, and author attribution.",
    compareUrl: "https://github.com/EricCogen/GauntletCI/compare/v2.0.3...v2.0.4",
    sections: [
      {
        label: "Added",
        entries: [
          { text: "Full-text search with Cmd/Ctrl+K shortcut across all 53 pages." },
          { text: "30 per-rule detail pages at /docs/rules/[ruleId].", link: "/docs/rules" },
          { text: "/about page with founder bio and E-E-A-T author attribution.", link: "/about" },
          { text: "Playwright E2E test suite with smoke, article, rule detail, and link-graph tests." },
          { text: "JSON-LD schemas on all docs and rule detail pages." },
        ],
      },
      {
        label: "Changed",
        entries: [
          { text: "Header nav simplified to 3 items: Product dropdown, Docs, About." },
        ],
      },
      {
        label: "Fixed",
        entries: [
          { text: "/docs pages had zero outbound content links - link-graph test now catches this." },
          { text: "/pricing had no inbound content links from any other page." },
        ],
      },
    ],
  },
  {
    version: "2.0.3",
    date: "2026-04-24",
    tag: "",
    summary: "Rich PR review summaries with Why, Action, and Evidence sections.",
    compareUrl: "https://github.com/EricCogen/GauntletCI/compare/v2.0.2...v2.0.3",
    sections: [
      {
        label: "Added",
        entries: [
          {
            text: "GitHub PR review comments now include Why, Action, and Evidence sections in collapsible details blocks.",
          },
          {
            text: "--with-llm enrichment attaches plain-English explanations to high-confidence findings.",
          },
        ],
      },
    ],
  },
  {
    version: "2.0.2",
    date: "2026-04-24",
    tag: "",
    summary: "Finding grouping and structured GitHub Actions output.",
    compareUrl: "https://github.com/EricCogen/GauntletCI/compare/v2.0.1...v2.0.2",
    sections: [
      {
        label: "Added",
        entries: [
          {
            text: "Duplicate findings across multiple files are collapsed into a single annotated entry.",
          },
          {
            text: "Structured Markdown output for GitHub Actions annotations and Checks summaries.",
          },
        ],
      },
    ],
  },
  {
    version: "2.0.1",
    date: "2026-04-24",
    tag: "",
    summary: "Demo links and footer fixes.",
    compareUrl: "https://github.com/EricCogen/GauntletCI/compare/v2.0.0...v2.0.1",
    sections: [
      {
        label: "Added",
        entries: [
          {
            text: "GauntletCI-Demo links in header, footer, and README.",
            link: "https://github.com/EricCogen/GauntletCI-Demo",
          },
          { text: "Footer added to all /docs pages." },
        ],
      },
      {
        label: "Fixed",
        entries: [{ text: "Footer anchor links now use / prefix for correct cross-page navigation." }],
      },
    ],
  },
  {
    version: "2.0.0",
    date: "2026-04-14",
    tag: "initial",
    summary: "Initial public release. 30 detection rules, local LLM support, MCP server, GitHub Actions integration.",
    compareUrl: "https://github.com/EricCogen/GauntletCI/releases/tag/v2.0.0",
    sections: [
      {
        label: "Added",
        entries: [
          { text: "30 built-in deterministic detection rules (GCI0001-GCI0050)." },
          { text: "Local LLM enrichment via Ollama - no data leaves your machine." },
          { text: "MCP server for AI assistant integration." },
          { text: "GitHub Actions integration with inline PR comments and Checks annotations." },
          { text: "Baseline mode: suppress pre-existing findings, surface only new risks." },
          { text: "NuGet packaging and publish workflow." },
        ],
      },
      {
        label: "Fixed",
        entries: [
          {
            text: "Culture-invariant percent formatting in MarkdownReportExporter (non-en-US locales).",
          },
        ],
      },
    ],
  },
];

const sectionColors: Record<string, string> = {
  Added: "text-emerald-400 border-emerald-500/30 bg-emerald-500/10",
  Changed: "text-blue-400 border-blue-500/30 bg-blue-500/10",
  Fixed: "text-amber-400 border-amber-500/30 bg-amber-500/10",
};

export default function ReleasesPage() {
  return (
    <>
      <Header />
      <main className="min-h-screen bg-background pt-24">
        <div className="mx-auto max-w-3xl px-4 sm:px-6 lg:px-8 py-12">
          <Link href="/" className="text-sm text-muted-foreground hover:text-foreground">
            &larr; Back home
          </Link>

          <h1 className="mt-6 text-4xl font-bold tracking-tight">Releases</h1>
          <p className="mt-3 text-lg text-muted-foreground">
            What changed in each version of GauntletCI.
          </p>

          <p className="mt-2 text-sm text-muted-foreground">
            Full technical changelog:{" "}
            <Link
              href="https://github.com/EricCogen/GauntletCI/blob/main/CHANGELOG.md"
              target="_blank"
              rel="noopener noreferrer"
              className="text-cyan-400 hover:underline"
            >
              CHANGELOG.md on GitHub
            </Link>
          </p>

          <div className="mt-12 space-y-12">
            {releases.map((release) => (
              <div key={release.version} className="relative pl-6 border-l border-border">
                <div className="absolute -left-2.5 top-1 h-5 w-5 rounded-full border border-border bg-background flex items-center justify-center">
                  <div className="h-2 w-2 rounded-full bg-cyan-400" />
                </div>

                <div className="flex flex-wrap items-center gap-3 mb-3">
                  <h2 className="text-xl font-bold text-foreground">v{release.version}</h2>
                  {release.tag === "latest" && (
                    <span className="text-xs font-medium rounded-full px-2.5 py-0.5 border border-cyan-500/30 bg-cyan-500/10 text-cyan-400">
                      latest
                    </span>
                  )}
                  {release.tag === "initial" && (
                    <span className="text-xs font-medium rounded-full px-2.5 py-0.5 border border-violet-500/30 bg-violet-500/10 text-violet-400">
                      initial release
                    </span>
                  )}
                  <span className="text-sm text-muted-foreground">{release.date}</span>
                  {release.compareUrl && (
                    <Link
                      href={release.compareUrl}
                      target="_blank"
                      rel="noopener noreferrer"
                      className="text-xs text-muted-foreground hover:text-foreground"
                    >
                      diff &rarr;
                    </Link>
                  )}
                </div>

                <p className="text-muted-foreground text-sm mb-5">{release.summary}</p>

                <div className="space-y-4">
                  {release.sections.map((section) => (
                    <div key={section.label}>
                      <span
                        className={`inline-block text-xs font-medium rounded-full px-2.5 py-0.5 border mb-3 ${sectionColors[section.label]}`}
                      >
                        {section.label}
                      </span>
                      <ul className="space-y-2">
                        {section.entries.map((entry, i) => (
                          <li key={i} className="flex items-start gap-2 text-sm text-muted-foreground">
                            <span className="mt-1.5 h-1.5 w-1.5 rounded-full bg-muted-foreground/40 shrink-0" />
                            {entry.link ? (
                              <span>
                                {entry.text.split(".")[0]}.{" "}
                                <Link
                                  href={entry.link}
                                  className="text-cyan-400 hover:underline"
                                  {...(entry.link.startsWith("http")
                                    ? { target: "_blank", rel: "noopener noreferrer" }
                                    : {})}
                                >
                                  View &rarr;
                                </Link>
                              </span>
                            ) : (
                              <span>{entry.text}</span>
                            )}
                          </li>
                        ))}
                      </ul>
                    </div>
                  ))}
                </div>
              </div>
            ))}
          </div>

          <div className="mt-16 pt-8 border-t border-border">
            <p className="text-sm text-muted-foreground">
              Install the latest version:{" "}
              <code className="text-foreground/80 bg-muted px-1.5 py-0.5 rounded text-xs">
                dotnet tool install -g GauntletCI
              </code>
            </p>
          </div>
        </div>
      </main>
      <Footer />
    </>
  );
}

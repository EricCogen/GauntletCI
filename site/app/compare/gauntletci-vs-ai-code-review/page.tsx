import type { Metadata } from "next";
import Link from "next/link";
import { Header } from "@/components/header";
import { Footer } from "@/components/footer";

export const metadata: Metadata = {
  title: "GauntletCI vs AI Code Review | Behavioral Change Risk vs Review Suggestions",
  description:
    "AI code review tools validate intent and style. GauntletCI detects Behavioral Change Risk in the diff -- identifying logic shifts and hidden regressions that pass tests and look correct to a reviewer.",
  alternates: { canonical: "/compare/gauntletci-vs-ai-code-review" },
  openGraph: { images: [{ url: '/og/compare-ai-code-review.png', width: 1200, height: 630 }] },
};

const whatEachChecks = [
  {
    tool: "GitHub Copilot",
    checks: "Code style, readability, obvious bugs, intent alignment",
    misses: "Behavioral change risk, deleted guards, runtime logic shifts",
  },
  {
    tool: "Amazon CodeGuru",
    checks: "Code quality, security patterns, resource use",
    misses: "Diff-scoped behavioral risk, exception path changes",
  },
  {
    tool: "Cursor / Codeium",
    checks: "Inline suggestions, autocomplete, chat-driven refactors",
    misses: "Whether the behavior of the changed lines is still safe",
  },
  {
    tool: "GauntletCI",
    checks: "Change safety, Behavioral Change Risk, logic shifts in the diff",
    misses: "--",
  },
];

const featureRows = [
  {
    label: "Primary focus",
    gauntlet: "Behavioral risk in the current diff",
    ai: "Code style, readability, and intent",
  },
  {
    label: "Analysis scope",
    gauntlet: "Changed diff lines only",
    ai: "Full file or PR -- not scoped to behavioral delta",
  },
  {
    label: "Execution model",
    gauntlet: "Deterministic rules, same result every run",
    ai: "LLM inference -- non-deterministic, probabilistic output",
  },
  {
    label: "Data leaves the machine",
    gauntlet: "Never -- 100% local execution",
    ai: "Yes -- code sent to cloud LLM API",
  },
  {
    label: "When it runs",
    gauntlet: "Pre-commit, before the push",
    ai: "Post-push in the PR, after code leaves the machine",
  },
  {
    label: "Pre-commit speed",
    gauntlet: "Under 1 second",
    ai: "Not designed for pre-commit use",
  },
  {
    label: "Air-gap / data residency",
    gauntlet: "Yes -- no network dependency",
    ai: "No -- requires internet and API key",
  },
  {
    label: "Cost per run",
    gauntlet: "Free, no token cost",
    ai: "API token cost per review",
  },
  {
    label: "Catches deleted null guards",
    gauntlet: "Yes -- diff-scoped rule fires on guard removal",
    ai: "Unlikely -- the remaining code still looks correct",
  },
  {
    label: "Catches silent exception path change",
    gauntlet: "Yes",
    ai: "Unlikely -- no runtime execution model",
  },
  {
    label: "Catches API contract break",
    gauntlet: "Yes -- method signature rules",
    ai: "Partial -- may comment on it, not guaranteed",
  },
  {
    label: "MCP server integration",
    gauntlet: "Yes -- AI tools call GauntletCI directly",
    ai: "N/A",
  },
  {
    label: "Custom rules",
    gauntlet: "Yes -- implement IRule in C#",
    ai: "No -- not extensible",
  },
  {
    label: "Free for open source",
    gauntlet: "Yes, all rules",
    ai: "Varies by provider",
  },
];

const jsonLd = {
  "@context": "https://schema.org",
  "@type": "Article",
  "headline": "GauntletCI vs AI Code Review: Behavioral Change Risk Detection vs Review Suggestions",
  "description": "AI code review tools validate intent and style. GauntletCI detects Behavioral Change Risk in the diff -- identifying logic shifts and hidden regressions that pass tests and look correct to a reviewer.",
  "url": "https://gauntletci.com/compare/gauntletci-vs-ai-code-review",
  "publisher": { "@type": "Organization", "name": "GauntletCI", "url": "https://gauntletci.com" },
};

export default function AiCodeReviewComparePage() {
  return (
    <>
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLd) }} />
      <Header />
      <div className="min-h-screen bg-background">
      <div className="mx-auto max-w-5xl px-4 sm:px-6 lg:px-8 py-20 sm:py-28 space-y-20">

        {/* Hero */}
        <div className="text-center space-y-5">
          <p className="text-sm font-semibold uppercase tracking-widest text-cyan-400">Compare</p>
          <h1 className="text-4xl sm:text-5xl font-bold tracking-tight text-balance">
            GauntletCI vs AI Code Review
          </h1>
          <p className="text-xl text-muted-foreground max-w-2xl mx-auto text-pretty">
            AI code review tools read your code and suggest improvements.
            GauntletCI analyzes what changed and flags Behavioral Change Risk.
            They answer different questions at different moments.
          </p>
        </div>

        {/* What each tool checks / misses */}
        <div className="space-y-6">
          <h2 className="text-2xl font-bold tracking-tight">What each tool actually checks</h2>
          <p className="text-muted-foreground">
            The fastest way to understand the difference is to look at what each tool
            is designed to find -- and what it is not designed to find.
          </p>
          <div className="overflow-x-auto rounded-xl border border-border">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-border bg-card/80">
                  <th className="text-left px-5 py-3 font-semibold text-muted-foreground w-1/4">Tool</th>
                  <th className="text-left px-5 py-3 font-semibold text-muted-foreground w-5/12">What it checks</th>
                  <th className="text-left px-5 py-3 font-semibold text-muted-foreground w-5/12">What it misses</th>
                </tr>
              </thead>
              <tbody>
                {whatEachChecks.map((row, i) => (
                  <tr
                    key={row.tool}
                    className={`border-b border-border last:border-0 ${
                      row.tool === "GauntletCI"
                        ? "bg-cyan-500/5"
                        : i % 2 === 0
                        ? ""
                        : "bg-card/20"
                    }`}
                  >
                    <td className={`px-5 py-3 font-semibold ${row.tool === "GauntletCI" ? "text-cyan-400" : "text-foreground/80"}`}>
                      {row.tool}
                    </td>
                    <td className="px-5 py-3 text-muted-foreground">{row.checks}</td>
                    <td className={`px-5 py-3 ${row.tool === "GauntletCI" ? "text-muted-foreground/40" : "text-amber-400/80"}`}>
                      {row.misses}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>

        {/* Key difference */}
        <div className="grid sm:grid-cols-2 gap-6">
          <div className="rounded-xl border border-border bg-card p-7 space-y-3">
            <p className="text-xs font-semibold uppercase tracking-widest text-muted-foreground">AI Code Review</p>
            <h2 className="text-lg font-bold">Intent and style validation</h2>
            <p className="text-sm text-muted-foreground leading-relaxed">
              AI code review tools use large language models to read your code and provide
              suggestions. They can identify unclear naming, suggest better patterns, catch
              obvious logic errors, and comment on whether the code looks correct.
            </p>
            <p className="text-sm text-muted-foreground leading-relaxed">
              These tools operate on the full file or PR context and produce non-deterministic
              output. The same code reviewed twice may produce different comments. They run
              post-push inside the PR, after the code has already left the developer's machine.
              They require an internet connection and incur API token cost per review.
            </p>
          </div>
          <div className="rounded-xl border border-cyan-500/30 bg-cyan-500/5 p-7 space-y-3">
            <p className="text-xs font-semibold uppercase tracking-widest text-cyan-400">GauntletCI</p>
            <h2 className="text-lg font-bold">Behavioral Change Risk detection</h2>
            <p className="text-sm text-muted-foreground leading-relaxed">
              GauntletCI runs on your machine, reads only the staged diff, and applies
              deterministic rules to identify Behavioral Change Risk before the commit is
              created. The same input always produces the same output. No LLM. No cloud.
              No token cost. Results in under one second.
            </p>
            <p className="text-sm text-muted-foreground leading-relaxed">
              It catches the category of change that looks safe to a reviewer -- and to an
              AI -- because the code is syntactically correct. A deleted null guard, a changed
              exception path, a modified API contract: these compile cleanly, pass tests, and
              read normally. They are not style problems. They are behavioral risk.
            </p>
          </div>
        </div>

        {/* The gap */}
        <div className="space-y-6">
          <h2 className="text-2xl font-bold tracking-tight">The gap AI review cannot close</h2>
          <p className="text-muted-foreground leading-relaxed">
            AI code review is fundamentally a language task. The model reads code and produces
            commentary based on what the code appears to intend. It does not have a behavioral
            model of what the code does at runtime, and it does not track what changed between
            versions in a semantically precise way.
          </p>
          <div className="grid sm:grid-cols-2 gap-4">
            {[
              {
                title: "Null guard removed",
                tag: "Missed by AI review",
                body: "The remaining code is syntactically valid. An AI reviewer sees a method that still works. It does not know that a defensive check was present before and is now gone. GauntletCI fires on the deletion in the diff.",
              },
              {
                title: "Null guard removed",
                tag: "Caught by GauntletCI",
                body: "GauntletCI scans the diff for guard removal patterns. The rule fires on the deleted line. The finding appears before the commit is created, with a precise reference to the removed check.",
              },
              {
                title: "Exception path changed silently",
                tag: "Missed by AI review",
                body: "A catch block is modified so a previously surfaced exception is now swallowed. The code looks clean. The reviewer sees tidy error handling. There is no style problem. There is a behavioral regression.",
              },
              {
                title: "Exception path changed silently",
                tag: "Caught by GauntletCI",
                body: "GauntletCI detects the exception handling change in the diff and flags the behavioral shift. The change looked safe. It was not.",
              },
            ].map((item) => (
              <div key={`${item.title}-${item.tag}`} className="rounded-lg border border-border bg-card p-5">
                <div className="flex items-center gap-2 mb-2">
                  <h3 className="text-sm font-semibold">{item.title}</h3>
                  <span className={`text-[10px] font-semibold px-1.5 py-0.5 rounded ${item.tag.startsWith("Caught") ? "bg-cyan-500/20 text-cyan-400" : "bg-amber-500/10 text-amber-400/80"}`}>
                    {item.tag}
                  </span>
                </div>
                <p className="text-xs text-muted-foreground leading-relaxed">{item.body}</p>
              </div>
            ))}
          </div>
        </div>

        {/* Feature comparison table */}
        <div className="space-y-4">
          <h2 className="text-2xl font-bold tracking-tight">Feature comparison</h2>
          <div className="overflow-x-auto rounded-xl border border-border">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-border bg-card/80">
                  <th className="text-left px-5 py-3 font-semibold text-muted-foreground w-1/3">Feature</th>
                  <th className="text-left px-5 py-3 font-semibold text-cyan-400">GauntletCI</th>
                  <th className="text-left px-5 py-3 font-semibold text-muted-foreground">AI Code Review</th>
                </tr>
              </thead>
              <tbody>
                {featureRows.map((row, i) => (
                  <tr key={row.label} className={`border-b border-border last:border-0 ${i % 2 === 0 ? "" : "bg-card/20"}`}>
                    <td className="px-5 py-3 text-muted-foreground">{row.label}</td>
                    <td className="px-5 py-3 text-foreground font-medium">{row.gauntlet}</td>
                    <td className="px-5 py-3 text-muted-foreground">{row.ai}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>

        {/* When to use each */}
        <div className="grid sm:grid-cols-2 gap-6">
          <div className="rounded-xl border border-border bg-card p-7 space-y-4">
            <h2 className="text-lg font-bold">When AI code review is the right choice</h2>
            <ul className="space-y-2 text-sm text-muted-foreground">
              {[
                "You want a second perspective on code clarity and naming",
                "You need help identifying patterns or best practices in unfamiliar areas",
                "Your team benefits from async PR commentary on intent and structure",
                "You want AI-assisted refactoring suggestions during development",
              ].map((item) => (
                <li key={item} className="flex gap-2">
                  <span className="text-muted-foreground/40 shrink-0 mt-0.5">-</span>
                  {item}
                </li>
              ))}
            </ul>
          </div>
          <div className="rounded-xl border border-cyan-500/30 bg-cyan-500/5 p-7 space-y-4">
            <h2 className="text-lg font-bold">When GauntletCI is the right choice</h2>
            <ul className="space-y-2 text-sm text-muted-foreground">
              {[
                "You want to catch behavioral regressions before the commit is created",
                "Your team needs deterministic, repeatable findings with no LLM variance",
                "You require 100% local execution -- no code uploads, no API cost",
                "You work in .NET / C# and want diff-aware behavioral detection",
                "Pre-commit speed matters -- results in under one second",
              ].map((item) => (
                <li key={item} className="flex gap-2">
                  <span className="text-cyan-400 shrink-0 mt-0.5">-</span>
                  {item}
                </li>
              ))}
            </ul>
          </div>
        </div>

        {/* Complementary use */}
        <div className="rounded-xl border border-border bg-card/40 p-7 space-y-3">
          <h2 className="text-lg font-bold">Using GauntletCI and AI code review together</h2>
          <p className="text-sm text-muted-foreground leading-relaxed">
            The two tools are complementary. GauntletCI runs before the commit to block
            behavioral regressions locally. AI code review runs in the PR to improve clarity
            and catch intent problems. Neither can do the other's job.
          </p>
          <p className="text-sm text-muted-foreground leading-relaxed">
            GauntletCI also ships with an MCP server. This means AI assistants like GitHub
            Copilot and Cursor can call GauntletCI directly inside the IDE -- surfacing
            Behavioral Change Risk findings inline while you write, without leaving the
            development environment.
          </p>
          <p className="text-sm text-muted-foreground leading-relaxed">
            A common setup: GauntletCI runs as a pre-commit hook. AI code review runs as
            a PR check. GauntletCI catches what looks safe but is not. AI review catches
            what is technically correct but unclear. Neither step replaces the other.
          </p>
        </div>

        {/* CTAs */}
        <div className="flex flex-col sm:flex-row gap-4 justify-center">
          <Link
            href="/docs"
            className="inline-flex items-center justify-center gap-2 rounded-lg bg-cyan-500 px-6 py-3 text-sm font-semibold text-background hover:bg-cyan-400 transition-colors"
          >
            Get started free
          </Link>
          <Link
            href="/docs/rules"
            className="inline-flex items-center gap-2 rounded-lg border border-border bg-card px-6 py-3 text-sm font-semibold hover:bg-card/80 transition-colors"
          >
            View the rule library
          </Link>
          <Link
            href="/compare/gauntletci-vs-sonarqube"
            className="inline-flex items-center gap-2 rounded-lg border border-border bg-card px-6 py-3 text-sm font-semibold hover:bg-card/80 transition-colors"
          >
            Compare vs SonarQube
          </Link>
        </div>

      </div>
    </div>
    <Footer />
    </>
  );
}

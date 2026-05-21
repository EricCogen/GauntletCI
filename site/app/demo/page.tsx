import type { Metadata } from "next";
import Link from "next/link";
import {
  ArrowRight,
  CheckCircle2,
  ExternalLink,
  GitPullRequest,
  Github,
  ShieldCheck,
  XCircle,
} from "lucide-react";
import { Header } from "@/components/header";
import { Footer } from "@/components/footer";
import { Breadcrumbs } from "@/components/breadcrumbs";

const demoRepoUrl = "https://github.com/EricCogen/GauntletCI-Demo";
const demoPullsUrl = `${demoRepoUrl}/pulls`;

const scenarios = [
  {
    number: 277,
    label: "Clean control",
    title: "Safe typo / wording fix",
    verdict: "Expected clean",
    status: "Pass control",
    icon: CheckCircle2,
    href: `${demoRepoUrl}/pull/277`,
    description:
      "A one-line wording change that should not produce behavioral findings. Use it to judge whether the tool stays quiet on safe diffs.",
  },
  {
    number: 312,
    label: "Dependency injection",
    title: "Singleton captures scoped dependency",
    verdict: "Expected risk",
    status: "Risk scenario",
    icon: XCircle,
    href: `${demoRepoUrl}/pull/312`,
    description:
      "A singleton service captures scoped user context, creating a data-isolation and lifetime risk that is easy to miss in review.",
  },
  {
    number: 309,
    label: "Performance behavior",
    title: "Cache lookup removed",
    verdict: "Expected risk",
    status: "Risk scenario",
    icon: XCircle,
    href: `${demoRepoUrl}/pull/309`,
    description:
      "A cache read is removed during a simplification, shifting hot-path traffic directly to the database.",
  },
  {
    number: 303,
    label: "Async/concurrency",
    title: "Blocking on async task",
    verdict: "Expected risk",
    status: "Risk scenario",
    icon: XCircle,
    href: `${demoRepoUrl}/pull/303`,
    description:
      "A request path blocks on async work, turning an implementation shortcut into a potential deadlock or thread-pool starvation risk.",
  },
  {
    number: 299,
    label: "Security",
    title: "Role-based authorization bypass",
    verdict: "Expected risk",
    status: "Risk scenario",
    icon: XCircle,
    href: `${demoRepoUrl}/pull/299`,
    description:
      "An authorization check moves inside conditional logic, changing who can reach a protected code path.",
  },
  {
    number: 298,
    label: "API contracts",
    title: "Breaking public package contract",
    verdict: "Expected risk",
    status: "Risk scenario",
    icon: XCircle,
    href: `${demoRepoUrl}/pull/298`,
    description:
      "A public API contract changes without the kind of compatibility signal library consumers need before merge.",
  },
];

export const metadata: Metadata = {
  title: "Live Demo Repository | GauntletCI",
  description:
    "Explore the public GauntletCI demo repository with 36 scenario pull requests, GitHub Actions checks, and expected risk verdicts.",
  alternates: { canonical: "/demo" },
  openGraph: {
    title: "GauntletCI Live Demo Repository",
    description:
      "Inspect real scenario PRs that show clean diffs, behavioral risk findings, and GitHub Actions checks.",
    url: "https://gauntletci.com/demo",
    images: [{ url: "/og/demo.png", width: 1200, height: 630, alt: "GauntletCI Live Demo Repository" }],
  },
  twitter: {
    card: "summary_large_image",
    title: "GauntletCI Live Demo Repository",
    description:
      "Explore 36 public scenario PRs with expected risk verdicts and GitHub Actions checks.",
    images: ["/og/demo.png"],
  },
};

const jsonLd = {
  "@context": "https://schema.org",
  "@type": "CollectionPage",
  name: "GauntletCI Live Demo Repository",
  description:
    "A public demo repository with scenario pull requests for inspecting GauntletCI findings in GitHub Actions.",
  url: "https://gauntletci.com/demo",
  isPartOf: {
    "@type": "WebSite",
    name: "GauntletCI",
    url: "https://gauntletci.com",
  },
  mainEntity: {
    "@type": "SoftwareSourceCode",
    name: "GauntletCI-Demo",
    codeRepository: demoRepoUrl,
    programmingLanguage: "C#",
  },
};

export default function DemoPage() {
  return (
    <>
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLd) }} />
      <Header />
      <main className="min-h-screen bg-background pt-24">
        <div className="mx-auto max-w-6xl px-4 sm:px-6 lg:px-8 py-16 sm:py-20 space-y-16">
          <Breadcrumbs />

          <section className="grid gap-10 lg:grid-cols-[1.1fr_0.9fr] lg:items-center">
            <div className="space-y-6">
              <div className="inline-flex items-center gap-2 rounded-full border border-cyan-500/30 bg-cyan-500/10 px-4 py-1.5 text-sm text-muted-foreground">
                <Github className="h-4 w-4 text-cyan-400" />
                Public GitHub demo repository
              </div>
              <div className="space-y-4">
                <h1 className="text-4xl sm:text-5xl font-bold tracking-tight text-balance">
                  Inspect GauntletCI on real scenario pull requests
                </h1>
                <p className="text-xl text-muted-foreground leading-relaxed text-pretty">
                  The demo repo contains 36 public scenario PRs. Each PR explains the intended behavior change, shows the code diff, and runs GitHub Actions checks you can inspect directly.
                </p>
              </div>
              <div className="flex flex-col sm:flex-row gap-3">
                <Link
                  href={demoPullsUrl}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="inline-flex items-center justify-center gap-2 rounded-lg bg-cyan-500 px-5 py-3 text-sm font-semibold text-black hover:bg-cyan-400 transition-colors"
                >
                  Open scenario PRs
                  <ExternalLink className="h-4 w-4" />
                </Link>
                <Link
                  href={demoRepoUrl}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="inline-flex items-center justify-center gap-2 rounded-lg border border-border bg-card px-5 py-3 text-sm font-semibold hover:bg-card/80 transition-colors"
                >
                  View demo repository
                  <Github className="h-4 w-4" />
                </Link>
                <Link
                  href="/demo/before-after-diff"
                  className="inline-flex items-center justify-center gap-2 rounded-lg border border-border bg-card px-5 py-3 text-sm font-semibold hover:bg-card/80 transition-colors"
                >
                  See diff visualizer
                  <ArrowRight className="h-4 w-4" />
                </Link>
              </div>
            </div>

            <div className="rounded-2xl border border-border bg-card/50 p-6 shadow-2xl shadow-black/20">
              <div className="flex items-center justify-between border-b border-border pb-4">
                <div>
                  <p className="text-sm font-semibold text-foreground">EricCogen/GauntletCI-Demo</p>
                  <p className="mt-1 text-xs text-muted-foreground">Public repository · default branch main</p>
                </div>
                <ShieldCheck className="h-5 w-5 text-cyan-400" />
              </div>
              <div className="mt-5 grid grid-cols-3 gap-3">
                <div className="rounded-lg border border-border bg-background/60 p-4">
                  <p className="text-2xl font-bold text-cyan-400">36</p>
                  <p className="mt-1 text-xs text-muted-foreground">open scenario PRs</p>
                </div>
                <div className="rounded-lg border border-border bg-background/60 p-4">
                  <p className="text-2xl font-bold text-cyan-400">C#</p>
                  <p className="mt-1 text-xs text-muted-foreground">demo codebase</p>
                </div>
                <div className="rounded-lg border border-border bg-background/60 p-4">
                  <p className="text-2xl font-bold text-cyan-400">Actions</p>
                  <p className="mt-1 text-xs text-muted-foreground">checks to inspect</p>
                </div>
              </div>
              <div className="mt-5 rounded-lg bg-zinc-950 p-4 font-mono text-xs text-muted-foreground">
                <p><span className="text-cyan-400">$</span> git clone {demoRepoUrl}</p>
                <p className="mt-2"><span className="text-cyan-400">$</span> gh pr list --repo EricCogen/GauntletCI-Demo</p>
              </div>
            </div>
          </section>

          <section className="space-y-6">
            <div className="max-w-3xl">
              <p className="text-sm font-semibold uppercase tracking-widest text-cyan-400">Curated starting points</p>
              <h2 className="mt-2 text-3xl font-bold tracking-tight">Start with these PRs</h2>
              <p className="mt-3 text-muted-foreground leading-relaxed">
                These links go to GitHub so you can inspect the PR text, changed files, checks, and annotations in the same interface your team already uses.
              </p>
            </div>
            <div className="grid gap-4 md:grid-cols-2">
              {scenarios.map((scenario) => {
                const Icon = scenario.icon;
                return (
                  <Link
                    key={scenario.number}
                    href={scenario.href}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="group rounded-xl border border-border bg-card p-5 hover:border-cyan-500/40 hover:bg-card/80 transition-colors"
                  >
                    <div className="flex items-start justify-between gap-4">
                      <div className="flex items-center gap-2">
                        <span className="inline-flex items-center gap-1 rounded-md bg-cyan-500/10 px-2 py-0.5 text-xs font-mono font-semibold text-cyan-400 ring-1 ring-inset ring-cyan-500/20">
                          PR #{scenario.number}
                        </span>
                        <span className="rounded-md bg-muted px-2 py-0.5 text-xs text-muted-foreground">
                          {scenario.label}
                        </span>
                      </div>
                      <Icon className={`h-5 w-5 ${scenario.status === "Pass control" ? "text-emerald-400" : "text-red-400"}`} />
                    </div>
                    <h3 className="mt-4 text-lg font-semibold text-foreground group-hover:text-cyan-400 transition-colors">
                      {scenario.title}
                    </h3>
                    <p className="mt-2 text-sm text-muted-foreground leading-relaxed">
                      {scenario.description}
                    </p>
                    <div className="mt-4 flex items-center justify-between">
                      <span className="text-xs font-medium text-muted-foreground">{scenario.verdict}</span>
                      <span className="inline-flex items-center gap-1 text-xs font-medium text-cyan-400">
                        Inspect PR <ArrowRight className="h-3.5 w-3.5" />
                      </span>
                    </div>
                  </Link>
                );
              })}
            </div>
          </section>

          <section className="grid gap-4 md:grid-cols-3">
            <div className="rounded-xl border border-border bg-card/50 p-5">
              <GitPullRequest className="h-5 w-5 text-cyan-400" />
              <h3 className="mt-4 font-semibold text-foreground">Use it as a buyer proof point</h3>
              <p className="mt-2 text-sm text-muted-foreground leading-relaxed">
                Send reviewers directly to scenario PRs instead of asking them to trust a screenshot or marketing claim.
              </p>
            </div>
            <div className="rounded-xl border border-border bg-card/50 p-5">
              <ShieldCheck className="h-5 w-5 text-cyan-400" />
              <h3 className="mt-4 font-semibold text-foreground">Compare clean vs risky diffs</h3>
              <p className="mt-2 text-sm text-muted-foreground leading-relaxed">
                Start with PR #277 as the clean control, then compare it with risk scenarios across security, async, API, and data behavior.
              </p>
            </div>
            <div className="rounded-xl border border-border bg-card/50 p-5">
              <Github className="h-5 w-5 text-cyan-400" />
              <h3 className="mt-4 font-semibold text-foreground">Inspect checks where they run</h3>
              <p className="mt-2 text-sm text-muted-foreground leading-relaxed">
                The demo stays on GitHub. You can inspect changed files, PR explanations, Actions runs, and check details without a custom sandbox.
              </p>
            </div>
          </section>

          <section className="rounded-2xl border border-border bg-card/50 p-6">
            <p className="text-sm font-semibold uppercase tracking-widest text-cyan-400">
              Learn what the checks mean
            </p>
            <h2 className="mt-2 text-2xl font-bold tracking-tight">
              Connect each demo finding back to the rule library
            </h2>
            <p className="mt-3 max-w-3xl text-sm text-muted-foreground leading-relaxed">
              After you inspect a scenario PR, use the docs to understand the rule category,
              severity, and CI integration path behind the finding.
            </p>
            <div className="mt-5 flex flex-col gap-3 sm:flex-row">
              <Link
                href="/docs/rules"
                className="inline-flex items-center justify-center gap-2 rounded-lg border border-border bg-background px-4 py-2 text-sm font-semibold hover:border-cyan-500/40 hover:bg-card transition-colors"
              >
                Browse detection rules
                <ArrowRight className="h-4 w-4" />
              </Link>
              <Link
                href="/docs/integrations/github-action"
                className="inline-flex items-center justify-center gap-2 rounded-lg border border-border bg-background px-4 py-2 text-sm font-semibold hover:border-cyan-500/40 hover:bg-card transition-colors"
              >
                View GitHub Actions setup
                <ArrowRight className="h-4 w-4" />
              </Link>
              <Link
                href="/demo/before-after-diff"
                className="inline-flex items-center justify-center gap-2 rounded-lg border border-border bg-background px-4 py-2 text-sm font-semibold hover:border-cyan-500/40 hover:bg-card transition-colors"
              >
                See before/after diff
                <ArrowRight className="h-4 w-4" />
              </Link>
              <Link
                href="/demo/github-checks-sarif"
                className="inline-flex items-center justify-center gap-2 rounded-lg border border-border bg-background px-4 py-2 text-sm font-semibold hover:border-cyan-500/40 hover:bg-card transition-colors"
              >
                Preview Checks and SARIF
                <ArrowRight className="h-4 w-4" />
              </Link>
            </div>
          </section>
        </div>
      </main>
      <Footer />
    </>
  );
}

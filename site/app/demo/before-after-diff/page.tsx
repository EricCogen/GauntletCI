import type { Metadata } from "next";
import type { ReactNode } from "react";
import Link from "next/link";
import {
  AlertTriangle,
  ArrowRight,
  CheckCircle2,
  Code2,
  GitCompareArrows,
  GitPullRequest,
  ShieldAlert,
} from "lucide-react";
import { Header } from "@/components/header";
import { Footer } from "@/components/footer";
import { Breadcrumbs } from "@/components/breadcrumbs";
import { rules } from "@/lib/rules";

const demoPrUrl = "https://github.com/EricCogen/GauntletCI-Demo/pull/299";

const beforeCode = `using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[HttpPost("settings")]
[Authorize(Policy = "AdminOnly")]
public async Task<ActionResult> UpdateSystemSettings(
    SystemSettingsRequest request,
    CancellationToken ct)
{
    await _settingsService.UpdateSettingsAsync(request, ct);
    return Ok();
}`;

const afterCode = `using Microsoft.AspNetCore.Mvc;

[HttpPost("settings")]
public async Task<ActionResult> UpdateSystemSettings(
    SystemSettingsRequest request,
    CancellationToken ct)
{
    if (User?.HasClaim("role", "admin") ?? false)
    {
        await _settingsService.UpdateSettingsAsync(request, ct);
    }

    return Ok();
}`;

const diffCode = `- using Microsoft.AspNetCore.Authorization;
  using Microsoft.AspNetCore.Mvc;
 
  [HttpPost("settings")]
- [Authorize(Policy = "AdminOnly")]
  public async Task<ActionResult> UpdateSystemSettings(
      SystemSettingsRequest request,
      CancellationToken ct)
  {
-     await _settingsService.UpdateSettingsAsync(request, ct);
+     if (User?.HasClaim("role", "admin") ?? false)
+     {
+         await _settingsService.UpdateSettingsAsync(request, ct);
+     }
+ 
      return Ok();
  }`;

function requireRule(ruleId: string) {
  const rule = rules.find((candidate) => candidate.id === ruleId);
  if (!rule) {
    throw new Error(`Missing rule metadata for ${ruleId}`);
  }
  return rule;
}

const securityRule = requireRule("GCI0012");
const behaviorRule = requireRule("GCI0003");

export const metadata: Metadata = {
  title: "Before/After Diff Visualizer | GauntletCI Demo",
  description:
    "See a static before/after code diff with example GauntletCI findings, rule links, and suggested next actions for a risky authorization change.",
  alternates: { canonical: "/demo/before-after-diff" },
  openGraph: {
    title: "GauntletCI Before/After Diff Visualizer",
    description:
      "Preview how a behavioral code change maps to GauntletCI findings, rule evidence, and merge guidance.",
    url: "https://gauntletci.com/demo/before-after-diff",
    images: [
      {
        url: "/og/before-after-diff.png",
        width: 1200,
        height: 630,
        alt: "GauntletCI Before/After Diff Visualizer",
      },
    ],
  },
  twitter: {
    card: "summary_large_image",
    title: "GauntletCI Before/After Diff Visualizer",
    description:
      "See before code, after code, changed lines, and example GauntletCI findings in one view.",
    images: ["/og/before-after-diff.png"],
  },
};

const jsonLd = {
  "@context": "https://schema.org",
  "@type": "TechArticle",
  headline: "GauntletCI Before/After Diff Visualizer",
  description:
    "A static example that explains how GauntletCI maps a pull request diff to rule findings and next actions.",
  url: "https://gauntletci.com/demo/before-after-diff",
  publisher: { "@type": "Organization", name: "GauntletCI", url: "https://gauntletci.com" },
  isPartOf: { "@type": "WebPage", name: "GauntletCI Live Demo", url: "https://gauntletci.com/demo" },
};

function StatusPill({ children, tone }: { children: ReactNode; tone: "red" | "yellow" | "green" | "cyan" }) {
  const tones = {
    red: "border-red-500/30 bg-red-500/10 text-red-400",
    yellow: "border-yellow-500/30 bg-yellow-500/10 text-yellow-400",
    green: "border-emerald-500/30 bg-emerald-500/10 text-emerald-400",
    cyan: "border-cyan-500/30 bg-cyan-500/10 text-cyan-400",
  };

  return (
    <span className={`inline-flex items-center rounded-full border px-2.5 py-1 text-xs font-semibold ${tones[tone]}`}>
      {children}
    </span>
  );
}

function CodePanel({ title, code, tone }: { title: string; code: string; tone: "cyan" | "yellow" }) {
  const accent = tone === "cyan" ? "text-cyan-400" : "text-yellow-400";

  return (
    <div className="rounded-2xl border border-border bg-card/50 overflow-hidden">
      <div className="flex items-center justify-between border-b border-border px-4 py-3">
        <p className="text-sm font-semibold text-foreground">{title}</p>
        <Code2 className={`h-4 w-4 ${accent}`} />
      </div>
      <div className="overflow-x-auto bg-zinc-950 p-4">
        <pre className="whitespace-pre text-xs leading-relaxed text-zinc-200">{code}</pre>
      </div>
    </div>
  );
}

function DiffLine({ line }: { line: string }) {
  const isRemoved = line.startsWith("-");
  const isAdded = line.startsWith("+");
  const tone = isRemoved
    ? "bg-red-500/10 text-red-100"
    : isAdded
      ? "bg-emerald-500/10 text-emerald-100"
      : "text-zinc-300";

  return <div className={`min-w-max px-4 ${tone}`}>{line || " "}</div>;
}

export default function BeforeAfterDiffPage() {
  return (
    <>
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLd) }} />
      <Header />
      <main className="min-h-screen bg-background pt-24">
        <div className="mx-auto max-w-6xl px-4 sm:px-6 lg:px-8 py-16 sm:py-20 space-y-16">
          <Breadcrumbs />

          <section className="grid gap-10 lg:grid-cols-[1.05fr_0.95fr] lg:items-center">
            <div className="space-y-6">
              <div className="inline-flex items-center gap-2 rounded-full border border-cyan-500/30 bg-cyan-500/10 px-4 py-1.5 text-sm text-muted-foreground">
                <GitCompareArrows className="h-4 w-4 text-cyan-400" />
                Static visualizer, based on demo PR #299
              </div>
              <div className="space-y-4">
                <h1 className="text-4xl sm:text-5xl font-bold tracking-tight text-balance">
                  See exactly how a risky diff becomes a GauntletCI finding
                </h1>
                <p className="text-xl text-muted-foreground leading-relaxed text-pretty">
                  This before/after view uses the role-based authorization scenario from PR #299 to show the
                  pieces reviewers need: original behavior, changed behavior, diff evidence, rule
                  mapping, and the next action before merge.
                </p>
              </div>
              <div className="rounded-xl border border-yellow-500/30 bg-yellow-500/10 p-4 text-sm text-yellow-100">
                <p className="font-semibold text-yellow-300">Illustrative example</p>
                <p className="mt-1 text-yellow-100/80">
                  This is a static walkthrough, not a live analyzer. The scenario links to the public
                  demo PR so you can inspect the full baseline and regressed files separately.
                </p>
              </div>
              <div className="flex flex-col gap-3 sm:flex-row">
                <Link
                  href={demoPrUrl}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="inline-flex items-center justify-center gap-2 rounded-lg bg-cyan-500 px-5 py-3 text-sm font-semibold text-black hover:bg-cyan-400 transition-colors"
                >
                  Open demo PR #299
                  <ArrowRight className="h-4 w-4" />
                </Link>
                <Link
                  href="/demo"
                  className="inline-flex items-center justify-center gap-2 rounded-lg border border-border bg-card px-5 py-3 text-sm font-semibold hover:bg-card/80 transition-colors"
                >
                  Back to live demo
                  <ArrowRight className="h-4 w-4" />
                </Link>
              </div>
            </div>

            <div className="rounded-2xl border border-border bg-card/50 p-6 shadow-2xl shadow-black/20">
              <div className="flex items-center justify-between border-b border-border pb-4">
                <div>
                  <p className="text-sm font-semibold text-foreground">Reviewer summary</p>
                  <p className="mt-1 text-xs text-muted-foreground">
                    src/OrderService/Controllers/AdminPolicyController.Regressed.cs:20
                  </p>
                </div>
                <StatusPill tone="red">block</StatusPill>
              </div>
              <div className="mt-5 space-y-4">
                <div className="rounded-lg border border-red-500/30 bg-red-500/10 p-4">
                  <div className="flex items-start gap-3">
                    <ShieldAlert className="mt-0.5 h-5 w-5 text-red-400" />
                    <div>
                      <p className="font-semibold text-foreground">
                        {securityRule.id}: {securityRule.name}
                      </p>
                      <p className="mt-1 text-sm text-muted-foreground">
                        Endpoint authorization changed from a framework policy attribute to inline
                        branch logic.
                      </p>
                    </div>
                  </div>
                </div>
                <div className="grid gap-3 sm:grid-cols-3">
                  <div className="rounded-lg border border-border bg-background/60 p-4">
                    <p className="text-2xl font-bold text-red-400">2</p>
                    <p className="mt-1 text-xs text-muted-foreground">Block findings</p>
                  </div>
                  <div className="rounded-lg border border-border bg-background/60 p-4">
                    <p className="text-2xl font-bold text-yellow-400">1</p>
                    <p className="mt-1 text-xs text-muted-foreground">Protected file</p>
                  </div>
                  <div className="rounded-lg border border-border bg-background/60 p-4">
                    <p className="text-2xl font-bold text-cyan-400">1</p>
                    <p className="mt-1 text-xs text-muted-foreground">Suggested fix</p>
                  </div>
                </div>
              </div>
            </div>
          </section>

          <section className="grid gap-6 lg:grid-cols-2">
            <CodePanel title="Before: framework-level policy" code={beforeCode} tone="cyan" />
            <CodePanel title="After: inline claim branch" code={afterCode} tone="yellow" />
          </section>

          <section className="space-y-6">
            <div>
              <p className="text-sm font-semibold uppercase tracking-widest text-cyan-400">
                Diff evidence
              </p>
              <h2 className="mt-2 text-3xl font-bold tracking-tight">
                The changed lines explain the risk
              </h2>
              <p className="mt-3 max-w-3xl text-sm text-muted-foreground leading-relaxed">
                The visualizer keeps literal <code className="bg-muted px-1 rounded text-xs">+</code>{" "}
                and <code className="bg-muted px-1 rounded text-xs">-</code> markers so the signal is
                readable without relying only on red and green highlighting.
              </p>
            </div>
            <div className="overflow-x-auto rounded-2xl border border-border bg-zinc-950 py-4 font-mono text-xs leading-relaxed">
              {diffCode.split("\n").map((line, index) => (
                <DiffLine key={`${index}-${line}`} line={line} />
              ))}
            </div>
          </section>

          <section className="grid gap-6 lg:grid-cols-[0.95fr_1.05fr]">
            <div className="rounded-2xl border border-border bg-card/50 p-6">
              <div className="flex items-center gap-3">
                <AlertTriangle className="h-5 w-5 text-red-400" />
                <h2 className="text-2xl font-bold tracking-tight">Example findings</h2>
              </div>
              <div className="mt-5 space-y-4">
                <div className="rounded-lg border border-red-500/30 bg-red-500/10 p-4">
                  <div className="flex flex-wrap items-center gap-2">
                    <StatusPill tone="red">{securityRule.severity}</StatusPill>
                    <Link href={`/docs/rules/${securityRule.id}`} className="text-xs font-semibold text-cyan-400 hover:underline">
                      {securityRule.id}: {securityRule.name}
                    </Link>
                  </div>
                  <p className="mt-3 text-sm text-muted-foreground leading-relaxed">
                    Authorization logic changed on a protected endpoint. The framework-level policy
                    attribute was removed and replaced with inline claim logic that returns success
                    even when the update does not run.
                  </p>
                  <p className="mt-3 font-mono text-xs text-muted-foreground">
                    src/OrderService/Controllers/AdminPolicyController.Regressed.cs:20
                  </p>
                </div>

                <div className="rounded-lg border border-red-500/30 bg-red-500/10 p-4">
                  <div className="flex flex-wrap items-center gap-2">
                    <StatusPill tone="red">{behaviorRule.severity}</StatusPill>
                    <Link href={`/docs/rules/${behaviorRule.id}`} className="text-xs font-semibold text-cyan-400 hover:underline">
                      {behaviorRule.id}: {behaviorRule.name}
                    </Link>
                  </div>
                  <p className="mt-3 text-sm text-muted-foreground leading-relaxed">
                    Runtime behavior changed without a corresponding test update in the diff. The
                    reviewer needs evidence that unauthorized settings updates are rejected rather
                    than silently returning success.
                  </p>
                </div>
              </div>
            </div>

            <div className="rounded-2xl border border-border bg-card/50 p-6">
              <div className="flex items-center gap-3">
                <CheckCircle2 className="h-5 w-5 text-emerald-400" />
                <h2 className="text-2xl font-bold tracking-tight">Suggested next action</h2>
              </div>
              <div className="mt-5 space-y-4 text-sm text-muted-foreground leading-relaxed">
                <p>
                  Restore the <code className="bg-muted px-1 rounded text-xs">AdminOnly</code> policy
                  attribute or return <code className="bg-muted px-1 rounded text-xs">Forbid()</code>{" "}
                  when the inline claim check fails.
                </p>
                <p>
                  Add tests that prove non-admin users cannot update system settings through the
                  changed path and do not receive a success-shaped response.
                </p>
                <p>
                  If the bypass is intentional, document the business rule in the PR and update
                  policy tests so the merge decision has durable evidence.
                </p>
              </div>
            </div>
          </section>

          <section className="rounded-2xl border border-border bg-card/50 p-6">
            <div className="flex items-center gap-3">
              <GitPullRequest className="h-5 w-5 text-cyan-400" />
              <h2 className="text-2xl font-bold tracking-tight">Connect the visualizer to the full workflow</h2>
            </div>
            <p className="mt-3 max-w-3xl text-sm text-muted-foreground leading-relaxed">
              Use this page to understand the finding, then inspect the real demo PR, rule docs, and
              GitHub output surfaces your team can wire into required checks.
            </p>
            <div className="mt-5 grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
              {[
                { href: "/demo", label: "Live demo PRs" },
                { href: "/demo/github-checks-sarif", label: "Checks and SARIF" },
                { href: `/docs/rules/${securityRule.id}`, label: `${securityRule.id} rule` },
                { href: "/docs/rules", label: "Rule library" },
              ].map((item) => (
                <Link
                  key={item.href}
                  href={item.href}
                  className="inline-flex items-center justify-between gap-2 rounded-lg border border-border bg-background px-4 py-3 text-sm font-semibold hover:border-cyan-500/40 hover:bg-card transition-colors"
                >
                  {item.label}
                  <ArrowRight className="h-4 w-4 text-cyan-400" />
                </Link>
              ))}
            </div>
          </section>
        </div>
      </main>
      <Footer />
    </>
  );
}

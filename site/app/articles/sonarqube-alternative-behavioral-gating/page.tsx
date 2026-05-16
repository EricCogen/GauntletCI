import type { Metadata } from "next";
import Link from "next/link";
import { Header } from "@/components/header";
import { Footer } from "@/components/footer";
import { AuthorBio } from "@/components/author-bio";
import { Breadcrumbs } from "@/components/breadcrumbs";

export const metadata: Metadata = {
  title: "SonarQube Alternative for .NET PR Gating | Behavioral Audit Layer",
  description:
    "Why SonarQube misses behavioral drift in .NET. How a deterministic Roslyn-based behavioral audit layer catches the regressions that traditional quality gates skip.",
  alternates: { canonical: "/articles/sonarqube-alternative-behavioral-gating" },
  keywords: ["SonarQube", "PR gating", ".NET", "code quality", "behavioral risk", "static analysis", "CI/CD"],
  authors: [{ name: "Eric Cogen", url: "https://github.com/EricCogen" }],
  creator: "Eric Cogen",
  publisher: "GauntletCI",
  openGraph: {
    title: "SonarQube Alternative for .NET PR Gating: Behavioral Audit Layer",
    description:
      "Why SonarQube misses behavioral drift. How deterministic Roslyn analysis catches structural regressions that quality gates skip.",
    url: "https://gauntletci.com/articles/sonarqube-alternative-behavioral-gating",
    type: "article",
    images: [
      {
        url: "/og/sonarqube-alternative.png",
        width: 1200,
        height: 630,
        alt: "SonarQube vs Behavioral Audit Layer",
      },
    ],
  },
  twitter: {
    card: "summary_large_image",
    title: "SonarQube Alternative for .NET PR Gating",
    description: "Why behavioral audit layers catch what SonarQube misses",
    images: ["/og/sonarqube-alternative.png"],
  },
};

const jsonLd = {
  "@context": "https://schema.org",
  "@type": "Article",
  headline: "SonarQube Alternative for .NET PR Gating: A Behavioral Audit Layer Approach",
  description:
    "Explore why SonarQube-based PR gating misses behavioral drift in .NET code. Learn how deterministic Roslyn-backed structural analysis catches the regressions that quality gates skip.",
  image: "/og/sonarqube-alternative.png",
  datePublished: "2026-05-16T00:00:00Z",
  author: {
    "@type": "Person",
    name: "Eric Cogen",
    url: "https://github.com/EricCogen",
  },
  publisher: {
    "@type": "Organization",
    name: "GauntletCI",
    url: "https://gauntletci.com",
    logo: {
      "@type": "ImageObject",
      url: "https://gauntletci.com/icon.svg",
    },
  },
  mainEntityOfPage: {
    "@type": "WebPage",
    "@id": "https://gauntletci.com/articles/sonarqube-alternative-behavioral-gating",
  },
  keywords: [
    "SonarQube",
    "PR gating",
    "code review",
    ".NET",
    "behavioral risk",
    "static analysis",
    "CI/CD",
    "code quality",
  ],
};

export default function SonarQubeAlternativeArticle() {
  return (
    <>
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLd) }} />
      <Header />
      <main className="min-h-screen bg-background pt-24">
        <div className="mx-auto max-w-4xl px-4 sm:px-6 lg:px-8 py-16 sm:py-20 space-y-16">

          {/* Breadcrumbs */}
          <Breadcrumbs />

          {/* Hero */}
          <div className="space-y-5 border-b border-border pb-12">
            <div className="flex items-center justify-between">
              <p className="text-sm font-semibold uppercase tracking-widest text-cyan-400">Analysis</p>
              <Link href="/articles" className="text-sm text-muted-foreground hover:text-cyan-400 transition-colors">← All articles</Link>
            </div>
            <h1 className="text-4xl sm:text-5xl font-bold tracking-tight text-balance">
              Beyond SonarQube: Building a Behavioral Audit Layer for .NET PR Gating
            </h1>
            <p className="text-xl text-muted-foreground leading-relaxed text-pretty">
              SonarQube catches code smells and security patterns. But it misses the semantic drift that breaks production systems. Here's why, and what to do about it.
            </p>
            <div className="flex items-center gap-2 pt-1">
              <span className="text-sm font-medium text-foreground">Eric Cogen</span>
              <span className="text-muted-foreground/40 text-sm">·</span>
              <span className="text-sm text-muted-foreground">Founder, GauntletCI</span>
              <span className="text-muted-foreground/40 text-sm">·</span>
              <time className="text-sm text-muted-foreground" dateTime="2026-05-16">May 16, 2026</time>
            </div>
          </div>

          {/* Lead */}
          <section className="space-y-4">
            <p className="text-lg text-muted-foreground leading-relaxed">
              If you run a mature .NET shop, your pull request workflow almost certainly includes SonarQube or a similar static analysis tool. These tools are powerful: they track code quality metrics, block merges on quality gate violations, and flag newly discovered vulnerabilities.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              But for senior engineering teams, something crucial still slips through: <strong className="text-foreground">behavioral drift</strong>.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              SonarQube and traditional SAST tools are obsessed with <em>state and style</em>. They catch SQL injection vectors, missing null checks, and style violations. What they miss is the subtle, high-risk semantic change that leaves surrounding code syntactically valid but fundamentally broken in intent.
            </p>
          </section>

          {/* The Problem */}
          <section className="space-y-5 border-t border-border pt-12">
            <h2 className="text-2xl font-bold tracking-tight">The Blind Spot in Semantic Equivalence</h2>
            <p className="text-muted-foreground leading-relaxed">
              SonarQube evaluates code quality by checking if <em>newly added or modified code</em> violates its rule set. But it lacks native understanding of structural and behavioral equivalence between the parent branch and the PR branch. It tells you if your new code is "clean," but it struggles to tell you if the <strong className="text-foreground">meaning</strong> of your existing systems has shifted.
            </p>
            <div className="space-y-4">
              <div className="rounded-lg border border-border bg-card p-5 space-y-3">
                <h3 className="font-semibold text-foreground">The Line Coverage Trap</h3>
                <p className="text-sm text-muted-foreground">
                  Hit 80% line coverage on your new feature branch, and the gate passes. But 100% test coverage on a heavily refactored class can still hide a critical behavioral regression if the underlying logic's sequence mutated in a way the existing assertions didn't account for. This is the gap that <Link href="/articles/why-tests-miss-bugs" className="text-cyan-400 hover:text-cyan-300 underline underline-offset-2">why tests miss bugs</Link> explores in detail.
                </p>
              </div>
              <div className="rounded-lg border border-border bg-card p-5 space-y-3">
                <h3 className="font-semibold text-foreground">Alert Fatigue vs. Structural Blindness</h3>
                <p className="text-sm text-muted-foreground">
                  Developers get bombarded with hundreds of minor maintainability warnings (e.g., "Rename this variable"), causing alert fatigue. Meanwhile, a structural regression slips through completely unnoticed because it doesn't violate a traditional linter rule. The result: critical issues hide in noise.
                </p>
              </div>
            </div>
            <p className="text-muted-foreground leading-relaxed">
              To build a better PR gate, we need to transition from auditing code snapshots to auditing <strong className="text-foreground">behavioral risk changes</strong>.
            </p>
          </section>

          {/* The Solution */}
          <section className="space-y-5 border-t border-border pt-12">
            <h2 className="text-2xl font-bold tracking-tight">The Behavioral Audit Layer</h2>
            <p className="text-muted-foreground leading-relaxed">
              Instead of asking, <em>"Is this code clean?"</em> a modern .NET PR gate should ask, <em>"What is the exact structural and behavioral delta between version A and version B?"</em>
            </p>
            <p className="text-muted-foreground leading-relaxed">
              A dedicated <strong className="text-foreground">Behavioral Audit Layer</strong>—a deterministic, Roslyn-native auditing tool—shifts the conversation by focusing exclusively on high-risk architectural and behavioral mutations. Rather than replacing SonarQube entirely, it complements it by detecting what quality gates miss: semantic regressions.
            </p>
            <p className="text-muted-foreground leading-relaxed">
              Unlike multi-language scanners that rely on abstract regex patterns or broad security signatures, a Roslyn-native auditor plugs directly into the C# compilation pipeline. It analyzes the Abstract Syntax Tree (AST) and semantic model of the incoming diff against the target branch to flag unexpected logic inversions and access control mutations.
            </p>
          </section>

          {/* Comparison Table */}
          <section className="space-y-5">
            <h3 className="text-xl font-bold tracking-tight">How It Differs from SonarQube PR Gating</h3>
            <div className="overflow-x-auto rounded-lg border border-border">
              <table className="w-full text-sm">
                <thead className="border-b border-border bg-card/50">
                  <tr>
                    <th className="text-left px-4 py-3 font-semibold text-foreground">Feature</th>
                    <th className="text-left px-4 py-3 font-semibold text-foreground">SonarQube Quality Gates</th>
                    <th className="text-left px-4 py-3 font-semibold text-foreground">Behavioral Audit Layer</th>
                  </tr>
                </thead>
                <tbody>
                  <tr className="border-b border-border">
                    <td className="px-4 py-3 font-medium text-foreground">Primary Focus</td>
                    <td className="px-4 py-3 text-muted-foreground">Snapshot quality, newly introduced security flaws, line coverage rules</td>
                    <td className="px-4 py-3 text-muted-foreground">Behavioral Change Risk (BCR), structural drift, semantic regressions</td>
                  </tr>
                  <tr className="border-b border-border">
                    <td className="px-4 py-3 font-medium text-foreground">Engine</td>
                    <td className="px-4 py-3 text-muted-foreground">Multi-language semantic scanners & pattern matching</td>
                    <td className="px-4 py-3 text-muted-foreground">Deep, deterministic Roslyn-based AST and semantic analysis</td>
                  </tr>
                  <tr className="border-b border-border">
                    <td className="px-4 py-3 font-medium text-foreground">Detection Scope</td>
                    <td className="px-4 py-3 text-muted-foreground">Code smells, known CVE signatures, data-flow vulnerabilities</td>
                    <td className="px-4 py-3 text-muted-foreground">Structural mutations, access control drops, execution order changes</td>
                  </tr>
                  <tr>
                    <td className="px-4 py-3 font-medium text-foreground">CI Philosophy</td>
                    <td className="px-4 py-3 text-muted-foreground">Post-commit visibility & quality compliance tracking</td>
                    <td className="px-4 py-3 text-muted-foreground">Hardened, pessimistic gating before merge occurs</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </section>

          {/* Concrete Example */}
          <section className="space-y-5 border-t border-border pt-12">
            <h2 className="text-2xl font-bold tracking-tight">A Concrete Pattern: What Traditional Gates Miss</h2>
            <p className="text-muted-foreground leading-relaxed">
              Consider a typical refactoring of an enterprise API endpoint handling payment updates. This is the kind of "cleanup" refactor that looks safe on the surface but introduces two critical regressions that SonarQube won't catch.
            </p>

            <div className="space-y-4">
              <div>
                <h3 className="font-semibold text-foreground mb-3">The Original Code (Target Branch)</h3>
                <div className="rounded-lg border border-border bg-card/80 overflow-hidden">
                  <div className="px-5 py-3 border-b border-border bg-muted/50">
                    <p className="text-xs font-mono text-muted-foreground/60">PaymentController.cs</p>
                  </div>
                  <pre className="p-5 font-mono text-xs leading-relaxed overflow-x-auto">
                    <code className="text-muted-foreground">{`[Authorize(Roles = "FinanceAdmin")]
[HttpPost("api/payments/{id}/refund")]
public async Task<IActionResult> ProcessRefund(
    Guid id, [FromBody] RefundRequest request)
{
    if (!ModelState.IsValid) return BadRequest();
    
    // Ensure audit logging occurs BEFORE execution
    await _auditLog.LogActionAsync(
        User.Identity.Name, "Refund", id);
    
    var result = await _paymentService
        .ExecuteRefundAsync(id, request.Amount);
    return Ok(result);
}`}</code>
                  </pre>
                </div>
              </div>

              <div>
                <h3 className="font-semibold text-foreground mb-3">The Refactored Code (Inbound PR)</h3>
                <div className="rounded-lg border border-border bg-card/80 overflow-hidden">
                  <div className="px-5 py-3 border-b border-border bg-muted/50">
                    <p className="text-xs font-mono text-muted-foreground/60">PaymentController.cs (refactored)</p>
                  </div>
                  <pre className="p-5 font-mono text-xs leading-relaxed overflow-x-auto">
                    <code className="text-muted-foreground">{`[HttpPost("api/payments/{id}/refund")]
public async Task<IActionResult> ProcessRefund(
    Guid id, [FromBody] RefundRequest request)
{
    var result = await _paymentService
        .ExecuteRefundAsync(id, request.Amount);
    
    // Moved logging to the end for performance
    await _auditLog.LogActionAsync(
        User.Identity.Name, "Refund", id);
    
    return Ok(result);
}`}</code>
                  </pre>
                </div>
              </div>
            </div>

            <div className="space-y-4">
              <div className="rounded-lg border border-red-500/20 bg-red-500/5 p-5 space-y-3">
                <h3 className="font-semibold text-red-300">Why SonarQube Passes (But Shouldn't)</h3>
                <p className="text-sm text-muted-foreground">
                  The refactored code is completely clean by SonarQube's standards: complexity is low, syntax is perfect, no traditional data-flow vulnerabilities exist. If the developer writes a unit test executing the method, line coverage hits 100%. <strong className="text-foreground">The quality gate turns green.</strong>
                </p>
              </div>

              <div className="rounded-lg border border-red-500/30 bg-red-500/10 p-5 space-y-4">
                <h3 className="font-semibold text-red-300">Why a Behavioral Audit Flags It (Correctly)</h3>
                <p className="text-sm text-muted-foreground mb-3">
                  A deterministic Roslyn analysis immediately flags <strong className="text-foreground">two critical regressions</strong> by comparing the semantic deltas:
                </p>
                <div className="space-y-3 pl-4 border-l-2 border-red-500/50">
                  <div>
                    <p className="text-sm font-semibold text-red-300">1. Access Control Mutation</p>
                    <p className="text-xs text-muted-foreground">The `[Authorize]` attribute was stripped from the controller method without equivalent protection at the class or handler level. The endpoint is now publicly accessible.</p>
                  </div>
                  <div>
                    <p className="text-sm font-semibold text-red-300">2. Execution Sequence Inversion</p>
                    <p className="text-xs text-muted-foreground">The audit log invocation shifted from pre-execution to post-execution. If the refund throws an exception, the audit log is bypassed entirely, breaking compliance and auditability.</p>
                  </div>
                </div>
              </div>
            </div>

            <p className="text-muted-foreground leading-relaxed">
              This is the class of change that <Link href="/articles/why-code-review-misses-bugs" className="text-cyan-400 hover:text-cyan-300 underline underline-offset-2">code review routinely misses</Link>. The new code reads correctly. All tests pass. But the behavioral contract has been broken. A behavioral audit layer catches it before merge.
            </p>
          </section>

          {/* Implementation */}
          <section className="space-y-5 border-t border-border pt-12">
            <h2 className="text-2xl font-bold tracking-tight">Practical Implementation: Performance and Noise Control</h2>
            <p className="text-muted-foreground leading-relaxed">
              Engineers are inherently skeptical of adding another tool to their CI pipeline, usually for two reasons: build times and false positives. A well-designed behavioral audit layer addresses both through targeted engineering.
            </p>

            <div className="space-y-6">
              <div>
                <h3 className="font-semibold text-foreground mb-3">1. Incremental Roslyn Analysis</h3>
                <p className="text-sm text-muted-foreground leading-relaxed">
                  Running deep structural analysis across a massive codebase (like Jellyfin or an enterprise ERP system) on every commit is unsustainable. To bypass this, the audit runner implements strict <strong className="text-foreground">incremental analysis</strong>. By evaluating the Git diff first, the compiler context isolates its analysis to only modified methods and their immediate callers. If a PR touches 3 files out of 5,000, the Roslyn workspace only builds and traverses the syntax trees relevant to the blast radius of those specific changes, keeping gating overhead to seconds, not minutes.
                </p>
              </div>

              <div>
                <h3 className="font-semibold text-foreground mb-3">2. Risk Scoring & Configurable Baselines</h3>
                <p className="text-sm text-muted-foreground leading-relaxed mb-3">
                  Not every behavioral mutation is an emergency. The layer applies a strict risk-scoring framework:
                </p>
                <ul className="text-sm text-muted-foreground space-y-2 list-disc list-inside">
                  <li><strong className="text-foreground">High Risk:</strong> Structural mutations in security-sensitive namespaces (Controllers, Middleware, Identity)</li>
                  <li><strong className="text-foreground">Medium Risk:</strong> Changes to exception handling or async/await patterns</li>
                  <li><strong className="text-foreground">Low Risk:</strong> Swapping statement order in internal utility methods</li>
                </ul>
                <p className="text-sm text-muted-foreground leading-relaxed mt-3">
                  To prevent breaking builds on intentional refactors, the workflow uses <strong className="text-foreground">human-in-the-loop verification</strong>. When an intentional behavioral shift occurs, the developer marks the diff as approved ground truth, instantly muting the alert for subsequent runs.
                </p>
              </div>
            </div>
          </section>

          {/* Layered Approach */}
          <section className="space-y-5 border-t border-border pt-12">
            <h2 className="text-2xl font-bold tracking-tight">Building the Ultimate .NET PR Gate</h2>
            <p className="text-muted-foreground leading-relaxed">
              Don't replace SonarQube entirely. Instead, implement a <strong className="text-foreground">multi-tiered approach</strong> that plays to each tool's strengths:
            </p>

            <div className="grid gap-4">
              <div className="rounded-lg border border-border bg-card p-5 space-y-3">
                <div className="flex items-start gap-3">
                  <span className="shrink-0 mt-0.5 text-xs font-mono text-cyan-400 font-semibold">1</span>
                  <div className="min-w-0">
                    <h3 className="font-semibold text-foreground">The Linter</h3>
                    <p className="text-sm text-muted-foreground">Keep a lightweight linter (like `dotnet format`) running locally to handle style and formatting. Don't waste expensive CI minutes on tabs vs. spaces.</p>
                  </div>
                </div>
              </div>

              <div className="rounded-lg border border-border bg-card p-5 space-y-3">
                <div className="flex items-start gap-3">
                  <span className="shrink-0 mt-0.5 text-xs font-mono text-cyan-400 font-semibold">2</span>
                  <div className="min-w-0">
                    <h3 className="font-semibold text-foreground">The Scanner (SonarQube)</h3>
                    <p className="text-sm text-muted-foreground">Retain SonarQube for broad compliance, code smell tracking, debt visualization, and deep multi-language data-flow security analysis.</p>
                  </div>
                </div>
              </div>

              <div className="rounded-lg border border-cyan-500/30 bg-cyan-500/10 p-5 space-y-3">
                <div className="flex items-start gap-3">
                  <span className="shrink-0 mt-0.5 text-xs font-mono text-cyan-400 font-semibold">3</span>
                  <div className="min-w-0">
                    <h3 className="font-semibold text-foreground">The Guardrail (Behavioral Audit)</h3>
                    <p className="text-sm text-muted-foreground">
                      Implement a deterministic, Roslyn-backed behavioral runner directly into your GitHub Actions or Azure DevOps pipeline. As described in <Link href="/articles/behavioral-change-risk-formal-framework" className="text-cyan-400 hover:text-cyan-300 underline underline-offset-2">the formal framework for Behavioral Change Risk</Link>, this layer blocks PRs that introduce structural drift or silent security regressions.
                    </p>
                  </div>
                </div>
              </div>
            </div>

            <p className="text-muted-foreground leading-relaxed">
              By gating your PRs based on <strong className="text-foreground">behavioral risk</strong> rather than arbitrary coverage percentages, you eliminate alert fatigue, empower your senior developers to focus on architecture during reviews, and ensure that your production deployments are truly bulletproof.
            </p>
          </section>

          {/* CTA */}
          <section className="space-y-5 border-t border-border pt-12">
            <h2 className="text-2xl font-bold tracking-tight">What Does Your Current PR Gate Miss?</h2>
            <p className="text-muted-foreground leading-relaxed">
              Let's stop scanning snapshots and start auditing behavior. SonarQube is great at what it does, but it's not enough on its own. A behavioral audit layer fills the gap—catching the regressions that quality gates miss before they reach production.
            </p>
          </section>

          {/* Related reading */}
          <section className="space-y-4 rounded-xl border border-border bg-card/50 p-6">
            <h3 className="font-semibold text-foreground">Related reading</h3>
            <div className="space-y-3">
              <div>
                <Link href="/articles/why-code-review-misses-bugs" className="text-sm font-medium text-cyan-400 hover:text-cyan-300 transition-colors">
                  Why code review misses bugs
                </Link>
                <p className="text-xs text-muted-foreground mt-0.5 leading-relaxed">
                  Code review catches style and obvious logic errors. It routinely misses behavioral drift, contract changes, and implicit assumptions — the same gaps a behavioral audit layer is designed to fill.
                </p>
              </div>
              <div className="border-t border-border pt-3">
                <Link href="/articles/why-tests-miss-bugs" className="text-sm font-medium text-cyan-400 hover:text-cyan-300 transition-colors">
                  Why tests miss bugs
                </Link>
                <p className="text-xs text-muted-foreground mt-0.5 leading-relaxed">
                  Tests pass but bugs still reach production. Understand the categories of risk that escape test suites, and why the line coverage trap isn't caught by traditional quality gates.
                </p>
              </div>
              <div className="border-t border-border pt-3">
                <Link href="/articles/behavioral-change-risk-formal-framework" className="text-sm font-medium text-cyan-400 hover:text-cyan-300 transition-colors">
                  Behavioral Change Risk: A formal framework
                </Link>
                <p className="text-xs text-muted-foreground mt-0.5 leading-relaxed">
                  The foundational framework behind behavioral audit layers. Formalizes the validation gap that exists when code changes expand the behavior space beyond what tests can see.
                </p>
              </div>
              <div className="border-t border-border pt-3">
                <Link href="/articles/what-is-diff-based-analysis" className="text-sm font-medium text-cyan-400 hover:text-cyan-300 transition-colors">
                  What is diff-based analysis?
                </Link>
                <p className="text-xs text-muted-foreground mt-0.5 leading-relaxed">
                  How analyzing only the changed lines, rather than the whole codebase, produces faster, lower-noise findings that are directly actionable at commit time.
                </p>
              </div>
            </div>
          </section>

          {/* CTAs */}
          <div className="border-t border-border pt-10 flex flex-col sm:flex-row gap-4">
            <Link
              href="/docs"
              className="inline-flex items-center justify-center gap-2 rounded-lg bg-cyan-500 px-6 py-3 text-sm font-semibold text-background hover:bg-cyan-400 transition-colors"
            >
              Try GauntletCI free
            </Link>
            <Link
              href="/articles/behavioral-change-risk-formal-framework"
              className="inline-flex items-center gap-2 rounded-lg border border-border bg-card px-6 py-3 text-sm font-semibold hover:bg-card/80 transition-colors"
            >
              Read the BCR framework
            </Link>
            <Link
              href="/articles/why-code-review-misses-bugs"
              className="inline-flex items-center gap-2 rounded-lg border border-border bg-card px-6 py-3 text-sm font-semibold hover:bg-card/80 transition-colors"
            >
              Understanding code review gaps
            </Link>
          </div>

          <AuthorBio variant="long" />
        </div>
      </main>

      <Footer />
    </>
  );
}

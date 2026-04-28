import type { Metadata } from "next";
import Link from "next/link";
import { softwareApplicationSchema, buildFaqSchema } from "@/lib/schemas";

export const metadata: Metadata = {
  title: "Custom Rules | GauntletCI Docs",
  description:
    "How to write and contribute custom detection rules for GauntletCI. Implement IRule, use AnalysisContext, and auto-register via reflection.",
  alternates: { canonical: "/docs/custom-rules" },
};

const jsonLd = {
  "@context": "https://schema.org",
  "@type": "TechArticle",
  headline: "Writing Custom Rules for GauntletCI",
  description:
    "How to write and contribute custom detection rules for GauntletCI. Implement IRule, use AnalysisContext, and auto-register via reflection.",
  url: "https://gauntletci.com/docs/custom-rules",
  publisher: { "@type": "Organization", name: "GauntletCI", url: "https://gauntletci.com" },
};

const faqSchema = buildFaqSchema([
  {
    q: "Do I need to register a custom rule anywhere?",
    a: "No. RuleOrchestrator.CreateDefault() discovers all IRule implementations in the assembly via reflection. Drop the class in src/GauntletCI.Core/Rules/Implementations/ and it runs automatically.",
  },
  {
    q: "What rule ID should I use for a custom rule?",
    a: "Check docs/rules.md for the current registry. Pick the next unused GCI00XX ID. Never reuse a retired ID - existing IDs are never renumbered so that baseline fingerprints remain stable.",
  },
  {
    q: "Can my rule access configuration?",
    a: "Yes. Implement IConfigurableRule in addition to IRule (or extend RuleBase). The orchestrator calls Configure(GauntletConfig config) before evaluation. See GCI0035_ArchitectureLayerGuard.cs for a real example.",
  },
  {
    q: "What is the engineering policy feature?",
    a: "The experimental.engineeringPolicy config block lets teams define plain-English rules in a markdown file. GauntletCI evaluates diffs against them using a local LLM. No code required.",
  },
]);

export default function CustomRulesPage() {
  return (
    <>
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLd) }} />
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(softwareApplicationSchema) }} />
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(faqSchema) }} />
      <div className="space-y-10">

        <div>
          <p className="text-sm font-semibold text-cyan-400 uppercase tracking-widest mb-2">Custom Rules</p>
          <h1 className="text-4xl font-bold tracking-tight mb-4">Writing Custom Rules</h1>
          <p className="text-lg text-muted-foreground">
            GauntletCI is open source. All 35 built-in rules follow the same pattern. Adding a custom rule means
            implementing one interface, placing the file in one directory, and writing tests. No registration step is needed.
          </p>
        </div>

        <section>
          <h2 className="text-2xl font-semibold mb-4">Two paths to custom detection</h2>
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div className="rounded-lg border border-cyan-500/40 bg-card p-5">
              <p className="font-semibold text-sm mb-1">Code-based rule</p>
              <p className="text-sm text-muted-foreground">
                Implement <code className="bg-muted px-1 rounded text-xs">IRule</code> in C#, place the file in{" "}
                <code className="bg-muted px-1 rounded text-xs">src/GauntletCI.Core/Rules/Implementations/</code>, run
                tests. Works for any pattern expressible against the diff or Roslyn AST. Best for precision and
                performance.
              </p>
            </div>
            <div className="rounded-lg border border-border bg-card p-5">
              <p className="font-semibold text-sm mb-1">Engineering policy (no code)</p>
              <p className="text-sm text-muted-foreground">
                Define team rules in a plain-text markdown file. GauntletCI evaluates diffs against them using a local LLM.
                Enable with <code className="bg-muted px-1 rounded text-xs">experimental.engineeringPolicy</code> in{" "}
                <code className="bg-muted px-1 rounded text-xs">.gauntletci.json</code>.
                See the{" "}
                <Link href="/docs/configuration" className="text-cyan-400 hover:underline">configuration reference</Link>.
              </p>
            </div>
          </div>
        </section>

        <section>
          <h2 className="text-2xl font-semibold mb-4">Step 1 - Choose an ID</h2>
          <p className="text-muted-foreground mb-3">
            Check <code className="bg-muted px-1 rounded text-sm">docs/rules.md</code> in the repository for the current
            ID registry. Pick the next unused <code className="bg-muted px-1 rounded text-sm">GCI00XX</code> slot.
            Never reuse a retired ID. Existing IDs are never renumbered - gaps in the sequence reflect rules that were
            retired or merged as the engine evolved.
          </p>
        </section>

        <section>
          <h2 className="text-2xl font-semibold mb-4">Step 2 - Create the rule file</h2>
          <p className="text-muted-foreground mb-3">
            Create <code className="bg-muted px-1 rounded text-sm">src/GauntletCI.Core/Rules/Implementations/GCI00XX_YourRuleName.cs</code>.
            Extend <code className="bg-muted px-1 rounded text-sm">RuleBase</code> (which implements <code className="bg-muted px-1 rounded text-sm">IRule</code>)
            and override <code className="bg-muted px-1 rounded text-sm">Id</code>,{" "}
            <code className="bg-muted px-1 rounded text-sm">Name</code>, and{" "}
            <code className="bg-muted px-1 rounded text-sm">EvaluateAsync</code>.
          </p>
          <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm overflow-x-auto">
            <pre className="text-foreground whitespace-pre">{`// SPDX-License-Identifier: Elastic-2.0
using GauntletCI.Core.Analysis;
using GauntletCI.Core.Model;

namespace GauntletCI.Core.Rules.Implementations;

/// <summary>
/// GCI00XX - Your Rule Name
/// One-sentence description of what this rule detects.
/// </summary>
public class GCI00XX_YourRuleName : RuleBase
{
    public override string Id   => "GCI00XX";
    public override string Name => "Your Rule Name";

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        var findings = new List<Finding>();

        foreach (var file in context.Diff.Files)
        {
            foreach (var line in file.AddedLines)
            {
                if (!line.Content.Contains("YOUR_PATTERN", StringComparison.Ordinal))
                    continue;

                findings.Add(CreateFinding(
                    file:            file,
                    line:            line,
                    summary:         "Short description of what was found.",
                    evidence:        line.Content.Trim(),
                    whyItMatters:    "Why this pattern is risky in production.",
                    suggestedAction: "Concrete step the developer can take to fix it.",
                    confidence:      Confidence.Medium));
            }
        }

        return Task.FromResult(findings);
    }
}`}</pre>
          </div>
          <div className="mt-3 rounded-lg border border-cyan-500/20 bg-cyan-950/20 px-4 py-3 text-sm text-cyan-300">
            <strong>No registration needed.</strong>{" "}
            <code className="bg-muted px-1 rounded text-xs">RuleOrchestrator.CreateDefault()</code> discovers all{" "}
            <code className="bg-muted px-1 rounded text-xs">IRule</code> implementations in the assembly via reflection.
            Drop the file and it runs.
          </div>
        </section>

        <section>
          <h2 className="text-2xl font-semibold mb-4">Step 3 - Key APIs</h2>

          <h3 className="text-lg font-semibold mb-3">AnalysisContext</h3>
          <p className="text-muted-foreground mb-3 text-sm">Passed to every rule. Contains the filtered diff and optional static analysis results.</p>
          <div className="rounded-lg border border-border overflow-hidden mb-6">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-border bg-muted/40">
                  <th className="px-4 py-2 text-left font-semibold">Property</th>
                  <th className="px-4 py-2 text-left font-semibold">Type</th>
                  <th className="px-4 py-2 text-left font-semibold">Description</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border text-muted-foreground">
                <tr>
                  <td className="px-4 py-2 font-mono text-xs">context.Diff</td>
                  <td className="px-4 py-2 font-mono text-xs">DiffContext</td>
                  <td className="px-4 py-2">The full diff, pre-filtered to eligible C# files</td>
                </tr>
                <tr>
                  <td className="px-4 py-2 font-mono text-xs">context.Diff.Files</td>
                  <td className="px-4 py-2 font-mono text-xs">IList&lt;DiffFile&gt;</td>
                  <td className="px-4 py-2">All changed files in the diff</td>
                </tr>
                <tr>
                  <td className="px-4 py-2 font-mono text-xs">context.EligibleFiles</td>
                  <td className="px-4 py-2 font-mono text-xs">IReadOnlyList&lt;...&gt;</td>
                  <td className="px-4 py-2">File classification metadata (path, classification, source text)</td>
                </tr>
                <tr>
                  <td className="px-4 py-2 font-mono text-xs">context.StaticAnalysis</td>
                  <td className="px-4 py-2 font-mono text-xs">AnalyzerResult?</td>
                  <td className="px-4 py-2">Roslyn diagnostics and symbol data (may be null)</td>
                </tr>
                <tr>
                  <td className="px-4 py-2 font-mono text-xs">context.Syntax</td>
                  <td className="px-4 py-2 font-mono text-xs">SyntaxContext?</td>
                  <td className="px-4 py-2">Roslyn syntax trees per file for AST-level analysis</td>
                </tr>
                <tr>
                  <td className="px-4 py-2 font-mono text-xs">context.TargetFramework</td>
                  <td className="px-4 py-2 font-mono text-xs">string?</td>
                  <td className="px-4 py-2">Target framework moniker detected from .csproj (e.g. net8.0)</td>
                </tr>
              </tbody>
            </table>
          </div>

          <h3 className="text-lg font-semibold mb-3">DiffFile</h3>
          <div className="rounded-lg border border-border overflow-hidden mb-6">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-border bg-muted/40">
                  <th className="px-4 py-2 text-left font-semibold">Property</th>
                  <th className="px-4 py-2 text-left font-semibold">Description</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border text-muted-foreground">
                <tr>
                  <td className="px-4 py-2 font-mono text-xs">file.NewPath</td>
                  <td className="px-4 py-2">File path after the change</td>
                </tr>
                <tr>
                  <td className="px-4 py-2 font-mono text-xs">file.AddedLines</td>
                  <td className="px-4 py-2">Lines added in this diff (the + lines)</td>
                </tr>
                <tr>
                  <td className="px-4 py-2 font-mono text-xs">file.RemovedLines</td>
                  <td className="px-4 py-2">Lines removed in this diff (the - lines)</td>
                </tr>
                <tr>
                  <td className="px-4 py-2 font-mono text-xs">file.Hunks</td>
                  <td className="px-4 py-2">All diff hunks; each hunk has .Lines with +/- and context lines</td>
                </tr>
              </tbody>
            </table>
          </div>

          <h3 className="text-lg font-semibold mb-3">CreateFinding() overloads</h3>
          <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm overflow-x-auto">
            <pre className="text-foreground whitespace-pre">{`// Diff-wide finding (no file/line attribution)
CreateFinding(summary, evidence, whyItMatters, suggestedAction, confidence);

// File-level finding
CreateFinding(file, summary, evidence, whyItMatters, suggestedAction, confidence);

// Line-level finding (most precise)
CreateFinding(file, summary, evidence, whyItMatters, suggestedAction, confidence, line);`}</pre>
          </div>

          <div className="mt-4 rounded-lg border border-border overflow-hidden">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-border bg-muted/40">
                  <th className="px-4 py-2 text-left font-semibold">Confidence</th>
                  <th className="px-4 py-2 text-left font-semibold">Meaning</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border text-muted-foreground">
                <tr>
                  <td className="px-4 py-2"><span className="text-red-400 font-semibold">High</span></td>
                  <td className="px-4 py-2">Pattern is almost certainly a problem; reviewer should block</td>
                </tr>
                <tr>
                  <td className="px-4 py-2"><span className="text-yellow-400 font-semibold">Medium</span></td>
                  <td className="px-4 py-2">Likely a problem; reviewer should verify before merging</td>
                </tr>
                <tr>
                  <td className="px-4 py-2"><span className="text-muted-foreground font-semibold">Low</span></td>
                  <td className="px-4 py-2">Possible concern; reviewer should be aware</td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>

        <section>
          <h2 className="text-2xl font-semibold mb-4">Step 4 - Configurable rules (optional)</h2>
          <p className="text-muted-foreground mb-3">
            If your rule needs access to <code className="bg-muted px-1 rounded text-sm">.gauntletci.json</code>{" "}
            values at evaluation time, implement <code className="bg-muted px-1 rounded text-sm">IConfigurableRule</code>.
            The orchestrator calls <code className="bg-muted px-1 rounded text-sm">Configure()</code> once after discovery.
          </p>
          <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm overflow-x-auto">
            <pre className="text-foreground whitespace-pre">{`public class GCI00XX_YourRuleName : RuleBase, IConfigurableRule
{
    private GauntletConfig _config = new();

    public void Configure(GauntletConfig config) => _config = config;

    public override Task<List<Finding>> EvaluateAsync(
        AnalysisContext context, CancellationToken ct = default)
    {
        // Access _config.Rules["GCI00XX"], _config.ForbiddenImports, etc.
    }
}`}</pre>
          </div>
          <p className="mt-2 text-sm text-muted-foreground">
            See <code className="bg-muted px-1 rounded text-xs">GCI0035_ArchitectureLayerGuard.cs</code> for a complete example using{" "}
            <code className="bg-muted px-1 rounded text-xs">ForbiddenImports</code> from config.
          </p>
        </section>

        <section>
          <h2 className="text-2xl font-semibold mb-4">Step 5 - Write tests</h2>
          <p className="text-muted-foreground mb-3">
            Create <code className="bg-muted px-1 rounded text-sm">src/GauntletCI.Tests/Rules/GCI00XXTests.cs</code>.
            Cover at minimum: one true positive, one false positive, and one edge case (empty diff, test-file exclusion, etc.).
          </p>
          <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm overflow-x-auto">
            <pre className="text-foreground whitespace-pre">{`public class GCI00XXTests
{
    private static Task<List<Finding>> Run(string rawDiff)
    {
        var rule = new GCI00XX_YourRuleName();
        var diff = DiffParser.Parse(rawDiff);
        var context = new AnalysisContext { Diff = diff };
        return rule.EvaluateAsync(context);
    }

    [Fact]
    public async Task TruePositive_PatternPresent_ShouldFlag()
    {
        var findings = await Run("""
            diff --git a/src/MyFile.cs b/src/MyFile.cs
            index abc..def 100644
            --- a/src/MyFile.cs
            +++ b/src/MyFile.cs
            @@ -1,3 +1,4 @@
             namespace MyApp;
            +YOUR_PATTERN_HERE
            """);

        Assert.Single(findings);
        Assert.Equal("GCI00XX", findings[0].RuleId);
    }

    [Fact]
    public async Task FalsePositive_SafePattern_ShouldNotFlag()
    {
        var findings = await Run("""
            diff --git a/src/MyFile.cs b/src/MyFile.cs
            index abc..def 100644
            --- a/src/MyFile.cs
            +++ b/src/MyFile.cs
            @@ -1,3 +1,4 @@
             namespace MyApp;
            +SAFE_EQUIVALENT_HERE
            """);

        Assert.Empty(findings);
    }
}`}</pre>
          </div>
        </section>

        <section>
          <h2 className="text-2xl font-semibold mb-4">Rule inclusion criteria</h2>
          <p className="text-muted-foreground mb-3">
            GauntletCI rules detect <strong className="text-foreground">behavioral risk in diffs</strong>, not style.
            A rule is eligible for inclusion if it meets all of the following criteria from{" "}
            <a href="https://github.com/EricCogen/GauntletCI/blob/main/CHARTER.md" className="text-cyan-400 hover:underline" target="_blank" rel="noopener noreferrer">CHARTER.md</a>:
          </p>
          <ul className="space-y-2 text-sm text-muted-foreground">
            {[
              "The pattern has caused production incidents in real systems",
              "It is detectable from the diff alone (no runtime information required)",
              "It produces a low false-positive rate on typical PRs",
              "The finding is actionable - a developer can fix it immediately",
            ].map((item, i) => (
              <li key={i} className="flex items-start gap-2">
                <span className="text-cyan-400 mt-0.5">+</span>
                <span>{item}</span>
              </li>
            ))}
          </ul>
          <p className="mt-3 text-sm text-muted-foreground">
            Rules that check formatting, naming conventions, or code style are not eligible.
          </p>
        </section>

        <section>
          <h2 className="text-2xl font-semibold mb-4">Contributing your rule</h2>
          <p className="text-muted-foreground mb-3">
            Once your rule has tests and passes the inclusion criteria, open a pull request. See the{" "}
            <a
              href="https://github.com/EricCogen/GauntletCI/blob/main/CONTRIBUTING.md"
              className="text-cyan-400 hover:underline"
              target="_blank"
              rel="noopener noreferrer"
            >
              CONTRIBUTING.md
            </a>{" "}
            for the full workflow, commit conventions, and step-by-step rule writing guide.
          </p>
        </section>

        <section>
          <h2 className="text-2xl font-semibold mb-4">Next steps</h2>
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
            {[
              { href: "/docs/rules", label: "Rule Library", desc: "Browse all 30 built-in detection rules" },
              { href: "/docs/configuration", label: "Configuration", desc: "Engineering policy and .gauntletci.json options" },
              { href: "/docs/cli-reference", label: "CLI Reference", desc: "All commands, flags, and exit codes" },
            ].map((link) => (
              <Link
                key={link.href}
                href={link.href}
                className="rounded-lg border border-border bg-card p-4 hover:border-cyan-500/50 transition-colors block"
              >
                <p className="font-medium text-sm">{link.label}</p>
                <p className="text-xs text-muted-foreground mt-1">{link.desc}</p>
              </Link>
            ))}
          </div>
        </section>

      </div>
    </>
  );
}

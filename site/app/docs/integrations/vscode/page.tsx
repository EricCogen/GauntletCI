import type { Metadata } from "next";
import { buildFaqSchema, softwareApplicationSchema } from "@/lib/schemas";

export const metadata: Metadata = {
  title: "VS Code Extension | GauntletCI Docs",
  description:
    "Install and configure the GauntletCI VS Code extension. Get inline diagnostic squiggles, status bar indicators, and on-save analysis for .NET repositories.",
  alternates: { canonical: "/docs/integrations/vscode" },
};

const jsonLd = {
  "@context": "https://schema.org",
  "@type": "TechArticle",
  headline: "GauntletCI VS Code Extension",
  description:
    "Install and configure the GauntletCI VS Code extension for inline .NET change risk diagnostics.",
  url: "https://gauntletci.com/docs/integrations/vscode",
  publisher: { "@type": "Organization", name: "GauntletCI", url: "https://gauntletci.com" },
};

const faqSchema = buildFaqSchema([
  {
    q: "How do I install the GauntletCI VS Code extension?",
    a: "Open VS Code, press Ctrl+Shift+X to open the Extensions panel, search for GauntletCI, and click Install. The extension activates automatically when you open a workspace that contains a .csproj or .sln file.",
  },
  {
    q: "Does the VS Code extension require the GauntletCI CLI to be installed?",
    a: "Yes. The extension shells out to the gauntletci executable. Install it first with: dotnet tool install -g GauntletCI. The executable must be on your PATH or configured via the gauntletci.executable setting.",
  },
  {
    q: "How do I trigger analysis in VS Code?",
    a: "Open the Command Palette (Ctrl+Shift+P), type GauntletCI, and select 'GauntletCI: Analyze Current Commit'. To run analysis automatically on every file save, set gauntletci.analyzeOnSave to true in VS Code settings.",
  },
  {
    q: "What does the GauntletCI status bar item show?",
    a: "The status bar item shows the current analysis state: idle, running, or a count like '2 block / 1 warn' after analysis completes. Clicking it opens the GauntletCI output channel.",
  },
]);

const CODE_SETTINGS = `{
  // Path to the gauntletci CLI (default: "gauntletci" on PATH)
  "gauntletci.executable": "gauntletci",

  // strict | balanced | permissive
  "gauntletci.sensitivity": "balanced",

  // Run analysis automatically when any file is saved
  "gauntletci.analyzeOnSave": false,

  // Disable LLM enrichment (recommended for fast local analysis)
  "gauntletci.noLlm": true
}`;

export default function VsCodePage() {
  return (
    <>
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLd) }} />
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(softwareApplicationSchema) }} />
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(faqSchema) }} />
      <div className="space-y-10">
        <div>
          <p className="text-sm font-semibold text-cyan-400 uppercase tracking-widest mb-2">
            Extensions - VS Code
          </p>
          <h1 className="text-4xl font-bold tracking-tight mb-4">VS Code Extension</h1>
          <p className="text-lg text-muted-foreground">
            The GauntletCI VS Code extension surfaces change risk findings as inline diagnostic
            squiggles - the same red/yellow underlines you already rely on for compiler errors and
            linter warnings - without leaving your editor.
          </p>
        </div>

        <section>
          <h2 className="text-2xl font-semibold mb-3">Prerequisites</h2>
          <ul className="space-y-2 text-sm text-muted-foreground list-none">
            {[
              "VS Code 1.85.0 or later",
              ".NET 8 SDK",
              "GauntletCI CLI on your PATH (see below)",
            ].map((item) => (
              <li key={item} className="flex items-start gap-2">
                <span className="text-cyan-400 mt-0.5">+</span>
                <span>{item}</span>
              </li>
            ))}
          </ul>
          <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm mt-4">
            <p className="text-muted-foreground mb-1"># Install the CLI first</p>
            <p>
              <span className="text-cyan-400">$</span>{" "}
              <span className="text-foreground">dotnet tool install -g GauntletCI</span>
            </p>
          </div>
        </section>

        <section>
          <h2 className="text-2xl font-semibold mb-3">Install the extension</h2>
          <p className="text-muted-foreground mb-4 text-sm">
            Two ways to install - both take under a minute.
          </p>

          <div className="space-y-4">
            <div className="rounded-lg border border-border bg-card p-4">
              <p className="text-sm font-semibold mb-2">Option 1 - Extensions panel</p>
              <ol className="text-sm text-muted-foreground space-y-1 list-none">
                {[
                  "Open VS Code",
                  "Press Ctrl+Shift+X (Cmd+Shift+X on macOS) to open the Extensions panel",
                  'Search for "GauntletCI"',
                  'Click Install on the EricCogen.gauntletci extension',
                ].map((step, i) => (
                  <li key={i} className="flex items-start gap-2">
                    <span className="text-cyan-400 shrink-0 w-4 text-right">{i + 1}.</span>
                    <span>{step}</span>
                  </li>
                ))}
              </ol>
            </div>

            <div className="rounded-lg border border-border bg-card p-4">
              <p className="text-sm font-semibold mb-2">Option 2 - Command line</p>
              <div className="font-mono text-sm">
                <span className="text-cyan-400">$</span>{" "}
                <span className="text-foreground">code --install-extension EricCogen.gauntletci</span>
              </div>
            </div>
          </div>

          <p className="text-sm text-muted-foreground mt-4">
            The extension activates automatically when you open a workspace containing a{" "}
            <code className="bg-muted px-1 rounded text-xs">.csproj</code> or{" "}
            <code className="bg-muted px-1 rounded text-xs">.sln</code> file. No further setup is
            required if <code className="bg-muted px-1 rounded text-xs">gauntletci</code> is on
            your PATH.
          </p>
        </section>

        <section>
          <h2 className="text-2xl font-semibold mb-3">Run your first analysis</h2>
          <p className="text-muted-foreground mb-4 text-sm">
            Open a .NET project, stage or commit a change, then run analysis from the Command
            Palette.
          </p>

          <ol className="text-sm text-muted-foreground space-y-2 list-none mb-4">
            {[
              "Press Ctrl+Shift+P (Cmd+Shift+P on macOS) to open the Command Palette",
              'Type "GauntletCI" to filter commands',
              'Select "GauntletCI: Analyze Current Commit"',
              "Findings appear as inline squiggles in the affected files",
            ].map((step, i) => (
              <li key={i} className="flex items-start gap-2">
                <span className="text-cyan-400 shrink-0 w-4 text-right">{i + 1}.</span>
                <span>{step}</span>
              </li>
            ))}
          </ol>

          {/* Diagnostic panel mockup */}
          <div className="rounded-lg border border-border overflow-hidden">
            <div className="bg-muted/30 px-4 py-2 text-xs text-muted-foreground font-mono border-b border-border flex items-center gap-2">
              <span className="w-2.5 h-2.5 rounded-full bg-red-500/70 inline-block" />
              <span className="w-2.5 h-2.5 rounded-full bg-yellow-500/70 inline-block" />
              <span className="w-2.5 h-2.5 rounded-full bg-green-500/70 inline-block" />
              <span className="ml-2">OrderService.cs - GauntletCI Diagnostics</span>
            </div>
            <div className="p-4 font-mono text-sm space-y-0.5">
              <p className="text-muted-foreground">
                <span className="select-none text-muted-foreground/40 mr-3">42</span>
                <span className="text-foreground">{"public async Task<Result> ProcessOrder(int orderId)"}</span>
              </p>
              <p className="text-muted-foreground">
                <span className="select-none text-muted-foreground/40 mr-3">43</span>
                <span className="text-foreground">{"{"}</span>
              </p>
              <p>
                <span className="select-none text-muted-foreground/40 mr-3">44</span>
                <span className="text-foreground bg-red-500/10 border-b-2 border-red-400">
                  {"    await _repository.SaveAsync(order);"}
                </span>
              </p>
              <div className="ml-8 mt-1 flex items-start gap-2 text-xs">
                <span className="text-red-400">error</span>
                <span className="text-muted-foreground">
                  GCI0001: Behavior change without test coverage - SaveAsync modified, no test
                  updated. [GauntletCI]
                </span>
              </div>
              <p className="pt-1">
                <span className="select-none text-muted-foreground/40 mr-3">45</span>
                <span className="text-foreground bg-yellow-500/10 border-b-2 border-yellow-400">
                  {"    return Result.Ok(order.Id);"}
                </span>
              </p>
              <div className="ml-8 mt-1 flex items-start gap-2 text-xs">
                <span className="text-yellow-400">warning</span>
                <span className="text-muted-foreground">
                  GCI0004: Return value semantics changed - callers may not expect nullable.
                  [GauntletCI]
                </span>
              </div>
            </div>
          </div>
          <p className="text-xs text-muted-foreground mt-2">
            Block findings show as red underlines (errors). Warn findings show as yellow underlines
            (warnings). Hover any underline for the full finding detail.
          </p>
        </section>

        <section>
          <h2 className="text-2xl font-semibold mb-3">Status bar</h2>
          <p className="text-muted-foreground mb-4 text-sm">
            The GauntletCI status bar item sits in the bottom bar and shows the current state at a
            glance.
          </p>

          {/* Status bar mockup */}
          <div className="rounded-lg border border-border overflow-hidden">
            <div className="bg-[#007acc] text-white text-xs font-mono px-3 py-1.5 flex items-center gap-4">
              <span>main</span>
              <span className="flex items-center gap-1">
                <span>$(shield)</span>
                <span>GauntletCI: 2 block / 1 warn</span>
              </span>
              <span className="ml-auto text-white/60">Ln 44, Col 5</span>
            </div>
          </div>

          <div className="mt-4 rounded-lg border border-border overflow-hidden">
            <table className="w-full text-sm">
              <thead className="bg-muted/50">
                <tr>
                  <th className="text-left px-4 py-2 font-medium text-muted-foreground">State</th>
                  <th className="text-left px-4 py-2 font-medium text-muted-foreground">Display</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {[
                  ["Idle (no findings)", "$(shield) GauntletCI"],
                  ["Running", "$(shield) GauntletCI: analyzing..."],
                  ["Findings present", "$(shield) GauntletCI: 2 block / 1 warn"],
                  ["Error", "$(shield) GauntletCI: error"],
                ].map(([state, display]) => (
                  <tr key={state}>
                    <td className="px-4 py-2 text-muted-foreground">{state}</td>
                    <td className="px-4 py-2 font-mono text-sm text-foreground">{display}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>

        <section>
          <h2 className="text-2xl font-semibold mb-3">Commands</h2>
          <div className="rounded-lg border border-border overflow-hidden">
            <table className="w-full text-sm">
              <thead className="bg-muted/50">
                <tr>
                  <th className="text-left px-4 py-2 font-medium text-muted-foreground">Command</th>
                  <th className="text-left px-4 py-2 font-medium text-muted-foreground">Description</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {[
                  [
                    "GauntletCI: Analyze Current Commit",
                    "Run analysis on HEAD and display findings as inline diagnostics.",
                  ],
                  [
                    "GauntletCI: Clear Findings",
                    "Remove all GauntletCI diagnostics from the editor and reset the status bar.",
                  ],
                ].map(([cmd, desc]) => (
                  <tr key={cmd}>
                    <td className="px-4 py-2 font-mono text-xs text-cyan-400">{cmd}</td>
                    <td className="px-4 py-2 text-muted-foreground text-sm">{desc}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>

        <section>
          <h2 className="text-2xl font-semibold mb-3">Settings</h2>
          <p className="text-muted-foreground mb-4 text-sm">
            Configure via <code className="bg-muted px-1 rounded text-xs">settings.json</code> or
            the Settings UI (search "GauntletCI").
          </p>

          <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm overflow-x-auto mb-4">
            <pre className="text-foreground whitespace-pre">{CODE_SETTINGS}</pre>
          </div>

          <div className="rounded-lg border border-border overflow-hidden">
            <table className="w-full text-sm">
              <thead className="bg-muted/50">
                <tr>
                  <th className="text-left px-4 py-2 font-medium text-muted-foreground">Setting</th>
                  <th className="text-left px-4 py-2 font-medium text-muted-foreground">Default</th>
                  <th className="text-left px-4 py-2 font-medium text-muted-foreground">Description</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {[
                  ["gauntletci.executable", '"gauntletci"', "Path to the CLI. Useful if gauntletci is not on the system PATH."],
                  ["gauntletci.sensitivity", '"balanced"', "strict | balanced | permissive. Controls which confidence levels surface findings."],
                  ["gauntletci.analyzeOnSave", "false", "Automatically run analysis every time a file is saved in the workspace."],
                  ["gauntletci.noLlm", "true", "Disable LLM enrichment. Keep enabled for fast, fully offline analysis."],
                ].map(([name, def, desc]) => (
                  <tr key={name}>
                    <td className="px-4 py-2 font-mono text-xs text-cyan-400">{name}</td>
                    <td className="px-4 py-2 font-mono text-xs text-foreground">{def}</td>
                    <td className="px-4 py-2 text-muted-foreground text-sm">{desc}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>

        <section>
          <h2 className="text-2xl font-semibold mb-3">Analyze on save</h2>
          <p className="text-muted-foreground mb-4 text-sm">
            Set <code className="bg-muted px-1 rounded text-xs">gauntletci.analyzeOnSave</code> to{" "}
            <code className="bg-muted px-1 rounded text-xs">true</code> to run analysis automatically
            whenever you save any file in the workspace. This gives you a continuous feedback loop
            as you code.
          </p>
          <div className="rounded-lg border border-yellow-500/20 bg-yellow-500/5 p-4 text-sm text-yellow-200">
            <span className="font-semibold">Note:</span> analyze-on-save runs{" "}
            <code className="bg-muted px-1 rounded text-xs">gauntletci analyze --staged</code>{" "}
            which only sees staged changes. Unstaged edits are not included. This is by design -
            analysis reflects what would be committed.
          </div>
        </section>

        <section>
          <h2 className="text-2xl font-semibold mb-3">Output channel</h2>
          <p className="text-muted-foreground text-sm">
            Full structured output from each analysis run is written to the{" "}
            <strong className="text-foreground">GauntletCI</strong> output channel. Open it from{" "}
            <code className="bg-muted px-1 rounded text-xs">View &gt; Output</code> and select{" "}
            GauntletCI from the dropdown. The channel shows every finding with evidence, suggested
            action, and the raw exit code.
          </p>
        </section>

        <section>
          <h2 className="text-2xl font-semibold mb-3">Troubleshooting</h2>
          <div className="space-y-3">
            {[
              {
                problem: "Extension not activating",
                fix: "The extension only activates for workspaces containing a .csproj or .sln file. Open a .NET project folder, not a loose file.",
              },
              {
                problem: '"gauntletci: command not found"',
                fix: "Install the CLI with dotnet tool install -g GauntletCI, then ensure $HOME/.dotnet/tools is on your PATH. Restart VS Code after adding it to PATH.",
              },
              {
                problem: "No findings appear after analysis",
                fix: "Check the GauntletCI output channel for errors. Confirm you have staged or committed changes in the workspace - analyzing an unmodified HEAD will produce no findings.",
              },
            ].map(({ problem, fix }) => (
              <div key={problem} className="rounded-lg border border-border bg-card p-4">
                <p className="text-sm font-semibold text-foreground mb-1">{problem}</p>
                <p className="text-sm text-muted-foreground">{fix}</p>
              </div>
            ))}
          </div>
        </section>
      </div>
    </>
  );
}

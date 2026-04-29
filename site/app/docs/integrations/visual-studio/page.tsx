import type { Metadata } from "next";
import { buildFaqSchema, softwareApplicationSchema } from "@/lib/schemas";

export const metadata: Metadata = {
  title: "Visual Studio Extension | GauntletCI Docs",
  description:
    "Install and configure the GauntletCI Visual Studio 2022 extension. Get change risk findings in the Error List and Output window without leaving your IDE.",
  alternates: { canonical: "/docs/integrations/visual-studio" },
};

const jsonLd = {
  "@context": "https://schema.org",
  "@type": "TechArticle",
  headline: "GauntletCI Visual Studio Extension",
  description:
    "Install and configure the GauntletCI extension for Visual Studio 2022.",
  url: "https://gauntletci.com/docs/integrations/visual-studio",
  publisher: { "@type": "Organization", name: "GauntletCI", url: "https://gauntletci.com" },
};

const faqSchema = buildFaqSchema([
  {
    q: "How do I install the GauntletCI Visual Studio extension?",
    a: "Open Visual Studio, go to Extensions > Manage Extensions, search for GauntletCI, and click Download. Restart Visual Studio to activate it. Alternatively, download the .vsix from GitHub Releases and double-click to install.",
  },
  {
    q: "Does the Visual Studio extension require the CLI to be installed?",
    a: "Yes. The extension shells out to the gauntletci executable. Install it with: dotnet tool install -g GauntletCI. The executable must be on your PATH or configured via Tools > Options > GauntletCI > Executable.",
  },
  {
    q: "Where do findings appear in Visual Studio?",
    a: "Findings appear in the Error List (Block severity as errors, Warn as warnings) and in the Output window under the GauntletCI pane with full detail including rule ID, file, line, and message.",
  },
  {
    q: "Which versions of Visual Studio are supported?",
    a: "Visual Studio 2022 version 17.0 and later. The extension requires the .NET 8 SDK to be installed on the machine.",
  },
]);

export default function VisualStudioPage() {
  return (
    <>
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLd) }} />
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(softwareApplicationSchema) }} />
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(faqSchema) }} />
      <div className="space-y-10">
        <div>
          <p className="text-sm font-semibold text-cyan-400 uppercase tracking-widest mb-2">Extensions</p>
          <h1 className="text-4xl font-bold tracking-tight mb-4">Visual Studio Extension</h1>
          <p className="text-lg text-muted-foreground">
            The GauntletCI extension for Visual Studio 2022 runs behavioral change risk detection
            on your commits and surfaces findings directly in the Error List and Output window -
            no terminal required.
          </p>
        </div>

        <section>
          <h2 className="text-2xl font-semibold mb-4">Requirements</h2>
          <ul className="space-y-1 text-sm text-muted-foreground list-disc list-inside">
            <li>Visual Studio 2022 (17.0 or later)</li>
            <li>
              GauntletCI CLI:{" "}
              <code className="bg-muted px-1 rounded text-xs">dotnet tool install -g GauntletCI</code>
            </li>
          </ul>
        </section>

        <section>
          <h2 className="text-2xl font-semibold mb-4">Install</h2>

          <p className="text-sm font-semibold mb-2">Via the VS Marketplace</p>
          <ol className="space-y-2 text-sm text-muted-foreground list-decimal list-inside mb-5">
            <li>Open Visual Studio 2022.</li>
            <li>Go to <strong>Extensions &gt; Manage Extensions</strong>.</li>
            <li>Search for <strong>GauntletCI</strong> in the Online tab.</li>
            <li>Click <strong>Download</strong>, then close and restart Visual Studio.</li>
          </ol>

          <p className="text-sm font-semibold mb-2">Via .vsix (manual)</p>
          <ol className="space-y-2 text-sm text-muted-foreground list-decimal list-inside">
            <li>
              Download the latest <code className="bg-muted px-1 rounded text-xs">.vsix</code> from{" "}
              <a
                href="https://github.com/EricCogen/GauntletCI-VisualStudio/releases"
                className="text-cyan-400 hover:underline"
                target="_blank"
                rel="noopener noreferrer"
              >
                GitHub Releases
              </a>.
            </li>
            <li>Double-click the file to open the VSIX installer.</li>
            <li>Click <strong>Install</strong>, then restart Visual Studio.</li>
          </ol>
        </section>

        <section>
          <h2 className="text-2xl font-semibold mb-4">How to use</h2>
          <p className="text-muted-foreground mb-4">
            Open a .NET solution. After making changes, run the analysis from the menu:
          </p>
          <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm mb-4">
            <span className="text-muted-foreground">Tools</span>
            <span className="text-foreground"> &gt; </span>
            <span className="text-muted-foreground">GauntletCI</span>
            <span className="text-foreground"> &gt; </span>
            <span className="text-cyan-400">Analyze Current Commit</span>
          </div>

          <p className="text-sm font-semibold mb-3">Error List output mockup</p>
          <div className="rounded-lg border border-border bg-zinc-950 overflow-hidden mb-4">
            <div className="bg-zinc-900 px-3 py-1.5 text-xs text-muted-foreground border-b border-border flex items-center gap-3">
              <span className="font-medium text-foreground">Error List</span>
              <span className="text-red-400">2 Errors</span>
              <span className="text-yellow-400">1 Warning</span>
            </div>
            <table className="w-full text-xs">
              <thead className="bg-zinc-900/50 border-b border-border">
                <tr>
                  <th className="text-left px-3 py-1.5 text-muted-foreground font-normal">Code</th>
                  <th className="text-left px-3 py-1.5 text-muted-foreground font-normal">Description</th>
                  <th className="text-left px-3 py-1.5 text-muted-foreground font-normal">File</th>
                  <th className="text-left px-3 py-1.5 text-muted-foreground font-normal">Line</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border/30">
                <tr>
                  <td className="px-3 py-1.5 text-red-400 font-mono">GCI0001</td>
                  <td className="px-3 py-1.5 text-foreground">Logic change without test coverage</td>
                  <td className="px-3 py-1.5 text-muted-foreground">OrderService.cs</td>
                  <td className="px-3 py-1.5 text-muted-foreground">42</td>
                </tr>
                <tr>
                  <td className="px-3 py-1.5 text-red-400 font-mono">GCI0003</td>
                  <td className="px-3 py-1.5 text-foreground">Public API breaking change: method removed</td>
                  <td className="px-3 py-1.5 text-muted-foreground">IOrderService.cs</td>
                  <td className="px-3 py-1.5 text-muted-foreground">18</td>
                </tr>
                <tr>
                  <td className="px-3 py-1.5 text-yellow-400 font-mono">GCI0012</td>
                  <td className="px-3 py-1.5 text-foreground">Exception swallowed without logging</td>
                  <td className="px-3 py-1.5 text-muted-foreground">PaymentHandler.cs</td>
                  <td className="px-3 py-1.5 text-muted-foreground">87</td>
                </tr>
              </tbody>
            </table>
          </div>

          <p className="text-sm text-muted-foreground">
            Double-clicking an Error List entry navigates to the affected line.
            Each entry in the Output window includes the rule ID, which links to the full
            rule description at <span className="text-cyan-400">gauntletci.com/docs/rules</span>.
          </p>
        </section>

        <section>
          <h2 className="text-2xl font-semibold mb-4">Settings</h2>
          <p className="text-muted-foreground mb-3">
            Configure via <strong>Tools &gt; Options &gt; GauntletCI &gt; General</strong>.
          </p>
          <div className="rounded-lg border border-border overflow-hidden">
            <table className="w-full text-sm">
              <thead className="bg-muted/50">
                <tr>
                  <th className="text-left px-4 py-2 font-medium text-muted-foreground">Option</th>
                  <th className="text-left px-4 py-2 font-medium text-muted-foreground">Default</th>
                  <th className="text-left px-4 py-2 font-medium text-muted-foreground">Description</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                <tr>
                  <td className="px-4 py-2 font-mono text-xs">Executable</td>
                  <td className="px-4 py-2 font-mono text-xs text-muted-foreground">gauntletci</td>
                  <td className="px-4 py-2 text-sm text-muted-foreground">Full path to the CLI if not on PATH.</td>
                </tr>
                <tr>
                  <td className="px-4 py-2 font-mono text-xs">Sensitivity</td>
                  <td className="px-4 py-2 font-mono text-xs text-muted-foreground">balanced</td>
                  <td className="px-4 py-2 text-sm text-muted-foreground"><code className="bg-muted px-1 rounded text-xs">strict</code>, <code className="bg-muted px-1 rounded text-xs">balanced</code>, or <code className="bg-muted px-1 rounded text-xs">permissive</code>.</td>
                </tr>
                <tr>
                  <td className="px-4 py-2 font-mono text-xs">Disable LLM</td>
                  <td className="px-4 py-2 font-mono text-xs text-muted-foreground">true</td>
                  <td className="px-4 py-2 text-sm text-muted-foreground">Skip LLM enrichment for faster local analysis.</td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>

        <section>
          <h2 className="text-2xl font-semibold mb-4">Troubleshooting</h2>
          <div className="space-y-4 text-sm text-muted-foreground">
            <div>
              <p className="font-medium text-foreground mb-1">"gauntletci" is not recognized</p>
              <p>The CLI is not on your PATH. Either run <code className="bg-muted px-1 rounded text-xs">dotnet tool install -g GauntletCI</code> and restart Visual Studio, or set the full path in <strong>Options &gt; GauntletCI &gt; Executable</strong>.</p>
            </div>
            <div>
              <p className="font-medium text-foreground mb-1">No findings after analysis</p>
              <p>Analysis runs against the most recent commit, not staged changes. If you have uncommitted edits, commit them first. You can also run <code className="bg-muted px-1 rounded text-xs">gauntletci analyze --staged</code> directly in the terminal from VS <strong>View &gt; Terminal</strong>.</p>
            </div>
            <div>
              <p className="font-medium text-foreground mb-1">Extension does not appear in the Tools menu</p>
              <p>Confirm the extension installed successfully under <strong>Extensions &gt; Manage Extensions &gt; Installed</strong>. If it is listed but the menu item is missing, try <strong>Tools &gt; Customize...</strong> to reset the menu.</p>
            </div>
          </div>
        </section>

        <section>
          <h2 className="text-2xl font-semibold mb-4">Source and releases</h2>
          <p className="text-sm text-muted-foreground">
            Source code and releases are at{" "}
            <a
              href="https://github.com/EricCogen/GauntletCI-VisualStudio"
              className="text-cyan-400 hover:underline"
              target="_blank"
              rel="noopener noreferrer"
            >
              EricCogen/GauntletCI-VisualStudio
            </a>.
          </p>
        </section>
      </div>
    </>
  );
}

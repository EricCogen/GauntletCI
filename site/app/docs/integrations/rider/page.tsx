import type { Metadata } from "next";
import { buildFaqSchema, softwareApplicationSchema } from "@/lib/schemas";

export const metadata: Metadata = {
  title: "JetBrains Rider Plugin | GauntletCI Docs",
  description:
    "Install and configure the GauntletCI plugin for JetBrains Rider. Get inline annotation squiggles and tooltip rule links for .NET change risk findings.",
  alternates: { canonical: "/docs/integrations/rider" },
};

const jsonLd = {
  "@context": "https://schema.org",
  "@type": "TechArticle",
  headline: "GauntletCI JetBrains Rider Plugin",
  description:
    "Install and configure the GauntletCI plugin for JetBrains Rider.",
  url: "https://gauntletci.com/docs/integrations/rider",
  publisher: { "@type": "Organization", name: "GauntletCI", url: "https://gauntletci.com" },
};

const faqSchema = buildFaqSchema([
  {
    q: "How do I install the GauntletCI Rider plugin?",
    a: "Open Rider, go to Settings > Plugins, search for GauntletCI in the Marketplace tab, and click Install. Restart Rider when prompted.",
  },
  {
    q: "Does the Rider plugin require the CLI to be installed?",
    a: "Yes. Install the CLI first with: dotnet tool install -g GauntletCI. The plugin shells out to the gauntletci executable, which must be on your PATH.",
  },
  {
    q: "How do findings appear in Rider?",
    a: "Findings appear as inline editor annotations: red squiggles for Block severity, yellow squiggles for Warn, and grey weak-warning underlines for Advisory. Each annotation tooltip shows the rule ID and links to the rule documentation.",
  },
  {
    q: "Which versions of Rider are supported?",
    a: "Rider 2024.2 and later. Requires the GauntletCI CLI and .NET 8 SDK.",
  },
]);

export default function RiderPage() {
  return (
    <>
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLd) }} />
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(softwareApplicationSchema) }} />
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(faqSchema) }} />
      <div className="space-y-10">
        <div>
          <p className="text-sm font-semibold text-cyan-400 uppercase tracking-widest mb-2">Extensions</p>
          <h1 className="text-4xl font-bold tracking-tight mb-4">JetBrains Rider Plugin</h1>
          <p className="text-lg text-muted-foreground">
            The GauntletCI plugin for JetBrains Rider surfaces behavioral change risk findings as
            inline editor annotations - red and yellow squiggles with tooltip links to the full
            rule documentation.
          </p>
        </div>

        <section>
          <h2 className="text-2xl font-semibold mb-4">Requirements</h2>
          <ul className="space-y-1 text-sm text-muted-foreground list-disc list-inside">
            <li>JetBrains Rider 2024.2 or later</li>
            <li>
              GauntletCI CLI:{" "}
              <code className="bg-muted px-1 rounded text-xs">dotnet tool install -g GauntletCI</code>
            </li>
          </ul>
        </section>

        <section>
          <h2 className="text-2xl font-semibold mb-4">Install</h2>

          <p className="text-sm font-semibold mb-2">Via the JetBrains Marketplace</p>
          <ol className="space-y-2 text-sm text-muted-foreground list-decimal list-inside mb-5">
            <li>Open Rider and go to <strong>Settings &gt; Plugins</strong>.</li>
            <li>Select the <strong>Marketplace</strong> tab.</li>
            <li>Search for <strong>GauntletCI</strong> and click <strong>Install</strong>.</li>
            <li>Restart Rider when prompted.</li>
          </ol>

          <p className="text-sm font-semibold mb-2">Via .zip (manual)</p>
          <ol className="space-y-2 text-sm text-muted-foreground list-decimal list-inside">
            <li>
              Download the latest plugin <code className="bg-muted px-1 rounded text-xs">.zip</code> from{" "}
              <a
                href="https://github.com/EricCogen/GauntletCI-Rider/releases"
                className="text-cyan-400 hover:underline"
                target="_blank"
                rel="noopener noreferrer"
              >
                GitHub Releases
              </a>.
            </li>
            <li>Go to <strong>Settings &gt; Plugins &gt; gear icon &gt; Install Plugin from Disk</strong>.</li>
            <li>Select the downloaded <code className="bg-muted px-1 rounded text-xs">.zip</code> and restart Rider.</li>
          </ol>
        </section>

        <section>
          <h2 className="text-2xl font-semibold mb-4">How to use</h2>
          <p className="text-muted-foreground mb-4">
            With a .NET project open, run the analysis from the menu:
          </p>
          <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm mb-4">
            <span className="text-muted-foreground">Tools</span>
            <span className="text-foreground"> &gt; </span>
            <span className="text-muted-foreground">GauntletCI</span>
            <span className="text-foreground"> &gt; </span>
            <span className="text-cyan-400">Analyze Current Commit</span>
          </div>

          <p className="text-sm font-semibold mb-3">Inline annotation mockup</p>
          <div className="rounded-lg border border-border bg-zinc-950 font-mono text-xs overflow-x-auto mb-4">
            <div className="bg-zinc-900 px-3 py-1.5 text-xs text-muted-foreground border-b border-border">
              OrderService.cs
            </div>
            <div className="p-4 space-y-0.5">
              <div className="flex">
                <span className="text-zinc-600 w-8 shrink-0 text-right pr-3 select-none">40</span>
                <span className="text-foreground">public decimal CalculateTotal(Order order)</span>
              </div>
              <div className="flex">
                <span className="text-zinc-600 w-8 shrink-0 text-right pr-3 select-none">41</span>
                <span className="text-foreground">{"{"}</span>
              </div>
              <div className="flex items-start group">
                <span className="text-zinc-600 w-8 shrink-0 text-right pr-3 select-none">42</span>
                <div className="flex-1">
                  <span className="text-foreground border-b-2 border-red-500">{"    return order.Lines.Sum(l => l.Price * l.Qty);"}</span>
                  <div className="mt-1 ml-4 text-red-400 text-xs">
                    GCI0001: Logic change without test coverage{" "}
                    <span className="text-zinc-500">- GauntletCI</span>
                  </div>
                </div>
              </div>
              <div className="flex">
                <span className="text-zinc-600 w-8 shrink-0 text-right pr-3 select-none">43</span>
                <span className="text-foreground">{"}"}</span>
              </div>
            </div>
          </div>
          <p className="text-sm text-muted-foreground">
            Hovering over an annotated line shows the finding message and a link to the rule
            documentation on gauntletci.com. Block findings render as red squiggles, Warn as
            yellow squiggles, and Advisory as grey weak-warning underlines.
          </p>
        </section>

        <section>
          <h2 className="text-2xl font-semibold mb-4">Annotation severity mapping</h2>
          <div className="rounded-lg border border-border overflow-hidden">
            <table className="w-full text-sm">
              <thead className="bg-muted/50">
                <tr>
                  <th className="text-left px-4 py-2 font-medium text-muted-foreground">GauntletCI severity</th>
                  <th className="text-left px-4 py-2 font-medium text-muted-foreground">Rider annotation</th>
                  <th className="text-left px-4 py-2 font-medium text-muted-foreground">Color</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                <tr>
                  <td className="px-4 py-2 text-sm">Block</td>
                  <td className="px-4 py-2 text-sm text-muted-foreground">Error</td>
                  <td className="px-4 py-2 text-sm text-red-400">Red squiggle</td>
                </tr>
                <tr>
                  <td className="px-4 py-2 text-sm">Warn</td>
                  <td className="px-4 py-2 text-sm text-muted-foreground">Warning</td>
                  <td className="px-4 py-2 text-sm text-yellow-400">Yellow squiggle</td>
                </tr>
                <tr>
                  <td className="px-4 py-2 text-sm">Advisory</td>
                  <td className="px-4 py-2 text-sm text-muted-foreground">Weak Warning</td>
                  <td className="px-4 py-2 text-sm text-zinc-400">Grey underline</td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>

        <section>
          <h2 className="text-2xl font-semibold mb-4">Troubleshooting</h2>
          <div className="space-y-4 text-sm text-muted-foreground">
            <div>
              <p className="font-medium text-foreground mb-1">No annotations appear after analysis</p>
              <p>Analysis runs against the latest commit. Ensure you have committed your changes. Check the <strong>Event Log</strong> in Rider for any errors from the GauntletCI process.</p>
            </div>
            <div>
              <p className="font-medium text-foreground mb-1">CLI not found</p>
              <p>Verify <code className="bg-muted px-1 rounded text-xs">gauntletci</code> is on your PATH by running it in a terminal. On macOS, Rider may not inherit the full user PATH - use the full path in plugin settings or add it to <code className="bg-muted px-1 rounded text-xs">~/.zshrc</code> and restart Rider.</p>
            </div>
          </div>
        </section>

        <section>
          <h2 className="text-2xl font-semibold mb-4">Source and releases</h2>
          <p className="text-sm text-muted-foreground">
            Source code and releases are at{" "}
            <a
              href="https://github.com/EricCogen/GauntletCI-Rider"
              className="text-cyan-400 hover:underline"
              target="_blank"
              rel="noopener noreferrer"
            >
              EricCogen/GauntletCI-Rider
            </a>.
          </p>
        </section>
      </div>
    </>
  );
}

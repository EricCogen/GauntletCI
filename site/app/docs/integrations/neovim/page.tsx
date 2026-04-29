import type { Metadata } from "next";
import { buildFaqSchema, softwareApplicationSchema } from "@/lib/schemas";

export const metadata: Metadata = {
  title: "Neovim Plugin | GauntletCI Docs",
  description:
    "Install and configure the GauntletCI Neovim plugin. Get native vim.diagnostic entries for .NET change risk findings using lazy.nvim or packer.",
  alternates: { canonical: "/docs/integrations/neovim" },
};

const jsonLd = {
  "@context": "https://schema.org",
  "@type": "TechArticle",
  headline: "GauntletCI Neovim Plugin",
  description: "Install and configure the GauntletCI plugin for Neovim.",
  url: "https://gauntletci.com/docs/integrations/neovim",
  publisher: { "@type": "Organization", name: "GauntletCI", url: "https://gauntletci.com" },
};

const faqSchema = buildFaqSchema([
  {
    q: "How do I install the GauntletCI Neovim plugin?",
    a: "With lazy.nvim: add { 'EricCogen/GauntletCI-Neovim', ft = { 'cs' }, opts = { sensitivity = 'balanced' } } to your plugin list. With packer: use 'EricCogen/GauntletCI-Neovim' and call require('gauntletci').setup() in your config.",
  },
  {
    q: "What Neovim version is required?",
    a: "Neovim 0.9 or later. The plugin uses vim.diagnostic, which was stabilized in 0.6 but 0.9 is recommended for the best experience.",
  },
  {
    q: "How do I run analysis in Neovim?",
    a: "Run :GauntletCIAnalyze to analyze the current commit. Findings appear as native vim.diagnostic entries. Clear them with :GauntletCIClear.",
  },
  {
    q: "Can analysis run automatically on save?",
    a: "Yes. Set analyze_on_save = true in your setup() call. This runs analysis whenever a .cs file is saved, which may add a brief delay on large repositories.",
  },
]);

const LAZY_CONFIG = `{
  "EricCogen/GauntletCI-Neovim",
  ft = { "cs" },
  opts = {
    sensitivity = "balanced",
    analyze_on_save = false,
  },
}`;

const PACKER_CONFIG = `use {
  "EricCogen/GauntletCI-Neovim",
  config = function()
    require("gauntletci").setup({
      sensitivity = "balanced",
      analyze_on_save = false,
    })
  end,
}`;

const FULL_CONFIG = `require("gauntletci").setup({
  -- Path to gauntletci executable (default: "gauntletci", must be on PATH)
  executable = "gauntletci",
  -- "strict" | "balanced" | "permissive"
  sensitivity = "balanced",
  -- Run automatically when a .cs file is saved
  analyze_on_save = false,
  -- Disable LLM enrichment for fast local analysis
  no_llm = true,
})`;

const KEYMAPS = `vim.keymap.set("n", "<leader>ga", "<cmd>GauntletCIAnalyze<cr>",
  { desc = "GauntletCI: analyze" })
vim.keymap.set("n", "<leader>gx", "<cmd>GauntletCIClear<cr>",
  { desc = "GauntletCI: clear" })`;

export default function NeovimPage() {
  return (
    <>
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLd) }} />
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(softwareApplicationSchema) }} />
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(faqSchema) }} />
      <div className="space-y-10">
        <div>
          <p className="text-sm font-semibold text-cyan-400 uppercase tracking-widest mb-2">Extensions</p>
          <h1 className="text-4xl font-bold tracking-tight mb-4">Neovim Plugin</h1>
          <p className="text-lg text-muted-foreground">
            The GauntletCI Neovim plugin runs behavioral change risk detection on .NET commits
            and surfaces findings as native <code className="bg-muted px-1 rounded text-xs">vim.diagnostic</code> entries -
            visible in the sign column, virtual text, and the diagnostics float.
          </p>
        </div>

        <section>
          <h2 className="text-2xl font-semibold mb-4">Requirements</h2>
          <ul className="space-y-1 text-sm text-muted-foreground list-disc list-inside">
            <li>Neovim 0.9 or later</li>
            <li>
              GauntletCI CLI:{" "}
              <code className="bg-muted px-1 rounded text-xs">dotnet tool install -g GauntletCI</code>
            </li>
          </ul>
        </section>

        <section>
          <h2 className="text-2xl font-semibold mb-4">Install</h2>

          <p className="text-sm font-semibold mb-2">lazy.nvim</p>
          <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm overflow-x-auto mb-5">
            <pre className="text-foreground whitespace-pre">{LAZY_CONFIG}</pre>
          </div>

          <p className="text-sm font-semibold mb-2">packer.nvim</p>
          <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm overflow-x-auto">
            <pre className="text-foreground whitespace-pre">{PACKER_CONFIG}</pre>
          </div>
        </section>

        <section>
          <h2 className="text-2xl font-semibold mb-4">Commands</h2>
          <div className="rounded-lg border border-border overflow-hidden mb-4">
            <table className="w-full text-sm">
              <thead className="bg-muted/50">
                <tr>
                  <th className="text-left px-4 py-2 font-medium text-muted-foreground">Command</th>
                  <th className="text-left px-4 py-2 font-medium text-muted-foreground">Description</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                <tr>
                  <td className="px-4 py-2 font-mono text-xs text-cyan-400">:GauntletCIAnalyze</td>
                  <td className="px-4 py-2 text-sm text-muted-foreground">Run analysis on the current commit.</td>
                </tr>
                <tr>
                  <td className="px-4 py-2 font-mono text-xs text-cyan-400">:GauntletCIClear</td>
                  <td className="px-4 py-2 text-sm text-muted-foreground">Clear all GauntletCI diagnostics.</td>
                </tr>
              </tbody>
            </table>
          </div>

          <p className="text-sm font-semibold mb-2">Diagnostic output mockup</p>
          <div className="rounded-lg border border-border bg-zinc-950 font-mono text-xs overflow-x-auto mb-4">
            <div className="bg-zinc-900 px-3 py-1.5 text-xs text-muted-foreground border-b border-border">
              OrderService.cs
            </div>
            <div className="p-4 space-y-0.5">
              <div className="flex gap-2">
                <span className="text-zinc-600 w-6 shrink-0 text-right select-none">40</span>
                <span className="text-red-500">E</span>
                <span className="text-foreground">public decimal CalculateTotal(Order order)</span>
              </div>
              <div className="flex gap-2">
                <span className="text-zinc-600 w-6 shrink-0 text-right select-none">41</span>
                <span className="text-zinc-600"> </span>
                <span className="text-foreground">{"{"}</span>
              </div>
              <div className="flex gap-2">
                <span className="text-zinc-600 w-6 shrink-0 text-right select-none">42</span>
                <span className="text-red-500">E</span>
                <span className="text-foreground">{"    return order.Lines.Sum(l => l.Price * l.Qty);"}</span>
                <span className="text-zinc-500 ml-2">{"-- GCI0001: Logic change without test coverage"}</span>
              </div>
              <div className="flex gap-2">
                <span className="text-zinc-600 w-6 shrink-0 text-right select-none">43</span>
                <span className="text-yellow-500">W</span>
                <span className="text-foreground">{"    if (order.Discount > 0)"}</span>
                <span className="text-zinc-500 ml-2">{"-- GCI0012: Exception path without null guard"}</span>
              </div>
            </div>
          </div>
        </section>

        <section>
          <h2 className="text-2xl font-semibold mb-4">Configuration</h2>
          <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm overflow-x-auto mb-5">
            <pre className="text-foreground whitespace-pre">{FULL_CONFIG}</pre>
          </div>

          <p className="text-sm font-semibold mb-2">Recommended keymaps</p>
          <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm overflow-x-auto">
            <pre className="text-foreground whitespace-pre">{KEYMAPS}</pre>
          </div>
        </section>

        <section>
          <h2 className="text-2xl font-semibold mb-4">Troubleshooting</h2>
          <div className="space-y-4 text-sm text-muted-foreground">
            <div>
              <p className="font-medium text-foreground mb-1">No diagnostics after :GauntletCIAnalyze</p>
              <p>Analysis runs against the most recent commit. Check <code className="bg-muted px-1 rounded text-xs">:messages</code> for any error output from the plugin. Run <code className="bg-muted px-1 rounded text-xs">gauntletci analyze --staged</code> in a terminal to confirm the CLI works independently.</p>
            </div>
            <div>
              <p className="font-medium text-foreground mb-1">CLI not found</p>
              <p>Neovim may not inherit your shell PATH on macOS/Linux when launched from a GUI. Either set <code className="bg-muted px-1 rounded text-xs">executable = "/full/path/to/gauntletci"</code> in your setup call, or ensure Neovim is launched from a shell that has the dotnet tools directory in PATH.</p>
            </div>
          </div>
        </section>

        <section>
          <h2 className="text-2xl font-semibold mb-4">Source and releases</h2>
          <p className="text-sm text-muted-foreground">
            Source code and releases are at{" "}
            <a
              href="https://github.com/EricCogen/GauntletCI-Neovim"
              className="text-cyan-400 hover:underline"
              target="_blank"
              rel="noopener noreferrer"
            >
              EricCogen/GauntletCI-Neovim
            </a>.
          </p>
        </section>
      </div>
    </>
  );
}

import type { Metadata } from "next";
import { buildFaqSchema, softwareApplicationSchema } from "@/lib/schemas";

export const metadata: Metadata = {
  title: "MCP Server (AI Assistants) | GauntletCI Docs",
  description:
    "Connect GauntletCI to Claude, GitHub Copilot, Cursor, and other MCP-compatible AI assistants. Ask your AI to analyze commits and explain findings in context.",
  alternates: { canonical: "/docs/integrations/mcp" },
};

const jsonLd = {
  "@context": "https://schema.org",
  "@type": "TechArticle",
  headline: "GauntletCI MCP Server",
  description:
    "Connect GauntletCI to AI coding assistants via the Model Context Protocol.",
  url: "https://gauntletci.com/docs/integrations/mcp",
  publisher: { "@type": "Organization", name: "GauntletCI", url: "https://gauntletci.com" },
};

const faqSchema = buildFaqSchema([
  {
    q: "What is the GauntletCI MCP server?",
    a: "The MCP server exposes GauntletCI analysis as tools that any MCP-compatible AI assistant can call. When you ask your AI to check for risks in the current commit, it calls GauntletCI locally and returns the structured findings.",
  },
  {
    q: "Does the MCP server send my code anywhere?",
    a: "No. The MCP server calls the local GauntletCI CLI, which analyzes your diff entirely on your machine. Nothing is sent to any external service - not to the MCP server host, not to any GauntletCI cloud endpoint.",
  },
  {
    q: "Which AI assistants support MCP?",
    a: "Claude Desktop, GitHub Copilot (VS Code), Cursor, and any other assistant that implements the Model Context Protocol. The configuration format differs slightly per client - see the setup sections below.",
  },
  {
    q: "What tools does the GauntletCI MCP server expose?",
    a: "Three tools: analyze_commit returns findings as readable text, get_findings_json returns raw structured JSON for programmatic use, and get_sarif returns a SARIF 2.1.0 report compatible with GitHub Advanced Security and the VS Code SARIF viewer.",
  },
]);

const CLAUDE_CONFIG = `{
  "mcpServers": {
    "gauntletci": {
      "command": "node",
      "args": ["/path/to/gauntletci-mcp/dist/index.js"]
    }
  }
}`;

const COPILOT_CONFIG = `{
  "servers": {
    "gauntletci": {
      "type": "stdio",
      "command": "node",
      "args": ["\${workspaceFolder}/node_modules/.bin/gauntletci-mcp"]
    }
  }
}`;

const CURSOR_CONFIG = `{
  "mcpServers": {
    "gauntletci": {
      "command": "npx",
      "args": ["-y", "@ericcogen/gauntletci-mcp"]
    }
  }
}`;

export default function McpPage() {
  return (
    <>
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLd) }} />
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(softwareApplicationSchema) }} />
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(faqSchema) }} />
      <div className="space-y-10">
        <div>
          <p className="text-sm font-semibold text-cyan-400 uppercase tracking-widest mb-2">
            Extensions - MCP Server
          </p>
          <h1 className="text-4xl font-bold tracking-tight mb-4">MCP Server</h1>
          <p className="text-lg text-muted-foreground">
            The GauntletCI MCP server gives your AI coding assistant direct access to behavioral
            change risk analysis. Ask Claude, Copilot, or Cursor to check your current commit for
            risks - the assistant calls GauntletCI locally and explains the findings in context with
            your question.
          </p>
        </div>

        <section>
          <h2 className="text-2xl font-semibold mb-3">How it works</h2>
          <p className="text-muted-foreground mb-4 text-sm">
            The{" "}
            <a
              href="https://modelcontextprotocol.io"
              className="text-cyan-400 hover:underline"
              target="_blank"
              rel="noopener noreferrer"
            >
              Model Context Protocol
            </a>{" "}
            is an open standard that lets AI assistants call external tools. The GauntletCI MCP
            server is a local Node.js process that listens on stdin/stdout. When your assistant
            calls the <code className="bg-muted px-1 rounded text-xs">analyze_commit</code> tool,
            the server runs <code className="bg-muted px-1 rounded text-xs">gauntletci analyze</code>{" "}
            in the directory you specify and returns the findings as structured text.
          </p>

          {/* Architecture diagram mockup */}
          <div className="rounded-lg border border-border bg-card p-6 text-sm text-center">
            <div className="flex items-center justify-center gap-4 flex-wrap">
              <div className="rounded-lg border border-cyan-500/30 bg-cyan-500/5 px-4 py-3 text-center">
                <p className="font-semibold text-foreground text-xs">AI Assistant</p>
                <p className="text-muted-foreground text-xs mt-0.5">Claude / Copilot / Cursor</p>
              </div>
              <div className="text-muted-foreground text-lg">--</div>
              <div className="text-muted-foreground text-center">
                <p className="text-xs font-mono">MCP (stdio)</p>
              </div>
              <div className="text-muted-foreground text-lg">--</div>
              <div className="rounded-lg border border-border bg-muted/30 px-4 py-3 text-center">
                <p className="font-semibold text-foreground text-xs">gauntletci-mcp</p>
                <p className="text-muted-foreground text-xs mt-0.5">Node.js (local)</p>
              </div>
              <div className="text-muted-foreground text-lg">--</div>
              <div className="text-muted-foreground text-center">
                <p className="text-xs font-mono">CLI spawn</p>
              </div>
              <div className="text-muted-foreground text-lg">--</div>
              <div className="rounded-lg border border-border bg-muted/30 px-4 py-3 text-center">
                <p className="font-semibold text-foreground text-xs">GauntletCI CLI</p>
                <p className="text-muted-foreground text-xs mt-0.5">.NET tool (local)</p>
              </div>
            </div>
            <p className="text-xs text-muted-foreground mt-4">
              All processing is local. No code or diff content leaves your machine.
            </p>
          </div>
        </section>

        <section>
          <h2 className="text-2xl font-semibold mb-3">Prerequisites</h2>
          <ul className="space-y-2 text-sm text-muted-foreground list-none">
            {[
              "Node.js 20 or later",
              "GauntletCI CLI: dotnet tool install -g GauntletCI",
              "An MCP-compatible AI assistant (Claude Desktop, Copilot, or Cursor)",
            ].map((item) => (
              <li key={item} className="flex items-start gap-2">
                <span className="text-cyan-400 mt-0.5">+</span>
                <span>{item}</span>
              </li>
            ))}
          </ul>
        </section>

        <section>
          <h2 className="text-2xl font-semibold mb-3">Install the MCP server</h2>
          <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm">
            <p>
              <span className="text-cyan-400">$</span>{" "}
              <span className="text-foreground">npm install -g @ericcogen/gauntletci-mcp</span>
            </p>
          </div>
          <p className="text-sm text-muted-foreground mt-3">
            Or clone and build from source for the latest changes:
          </p>
          <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm mt-2 space-y-1">
            <p>
              <span className="text-cyan-400">$</span>{" "}
              <span className="text-foreground">git clone https://github.com/EricCogen/GauntletCI-MCP</span>
            </p>
            <p>
              <span className="text-cyan-400">$</span>{" "}
              <span className="text-foreground">cd GauntletCI-MCP && npm install && npm run build</span>
            </p>
          </div>
        </section>

        <section>
          <h2 className="text-2xl font-semibold mb-3">Setup: Claude Desktop</h2>
          <p className="text-muted-foreground mb-3 text-sm">
            Add the server to your Claude Desktop config file. On macOS the file is at{" "}
            <code className="bg-muted px-1 rounded text-xs">
              ~/Library/Application Support/Claude/claude_desktop_config.json
            </code>
            . On Windows it is at{" "}
            <code className="bg-muted px-1 rounded text-xs">
              %APPDATA%\Claude\claude_desktop_config.json
            </code>
            .
          </p>
          <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm overflow-x-auto">
            <pre className="text-foreground whitespace-pre">{CLAUDE_CONFIG}</pre>
          </div>
          <p className="text-sm text-muted-foreground mt-3">
            Replace <code className="bg-muted px-1 rounded text-xs">/path/to/gauntletci-mcp/dist/index.js</code>{" "}
            with the absolute path to the built server. Restart Claude Desktop after saving.
          </p>
        </section>

        <section>
          <h2 className="text-2xl font-semibold mb-3">Setup: GitHub Copilot (VS Code)</h2>
          <p className="text-muted-foreground mb-3 text-sm">
            Create or update{" "}
            <code className="bg-muted px-1 rounded text-xs">.vscode/mcp.json</code> in your
            workspace. This scopes the server to projects that use it.
          </p>
          <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm overflow-x-auto">
            <pre className="text-foreground whitespace-pre">{COPILOT_CONFIG}</pre>
          </div>
        </section>

        <section>
          <h2 className="text-2xl font-semibold mb-3">Setup: Cursor</h2>
          <p className="text-muted-foreground mb-3 text-sm">
            In Cursor, open{" "}
            <code className="bg-muted px-1 rounded text-xs">Settings &gt; MCP</code> and add the
            server configuration, or add it directly to{" "}
            <code className="bg-muted px-1 rounded text-xs">~/.cursor/mcp.json</code>:
          </p>
          <div className="rounded-lg border border-border bg-card p-4 font-mono text-sm overflow-x-auto">
            <pre className="text-foreground whitespace-pre">{CURSOR_CONFIG}</pre>
          </div>
        </section>

        <section>
          <h2 className="text-2xl font-semibold mb-3">Available tools</h2>
          <div className="rounded-lg border border-border overflow-hidden">
            <table className="w-full text-sm">
              <thead className="bg-muted/50">
                <tr>
                  <th className="text-left px-4 py-2 font-medium text-muted-foreground">Tool</th>
                  <th className="text-left px-4 py-2 font-medium text-muted-foreground">Description</th>
                  <th className="text-left px-4 py-2 font-medium text-muted-foreground">Parameters</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border">
                {[
                  [
                    "analyze_commit",
                    "Run GauntletCI on HEAD and return findings as readable structured text.",
                    "workingDirectory (required), sensitivity (optional)",
                  ],
                  [
                    "get_findings_json",
                    "Run GauntletCI and return the raw JSON result for programmatic processing.",
                    "workingDirectory (required), sensitivity (optional)",
                  ],
                  [
                    "get_sarif",
                    "Run GauntletCI and return a SARIF 2.1.0 report for ingestion into GHAS or the VS Code SARIF viewer.",
                    "workingDirectory (required)",
                  ],
                ].map(([tool, desc, params]) => (
                  <tr key={tool}>
                    <td className="px-4 py-2 font-mono text-xs text-cyan-400">{tool}</td>
                    <td className="px-4 py-2 text-muted-foreground text-sm">{desc}</td>
                    <td className="px-4 py-2 text-muted-foreground text-xs">{params}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <p className="text-sm text-muted-foreground mt-3">
            The <code className="bg-muted px-1 rounded text-xs">sensitivity</code> parameter accepts{" "}
            <code className="bg-muted px-1 rounded text-xs">strict</code>,{" "}
            <code className="bg-muted px-1 rounded text-xs">balanced</code> (default), or{" "}
            <code className="bg-muted px-1 rounded text-xs">permissive</code>.
          </p>
        </section>

        <section>
          <h2 className="text-2xl font-semibold mb-3">Example prompts</h2>
          <p className="text-muted-foreground mb-4 text-sm">
            Once the MCP server is connected, you can ask your AI assistant directly about your
            commit. The assistant calls GauntletCI, parses the results, and responds in natural
            language.
          </p>

          {/* Chat mockup */}
          <div className="rounded-lg border border-border overflow-hidden space-y-0">
            {[
              {
                role: "user",
                text: "Run GauntletCI on the repo at /Users/eric/projects/myapp and tell me what risks it found.",
              },
              {
                role: "assistant",
                text: "I ran GauntletCI on the current HEAD commit. It found 3 issues:\n\n[Block] GCI0001 in OrderService.cs:44 - Behavior change without test coverage. SaveAsync was modified but no test file covering OrderService was updated. Add a test covering the new behavior before merging.\n\n[Block] GCI0003 in PaymentService.cs:112 - A new exception path was introduced but no call sites were updated to handle it. This may cause unhandled exceptions in production.\n\n[Warn] GCI0004 in Models/Order.cs:23 - Return type semantics changed from non-nullable to nullable. Callers may not be checking for null.",
              },
              {
                role: "user",
                text: "Can you explain why the GCI0003 finding is a problem?",
              },
              {
                role: "assistant",
                text: "GCI0003 fires when a method now throws a new exception type that no existing call sites are prepared to catch. In this case, PaymentService.ProcessPayment now throws InvalidOperationException under a condition that didn't exist before. If callers swallow all exceptions or only catch specific types, this new exception will propagate up as an unhandled crash at runtime - even if all existing tests pass, because the tests exercise the happy path that doesn't trigger the new branch.",
              },
            ].map(({ role, text }, i) => (
              <div
                key={i}
                className={`p-4 ${role === "user" ? "bg-muted/20 border-b border-border" : "border-b border-border"}`}
              >
                <p className={`text-xs font-semibold mb-1.5 ${role === "user" ? "text-cyan-400" : "text-foreground"}`}>
                  {role === "user" ? "You" : "AI Assistant"}
                </p>
                <p className="text-sm text-muted-foreground whitespace-pre-line">{text}</p>
              </div>
            ))}
          </div>
        </section>

        <section>
          <h2 className="text-2xl font-semibold mb-3">Suggested prompts</h2>
          <div className="space-y-2">
            {[
              "Are there any block-level risks in my current commit?",
              "Get the GauntletCI SARIF report for this repo and summarize the top issues.",
              "Run GauntletCI in strict mode and explain any findings related to async code.",
              "What does finding GCI0003 mean and how do I fix it?",
            ].map((prompt) => (
              <div
                key={prompt}
                className="rounded-lg border border-border bg-card px-4 py-3 font-mono text-sm text-muted-foreground"
              >
                {`"`}{prompt}{`"`}
              </div>
            ))}
          </div>
        </section>
      </div>
    </>
  );
}

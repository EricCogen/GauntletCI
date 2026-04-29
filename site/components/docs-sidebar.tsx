"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";

const coreItems = [
  { href: "/docs", label: "Getting Started" },
  { href: "/docs/cli-reference", label: "CLI Reference" },
  { href: "/docs/rules", label: "Rule Library" },
  { href: "/docs/configuration", label: "Configuration" },
  { href: "/docs/local-llm", label: "Local LLM Setup" },
  { href: "/docs/custom-rules", label: "Custom Rules" },
];

const integrationItems = [
  { href: "/docs/integrations", label: "CI/CD Overview" },
  { href: "/docs/integrations/github-action", label: "GitHub Action" },
  { href: "/docs/integrations/azure-devops", label: "Azure DevOps" },
  { href: "/docs/integrations/gitlab", label: "GitLab CI" },
  { href: "/docs/integrations/bitbucket", label: "Bitbucket Pipelines" },
  { href: "/docs/integrations/vscode", label: "VS Code" },
  { href: "/docs/integrations/visual-studio", label: "Visual Studio 2022" },
  { href: "/docs/integrations/rider", label: "JetBrains Rider" },
  { href: "/docs/integrations/neovim", label: "Neovim" },
  { href: "/docs/integrations/mcp", label: "MCP Server (AI)" },
  { href: "/docs/integrations/pre-commit", label: "Pre-commit Hooks" },
];

export function DocsSidebar() {
  const pathname = usePathname();
  const activeClass = "bg-cyan-500/10 text-cyan-400 font-medium";
  const inactiveClass = "text-muted-foreground hover:text-foreground hover:bg-muted";

  return (
    <aside data-pagefind-ignore="" className="hidden md:block w-56 shrink-0">
      <nav className="sticky top-24 space-y-1">
        <p className="text-xs font-semibold text-muted-foreground uppercase tracking-widest mb-3">Documentation</p>
        {coreItems.map((item) => (
          <Link
            key={item.href}
            href={item.href}
            className={`block rounded-md px-3 py-2 text-sm transition-colors ${
              pathname === item.href ? activeClass : inactiveClass
            }`}
          >
            {item.label}
          </Link>
        ))}

        <p className="text-xs font-semibold text-muted-foreground uppercase tracking-widest pt-5 pb-2">
          Extensions
        </p>
        {integrationItems.map((item) => (
          <Link
            key={item.href}
            href={item.href}
            className={`block rounded-md px-3 py-2 text-sm transition-colors ${
              pathname === item.href ? activeClass : inactiveClass
            }`}
          >
            {item.label}
          </Link>
        ))}
      </nav>
    </aside>
  );
}

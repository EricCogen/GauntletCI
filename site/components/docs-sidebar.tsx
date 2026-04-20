"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";

const navItems = [
  { href: "/docs", label: "Getting Started" },
  { href: "/docs/cli-reference", label: "CLI Reference" },
  { href: "/docs/rules", label: "Rule Library" },
  { href: "/docs/configuration", label: "Configuration" },
  { href: "/docs/integrations", label: "CI/CD Integrations" },
  { href: "/docs/local-llm", label: "Local LLM Setup" },
];

export function DocsSidebar() {
  const pathname = usePathname();
  return (
    <aside className="hidden md:block w-56 shrink-0">
      <nav className="sticky top-24 space-y-1">
        <p className="text-xs font-semibold text-muted-foreground uppercase tracking-widest mb-4">Documentation</p>
        {navItems.map((item) => (
          <Link
            key={item.href}
            href={item.href}
            className={`block rounded-md px-3 py-2 text-sm transition-colors ${
              pathname === item.href
                ? "bg-cyan-500/10 text-cyan-400 font-medium"
                : "text-muted-foreground hover:text-foreground hover:bg-muted"
            }`}
          >
            {item.label}
          </Link>
        ))}
      </nav>
    </aside>
  );
}

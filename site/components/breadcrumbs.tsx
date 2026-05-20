"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useMemo } from "react";

interface BreadcrumbItem {
  label: string;
  href: string;
}

const ROUTE_LABELS: Record<string, string> = {
  "": "Home",
  "about": "About",
  "docs": "Documentation",
  "demo": "Live Demo",
  "github-checks-sarif": "GitHub Checks & SARIF",
  "pricing": "Pricing",
  "detections": "Detection Rules",
  "case-studies": "Case Studies",
  "releases": "Releases",
  "articles": "Articles",
  "benchmark": "Benchmark",
  "why-tests-miss-bugs": "Why Tests Miss Bugs",
  "why-code-review-misses-bugs": "Why Code Review Misses Bugs",
  "what-is-diff-based-analysis": "What is Diff-Based Analysis",
  "detect-breaking-changes-before-merge": "Detect Breaking Changes",
  "can-ai-code-review-be-deterministic": "Can AI Code Review Be Deterministic",
  "behavioral-change-risk-formal-framework": "Formal Framework for Risk",
};

export function Breadcrumbs() {
  const pathname = usePathname();

  const breadcrumbs = useMemo(() => {
    const segments = pathname
      .split("/")
      .filter((seg) => seg.length > 0)
      .slice(0, 3); // Limit to 3 levels deep

    const items: BreadcrumbItem[] = [
      { label: "Home", href: "/" },
    ];

    let href = "";
    for (let i = 0; i < segments.length; i++) {
      const segment = segments[i];
      
      // Skip 'p' route parameter (pagination segment) and numeric page numbers
      if (segment === "p" || /^\d+$/.test(segment)) {
        continue;
      }
      
      href += `/${segment}`;
      const label = ROUTE_LABELS[segment] || formatLabel(segment);
      items.push({ label, href });
    }

    return items;
  }, [pathname]);

  // Only render breadcrumbs for nested pages
  if (breadcrumbs.length <= 1) return null;

  // Mobile: show only Home > Current
  const mobileItems = [breadcrumbs[0], breadcrumbs[breadcrumbs.length - 1]];

  return (
    <>
      {/* Desktop breadcrumbs */}
      <nav
        className="hidden sm:block mb-6 text-sm text-muted-foreground"
        aria-label="Breadcrumb"
      >
        <ol className="flex items-center gap-2 flex-wrap">
          {breadcrumbs.map((item, index) => (
            <li key={item.href} className="flex items-center gap-2">
              {index > 0 && <span className="text-muted-foreground mx-1">›</span>}
              {index === breadcrumbs.length - 1 ? (
                <span className="text-foreground font-medium" aria-current="page">
                  {item.label}
                </span>
              ) : (
                <Link
                  href={item.href}
                  className="hover:text-foreground transition-colors"
                >
                  {item.label}
                </Link>
              )}
            </li>
          ))}
        </ol>
      </nav>

      {/* Mobile breadcrumbs (abbreviated) */}
      <nav
        className="sm:hidden mb-4 text-xs text-muted-foreground"
        aria-label="Breadcrumb"
      >
        <ol className="flex items-center gap-1">
          {mobileItems.map((item, index) => (
            <li key={item.href} className="flex items-center gap-1">
              {index > 0 && <span className="text-muted-foreground">›</span>}
              {index === mobileItems.length - 1 ? (
                <span className="text-foreground font-medium" aria-current="page">
                  {item.label}
                </span>
              ) : (
                <Link
                  href={item.href}
                  className="hover:text-foreground transition-colors"
                >
                  {item.label}
                </Link>
              )}
            </li>
          ))}
        </ol>
      </nav>

      {/* JSON-LD schema for SEO */}
      <script
        type="application/ld+json"
        suppressHydrationWarning
        dangerouslySetInnerHTML={{
          __html: JSON.stringify({
            "@context": "https://schema.org",
            "@type": "BreadcrumbList",
            itemListElement: breadcrumbs.map((item, index) => ({
              "@type": "ListItem",
              position: index + 1,
              name: item.label,
              item: `https://gauntletci.com${item.href}`,
            })),
          }),
        }}
      />
    </>
  );
}

function formatLabel(segment: string): string {
  return segment
    .split("-")
    .map((word) => word.charAt(0).toUpperCase() + word.slice(1))
    .join(" ");
}

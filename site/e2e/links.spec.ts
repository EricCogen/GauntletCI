import { test, expect } from "@playwright/test";

/**
 * Normalize a raw href value into an absolute site path (e.g. "/docs/rules/GCI0001").
 * Returns null for external links, anchors, mailto:, tel:, and non-page resources.
 */
function toPath(href: string): string | null {
  if (!href) return null;
  if (href.startsWith("#") || href.startsWith("mailto:") || href.startsWith("tel:")) return null;
  try {
    if (href.startsWith("http://") || href.startsWith("https://")) {
      const url = new URL(href);
      if (url.hostname !== "gauntletci.com") return null;
      return url.pathname.replace(/\/$/, "") || "/";
    }
    if (href.startsWith("/")) {
      return href.split("?")[0].split("#")[0].replace(/\/$/, "") || "/";
    }
  } catch {
    return null;
  }
  return null;
}

test.describe("link graph", () => {
  let sitePages: string[] = [];
  const linkGraph = new Map<string, Set<string>>();

  test.beforeAll(async ({ browser }) => {
    const ctx = await browser.newContext();
    const page = await ctx.newPage();

    // Parse the generated sitemap.xml for the authoritative page list.
    const resp = await page.goto("/sitemap.xml");
    const xml = await resp!.text();
    const locs = [...xml.matchAll(/<loc>(.*?)<\/loc>/g)].map((m) => m[1]);
    sitePages = locs
      .map((loc) => {
        try {
          return new URL(loc).pathname.replace(/\/$/, "") || "/";
        } catch {
          return null;
        }
      })
      .filter(Boolean) as string[];

    // Crawl every page and record which internal pages it links to.
    // Only links in body content count - header, footer, and nav links are
    // boilerplate present on every page and carry no contextual signal.
    for (const path of sitePages) {
      await page.goto(path);
      const hrefs: string[] = await page.$$eval("a[href]", (as) =>
        as
          .filter((a) => !a.closest("header, footer, nav"))
          .map((a) => a.getAttribute("href") ?? "")
      );
      const outbound = new Set<string>();
      for (const href of hrefs) {
        const normalized = toPath(href);
        if (normalized !== null && normalized !== path) {
          outbound.add(normalized);
        }
      }
      linkGraph.set(path, outbound);
    }

    await ctx.close();
  });

  test("no sitemap page is an orphan (at least one other page links to it)", () => {
    const orphans: string[] = [];
    for (const sitePage of sitePages) {
      // The home page is the canonical entry point - exempt from the inbound check.
      if (sitePage === "/") continue;
      const hasInbound = [...linkGraph.entries()].some(
        ([from, outbound]) => from !== sitePage && outbound.has(sitePage)
      );
      if (!hasInbound) orphans.push(sitePage);
    }
    expect(
      orphans,
      `Pages in sitemap with no inbound links from any other page:\n  ${orphans.join("\n  ")}`
    ).toHaveLength(0);
  });

  test("no sitemap page has zero outbound links to other sitemap pages", () => {
    const isolated: string[] = [];
    for (const sitePage of sitePages) {
      const outbound = linkGraph.get(sitePage) ?? new Set<string>();
      const internalOutbound = [...outbound].filter((p) => sitePages.includes(p));
      if (internalOutbound.length === 0) isolated.push(sitePage);
    }
    expect(
      isolated,
      `Pages in sitemap with no outbound links to other sitemap pages:\n  ${isolated.join("\n  ")}`
    ).toHaveLength(0);
  });
});

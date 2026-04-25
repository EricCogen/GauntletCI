"use client";

import { useState, useEffect } from "react";
import { Search, X } from "lucide-react";
import Link from "next/link";

type PagefindResult = {
  data: () => Promise<{ url: string; meta: { title: string }; excerpt: string }>;
};

type PagefindApi = {
  search: (query: string) => Promise<{ results: PagefindResult[] }>;
};

let cachedPagefind: PagefindApi | null = null;

async function loadPagefind(): Promise<PagefindApi | null> {
  if (cachedPagefind) return cachedPagefind;
  try {
    // @ts-expect-error pagefind is a build artifact, not in the module graph
    const pf = await import(/* webpackIgnore: true */ "/pagefind/pagefind.js");
    cachedPagefind = pf as PagefindApi;
    return cachedPagefind;
  } catch {
    return null;
  }
}

type ResultItem = { url: string; title: string; excerpt: string };

export function SearchDialog() {
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState("");
  const [results, setResults] = useState<ResultItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [unavailable, setUnavailable] = useState(false);

  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      if ((e.metaKey || e.ctrlKey) && e.key === "k") {
        e.preventDefault();
        setOpen((prev) => !prev);
      }
      if (e.key === "Escape") setOpen(false);
    };
    window.addEventListener("keydown", handler);
    return () => window.removeEventListener("keydown", handler);
  }, []);

  useEffect(() => {
    if (!open || !query.trim()) {
      setResults([]);
      return;
    }
    const timer = setTimeout(async () => {
      setLoading(true);
      const pf = await loadPagefind();
      if (!pf) {
        setUnavailable(true);
        setLoading(false);
        return;
      }
      try {
        const search = await pf.search(query);
        const resolved = await Promise.all(
          search.results.slice(0, 8).map((r) => r.data())
        );
        setResults(
          resolved.map((r) => ({
            url: r.url,
            title: r.meta.title,
            excerpt: r.excerpt,
          }))
        );
      } catch {
        setResults([]);
      }
      setLoading(false);
    }, 150);
    return () => clearTimeout(timer);
  }, [query, open]);

  useEffect(() => {
    if (!open) {
      setQuery("");
      setResults([]);
      setUnavailable(false);
    }
  }, [open]);

  return (
    <>
      <button
        onClick={() => setOpen(true)}
        className="inline-flex items-center gap-2 rounded-md border border-border bg-muted/50 px-3 py-1.5 text-sm text-muted-foreground hover:text-foreground hover:border-foreground/40 transition-colors"
        aria-label="Search documentation"
      >
        <Search className="h-3.5 w-3.5" />
        <span className="hidden lg:inline">Search docs</span>
        <kbd className="hidden lg:inline ml-1 rounded border border-border bg-background px-1.5 py-0.5 text-xs font-mono">
          &#8984;K
        </kbd>
      </button>

      {open && (
        <div
          className="fixed inset-0 z-[100] flex items-start justify-center pt-[10vh] px-4"
          onClick={() => setOpen(false)}
        >
          <div className="absolute inset-0 bg-background/80 backdrop-blur-sm" />
          <div
            className="relative w-full max-w-xl rounded-xl border border-border bg-card shadow-2xl"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="flex items-center gap-3 px-4 py-3 border-b border-border">
              <Search className="h-4 w-4 text-muted-foreground shrink-0" />
              <input
                autoFocus
                value={query}
                onChange={(e) => setQuery(e.target.value)}
                placeholder="Search docs and rules..."
                className="flex-1 bg-transparent text-sm text-foreground placeholder:text-muted-foreground outline-none"
              />
              {query && (
                <button
                  onClick={() => setQuery("")}
                  className="text-muted-foreground hover:text-foreground"
                  aria-label="Clear search"
                >
                  <X className="h-4 w-4" />
                </button>
              )}
              <button
                onClick={() => setOpen(false)}
                className="ml-1 rounded border border-border px-2 py-0.5 text-xs text-muted-foreground hover:text-foreground font-mono"
              >
                ESC
              </button>
            </div>

            <div className="max-h-[60vh] overflow-y-auto">
              {unavailable && (
                <p className="px-4 py-8 text-sm text-center text-muted-foreground">
                  Search is only available on the built site.
                </p>
              )}
              {!unavailable && loading && (
                <p className="px-4 py-8 text-sm text-center text-muted-foreground">
                  Searching...
                </p>
              )}
              {!unavailable && !loading && query && results.length === 0 && (
                <p className="px-4 py-8 text-sm text-center text-muted-foreground">
                  No results for &ldquo;{query}&rdquo;
                </p>
              )}
              {results.map((r, i) => (
                <Link
                  key={i}
                  href={r.url}
                  onClick={() => setOpen(false)}
                  className="block px-4 py-3 hover:bg-muted/50 border-b border-border last:border-0 transition-colors"
                >
                  <p className="text-sm font-medium text-foreground mb-1">
                    {r.title}
                  </p>
                  <p
                    className="text-xs text-muted-foreground leading-relaxed line-clamp-2"
                    dangerouslySetInnerHTML={{ __html: r.excerpt }}
                  />
                </Link>
              ))}
              {!unavailable && !query && (
                <p className="px-4 py-8 text-sm text-center text-muted-foreground">
                  Type to search across all docs and rules...
                </p>
              )}
            </div>
          </div>
        </div>
      )}
    </>
  );
}

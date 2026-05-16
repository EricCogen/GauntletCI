"use client";

import React from "react";
import Link from "next/link";
import { Pin } from "lucide-react";

const PAGE_SIZE = 10;

type Article = {
  href: string;
  title: string;
  description: string;
  tags: string[];
  readTime: string;
  pinned?: boolean;
};

export function ArticleList({ articles }: { articles: Article[] }) {
  // Sort: pinned first, then by order
  const sortedArticles = React.useMemo(() => {
    return [...articles].sort((a, b) => {
      if (a.pinned && !b.pinned) return -1;
      if (!a.pinned && b.pinned) return 1;
      return articles.indexOf(a) - articles.indexOf(b);
    });
  }, [articles]);

  const [page, setPage] = React.useState(1);
  const totalPages = Math.ceil(sortedArticles.length / PAGE_SIZE);
  const start = (page - 1) * PAGE_SIZE;
  const visible = sortedArticles.slice(start, start + PAGE_SIZE);

  return (
    <>
      <div className="space-y-6">
        {visible.map((article) => (
          <Link
            key={article.href}
            href={article.href}
            className={`group block rounded-xl border bg-card/30 hover:bg-card/60 transition-all p-6 relative ${
              article.pinned
                ? "border-cyan-400/60 hover:border-cyan-400/80 bg-cyan-400/5 hover:bg-cyan-400/10"
                : "border-border hover:border-cyan-500/30"
            }`}
          >
            {/* Pinned indicator */}
            {article.pinned && (
              <div className="absolute bottom-4 right-4 text-cyan-400">
                <Pin size={18} fill="currentColor" />
              </div>
            )}

            <div className="flex flex-wrap items-center gap-2 mb-3">
              {article.tags.map((tag) => (
                <span
                  key={tag}
                  className="text-xs font-medium px-2 py-0.5 rounded-full bg-secondary text-muted-foreground"
                >
                  {tag}
                </span>
              ))}
              <span className="text-xs text-muted-foreground/50 ml-auto">
                {article.readTime}
              </span>
            </div>
            <h2 className="text-xl font-semibold text-foreground group-hover:text-cyan-400 transition-colors mb-2 pr-8">
              {article.title}
            </h2>
            <p className="text-sm text-muted-foreground leading-relaxed">
              {article.description}
            </p>
            <p className="mt-4 text-xs text-cyan-400/70 group-hover:text-cyan-400 transition-colors">
              Read article →
            </p>
          </Link>
        ))}
      </div>

      {totalPages > 1 && (
        <div className="flex items-center justify-between mt-10 pt-6 border-t border-border">
          <button
            onClick={() => { setPage((p) => p - 1); window.scrollTo({ top: 0, behavior: "smooth" }); }}
            disabled={page === 1}
            className="px-4 py-2 text-sm font-medium rounded-lg border border-border bg-card/30 hover:bg-card/60 hover:border-cyan-500/30 transition-all disabled:opacity-30 disabled:cursor-not-allowed"
          >
            ← Previous
          </button>
          <span className="text-sm text-muted-foreground">
            Page {page} of {totalPages}
          </span>
          <button
            onClick={() => { setPage((p) => p + 1); window.scrollTo({ top: 0, behavior: "smooth" }); }}
            disabled={page === totalPages}
            className="px-4 py-2 text-sm font-medium rounded-lg border border-border bg-card/30 hover:bg-card/60 hover:border-cyan-500/30 transition-all disabled:opacity-30 disabled:cursor-not-allowed"
          >
            Next →
          </button>
        </div>
      )}
    </>
  );
}

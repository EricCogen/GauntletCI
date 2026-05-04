"use client";

import React from "react";
import Link from "next/link";

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
            className="group block rounded-xl border border-border bg-card/30 hover:bg-card/60 hover:border-cyan-500/30 transition-all p-6 relative"
          >
            {/* Pin icon in top-right */}
            {article.pinned && (
              <div className="absolute top-4 right-4 text-cyan-400">
                <svg
                  className="w-5 h-5"
                  fill="currentColor"
                  viewBox="0 0 20 20"
                  xmlns="http://www.w3.org/2000/svg"
                >
                  <path d="M10.894 2.553a1 1 0 00-1.788 0l-7 14a1 1 0 001.169 1.409l5.951-1.429 5.951 1.429a1 1 0 001.169-1.409l-7-14z" />
                </svg>
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

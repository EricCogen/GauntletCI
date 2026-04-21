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
};

export function ArticleList({ articles }: { articles: Article[] }) {
  const [page, setPage] = React.useState(1);
  const totalPages = Math.ceil(articles.length / PAGE_SIZE);
  const start = (page - 1) * PAGE_SIZE;
  const visible = articles.slice(start, start + PAGE_SIZE);

  return (
    <>
      <div className="space-y-6">
        {visible.map((article) => (
          <Link
            key={article.href}
            href={article.href}
            className="group block rounded-xl border border-border bg-card/30 hover:bg-card/60 hover:border-cyan-500/30 transition-all p-6"
          >
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
            <h2 className="text-xl font-semibold text-foreground group-hover:text-cyan-400 transition-colors mb-2">
              {article.title}
            </h2>
            <p className="text-sm text-muted-foreground leading-relaxed">
              {article.description}
            </p>
            <p className="mt-4 text-xs text-cyan-400/70 group-hover:text-cyan-400 transition-colors">
              Read article &rarr;
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
            &larr; Previous
          </button>
          <span className="text-sm text-muted-foreground">
            Page {page} of {totalPages}
          </span>
          <button
            onClick={() => { setPage((p) => p + 1); window.scrollTo({ top: 0, behavior: "smooth" }); }}
            disabled={page === totalPages}
            className="px-4 py-2 text-sm font-medium rounded-lg border border-border bg-card/30 hover:bg-card/60 hover:border-cyan-500/30 transition-all disabled:opacity-30 disabled:cursor-not-allowed"
          >
            Next &rarr;
          </button>
        </div>
      )}
    </>
  );
}

import type { Metadata } from "next";
import JsonLd from "@/components/json-ld";
import { Header } from "@/components/header";
import { Footer } from "@/components/footer";
import { Breadcrumbs } from "@/components/breadcrumbs";
import Link from "next/link";
import { Pin } from "lucide-react";
import { articles } from "@/lib/articles";

const PAGE_SIZE = 10;

export async function generateStaticParams() {
  const sortedArticles = [...articles].reverse();
  const totalArticles = sortedArticles.length;
  const totalPages = Math.ceil(totalArticles / PAGE_SIZE);
  
  return Array.from({ length: Math.max(totalPages - 1, 0) }, (_, i) => ({
    page: String(i + 2),
  }));
}

export async function generateMetadata({ params }: { params: Promise<{ page: string }> }): Promise<Metadata> {
  const { page } = await params;
  const pageNum = parseInt(page, 10) || 1;
  const canonical = pageNum === 1 ? "/articles" : `/articles/p/${pageNum}`;
  
  return {
    title: `Articles${pageNum > 1 ? ` - Page ${pageNum}` : ""} | GauntletCI -- .NET Change Risk and Code Review`,
    description:
      "Technical articles on behavioral regressions in .NET, why code review and tests miss certain bugs, and how diff-based analysis catches what other tools skip.",
    alternates: { canonical },
  };
}

const jsonLd = {
  "@context": "https://schema.org",
  "@type": "CollectionPage",
  name: "GauntletCI Articles",
  description: "Technical articles on .NET change risk, code review blind spots, and diff-based static analysis.",
  url: "https://gauntletci.com/articles",
};

export default async function ArticlesPageRoute({ params }: { params: Promise<{ page: string }> }) {
  const { page } = await params;
  const pageParam = parseInt(page, 10);
  const currentPage = Math.max(1, pageParam || 1);

  // Sort: pinned first, then by order
  const sortedArticles = [...articles].reverse();
  const pinnedArticles = sortedArticles.filter((a) => a.pinned);
  const regularArticles = sortedArticles.filter((a) => !a.pinned);
  const allArticles = [...pinnedArticles, ...regularArticles];

  const totalPages = Math.ceil(allArticles.length / PAGE_SIZE);
  const validPage = Math.min(currentPage, totalPages || 1);
  const start = (validPage - 1) * PAGE_SIZE;
  const visible = allArticles.slice(start, start + PAGE_SIZE);

  return (
    <>
      <JsonLd data={jsonLd} />
      <div className="min-h-screen">
        <Header />

        <div className="mx-auto max-w-4xl px-4 sm:px-6 lg:px-8 pt-28 pb-20">
          <Breadcrumbs />
          <div className="mb-12">
            <h1 className="text-4xl sm:text-5xl font-bold tracking-tight text-balance">
              Articles
            </h1>
            <p className="mt-4 text-lg text-muted-foreground max-w-2xl text-pretty">
              Technical writing on behavioral regressions, code review blind spots,
              and why certain bugs only show up in production.
            </p>
          </div>

          {/* Articles Grid */}
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

          {/* SEO-Friendly Pagination with Static Links */}
          {totalPages > 1 && (
            <div className="flex flex-col items-center justify-center mt-12 pt-8 border-t border-border">
              <div className="flex items-center gap-2 flex-wrap justify-center mb-6">
                {/* Previous link */}
                {validPage > 1 ? (
                  <Link
                    href={validPage === 2 ? "/articles" : `/articles/p/${validPage - 1}`}
                    className="px-4 py-2 text-sm font-medium rounded-lg border border-border bg-card/30 hover:bg-card/60 hover:border-cyan-500/30 transition-all"
                  >
                    ← Previous
                  </Link>
                ) : (
                  <span className="px-4 py-2 text-sm font-medium rounded-lg border border-border bg-card/30 opacity-30 cursor-not-allowed">
                    ← Previous
                  </span>
                )}

                {/* Page numbers */}
                <div className="flex gap-1">
                  {Array.from({ length: totalPages }, (_, i) => i + 1).map((pageNum) => (
                    <Link
                      key={pageNum}
                      href={pageNum === 1 ? "/articles" : `/articles/p/${pageNum}`}
                      className={`px-3 py-2 text-sm font-medium rounded-lg transition-all ${
                        pageNum === validPage
                          ? "bg-cyan-500/20 border border-cyan-500/50 text-cyan-400 font-semibold"
                          : "border border-border bg-card/30 hover:bg-card/60 hover:border-cyan-500/30"
                      }`}
                    >
                      {pageNum}
                    </Link>
                  ))}
                </div>

                {/* Next link */}
                {validPage < totalPages ? (
                  <Link
                    href={`/articles/p/${validPage + 1}`}
                    className="px-4 py-2 text-sm font-medium rounded-lg border border-border bg-card/30 hover:bg-card/60 hover:border-cyan-500/30 transition-all"
                  >
                    Next →
                  </Link>
                ) : (
                  <span className="px-4 py-2 text-sm font-medium rounded-lg border border-border bg-card/30 opacity-30 cursor-not-allowed">
                    Next →
                  </span>
                )}
              </div>

              <span className="text-sm text-muted-foreground">
                Page {validPage} of {totalPages}
              </span>
            </div>
          )}
        </div>

        <Footer />
      </div>
    </>
  );
}

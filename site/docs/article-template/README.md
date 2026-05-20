# GauntletCI Article Template

This document describes the standard structure and components required for all new articles on gauntletci.com.

## File Structure

New articles should be created in:
```
app/articles/{article-slug}/page.tsx
```

## Required Sections

### 1. Imports

```typescript
import type { Metadata } from "next";
import Link from "next/link";
import { Header } from "@/components/header";
import { Footer } from "@/components/footer";
import { Breadcrumbs } from "@/components/breadcrumbs";
import { RulesApplied } from "@/components/rules-applied";
import { AuthorBio } from "@/components/author-bio";
import JsonLd from "@/components/json-ld";
```

### 2. Metadata Export

Provide comprehensive SEO metadata:

```typescript
export const metadata: Metadata = {
  title: "Article Title | GauntletCI",
  description: "Brief description of the article for search results",
  alternates: { canonical: "/articles/article-slug" },
  keywords: ["keyword1", "keyword2", "keyword3"],
  authors: [{ name: "Eric Cogen", url: "https://github.com/EricCogen" }],
  creator: "Eric Cogen",
  publisher: "GauntletCI",
  openGraph: {
    title: "Article Title",
    description: "Brief description for social sharing",
    url: "https://gauntletci.com/articles/article-slug",
    type: "article",
    images: [
      {
        url: "/og/article-slug.png",
        width: 1200,
        height: 630,
        alt: "Article Title",
      },
    ],
  },
  twitter: {
    card: "summary_large_image",
    title: "Article Title",
    description: "Brief description for Twitter",
    images: ["/og/article-slug.png"],
  },
};
```

### 3. JSON-LD Schema

```typescript
const jsonLd = {
  "@context": "https://schema.org",
  "@type": "Article",
  headline: "Full Article Title",
  description: "Full description of the article",
  image: "/og/article-slug.png",
  datePublished: "2026-05-19T00:00:00Z",
  author: {
    "@type": "Person",
    name: "Eric Cogen",
    url: "https://github.com/EricCogen",
  },
  publisher: {
    "@type": "Organization",
    name: "GauntletCI",
    url: "https://gauntletci.com",
    logo: {
      "@type": "ImageObject",
      url: "https://gauntletci.com/icon.svg",
    },
  },
  mainEntityOfPage: {
    "@type": "WebPage",
    "@id": "https://gauntletci.com/articles/article-slug",
  },
  keywords: ["keyword1", "keyword2", "keyword3"],
};
```

### 4. Reading Time

Calculate reading time based on content length (estimate 200 words per minute):

```typescript
const readingTime = "4 min read"; // or "3 min read", "8 min read", etc.
```

### 5. Page Component Structure

```typescript
export default function ArticleSlugPage() {
  return (
    <>
      {/* JSON-LD Schema */}
      <script type="application/ld+json" dangerouslySetInnerHTML={{ __html: JSON.stringify(jsonLd) }} />
      
      {/* Header */}
      <Header />
      
      {/* Main Content */}
      <main className="min-h-screen bg-background pt-24">
        <div className="mx-auto max-w-4xl px-4 sm:px-6 lg:px-8 py-16 sm:py-20 space-y-16">

          {/* Breadcrumbs */}
          <Breadcrumbs />

          {/* Hero Section */}
          <div className="space-y-5 border-b border-border pb-12">
            {/* Category Tag and Back Link */}
            <div className="flex items-center justify-between">
              <p className="text-sm font-semibold uppercase tracking-widest text-cyan-400">
                Category (e.g., "Case Study", "Engineering Philosophy")
              </p>
              <Link href="/articles" className="text-sm text-muted-foreground hover:text-cyan-400 transition-colors">
                ← All articles
              </Link>
            </div>
            
            {/* Title */}
            <h1 className="text-4xl sm:text-5xl font-bold tracking-tight text-balance">
              Article Title
            </h1>
            
            {/* Intro Paragraph */}
            <p className="text-xl text-muted-foreground leading-relaxed text-pretty">
              Compelling introduction paragraph that hooks the reader and explains the topic.
            </p>
            
            {/* Author Byline */}
            <div className="flex items-center gap-2 pt-1">
              <span className="text-sm font-medium text-foreground">Eric Cogen</span>
              <span className="text-muted-foreground/40 text-sm">·</span>
              <span className="text-sm text-muted-foreground">Founder, GauntletCI</span>
              <span className="text-muted-foreground/40 text-sm">·</span>
              <time className="text-sm text-muted-foreground" dateTime="2026-05-19">May 19, 2026</time>
              <span className="text-muted-foreground/40 text-sm">·</span>
              <span className="text-sm text-muted-foreground">{readingTime}</span>
            </div>
          </div>

          {/* Article Content */}
          <article className="space-y-16">
            <section className="space-y-4">
              <h2 className="text-3xl font-bold">Section Title</h2>
              <p className="text-lg text-muted-foreground leading-relaxed">
                Content paragraph
              </p>
              {/* Additional content sections */}
            </section>
          </article>

          {/* Rules Applied Component */}
          <RulesApplied ids={["GCI0016", "GCI0012", "GCI0044"]} />

          {/* Author Bio Section */}
          <div className="border-t border-border pt-12">
            <AuthorBio variant="long" />
          </div>

        </div>
      </main>

      {/* Footer */}
      <Footer />
    </>
  );
}
```

## Component Details

### Breadcrumbs
```typescript
<Breadcrumbs />
```
- Renders automatically based on current page location
- No parameters needed

### RulesApplied
```typescript
<RulesApplied ids={["GCI0004", "GCI0003", "GCI0006"]} />
```
- Pass array of rule IDs discussed in the article
- Pass empty array `ids={[]}` if no specific rules (component will render nothing)
- Shows rule cards with links to detailed documentation

### AuthorBio
```typescript
<AuthorBio variant="long" />
```
- Always use `variant="long"` for articles
- Displays author information and social links at end of article

## Style Classes

### Typography
- **Headings**: `text-3xl font-bold` for h2, `text-2xl font-bold` for h3
- **Body text**: `text-lg text-muted-foreground leading-relaxed` for paragraphs
- **Emphasis**: Use `<strong>` for bold, `<em>` for italic

### Spacing
- **Sections**: `space-y-4` for content within sections
- **Article**: `space-y-16` for major section spacing
- **Container**: `py-16 sm:py-20 space-y-16` for main content area

### Colors
- **Category tag**: `text-cyan-400`
- **Links**: `text-cyan-500 hover:underline`
- **Borders**: `border-border`
- **Background**: `bg-background`
- **Text**: `text-foreground`, `text-muted-foreground`

## After Creating an Article

### 1. Update `lib/articles.ts`

Add entry to the `articles` array:

```typescript
{
  slug: "article-slug",
  href: "/articles/article-slug",
  title: "Article Title",
  description: "Brief description for article listing",
  ruleIds: ["GCI0004", "GCI0003", "GCI0006"],
},
```

### 2. Update Sitemap

Add entry to `app/sitemap.ts`:

```typescript
{ url: `${BASE_URL}/articles/article-slug`, changeFrequency: "monthly", priority: 0.8 },
```

### 3. Add OG Image

Place 1200x630px image at:
```
public/og/article-slug.png
```

## Reading Time Guidelines

Estimate based on content:
- **2-3 min read**: ~400-600 words
- **4-5 min read**: ~800-1000 words
- **6-8 min read**: ~1200-1600 words
- **10+ min read**: 2000+ words

## SEO Best Practices

1. **Title**: Include main keyword, keep under 60 characters for search results
2. **Description**: 150-160 characters, compelling call-to-action
3. **Keywords**: 5-7 relevant keywords
4. **Headings**: Use h2 and h3 hierarchically, don't skip levels
5. **Links**: Link to related articles using anchor tags with descriptive text
6. **Images**: Include alt text on all images, use `/og/` images for social

## Example: Complete Article Template

See reference implementations:
- `app/articles/jellyfin-pr-16062-post-mortem/page.tsx` (full example)
- `app/articles/can-ai-code-review-be-deterministic/page.tsx` (alternative structure)
- `app/articles/azure-sdk-pr-57223-risk-analysis/page.tsx` (case study example)

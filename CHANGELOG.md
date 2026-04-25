# Changelog

All notable changes to GauntletCI are documented here.

Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
Versions follow [Semantic Versioning](https://semver.org/).

---

## [Unreleased]

---

## [2.0.4] - 2026-04-25

### Added
- `/about` page with founder bio and E-E-A-T author attribution
- Author bio component on all article pages (short variant in byline)
- Playwright e2e test suite: smoke, article, rule detail, and link-graph tests
- GitHub Actions workflow (`site-e2e.yml`) runs full Playwright suite on every push
- `SoftwareApplication` and `FAQPage` JSON-LD schemas on all 7 docs pages and all 30+ rule detail pages
- Pagefind full-text search: indexes all 53 pages at build time, ~3,456 words
- Search dialog component with Cmd/Ctrl+K shortcut, debounced input, and dark-theme styling
- "Product" dropdown in header nav consolidating Features / Proven Results / Quick Start anchors
- "Next steps" link grids at the bottom of `/docs/cli-reference`, `/docs/configuration`, and `/docs/local-llm`
- Per-rule detail pages at `/docs/rules/[ruleId]` (30 pages, one per detection rule)
- Contextual cross-links from articles back to relevant rule pages and vice versa

### Changed
- Header nav reduced from 5 flat links to 3 (Product dropdown, Docs, About)
- `npm run build` now runs `npx pagefind --site out` as a post-build step
- `data-pagefind-ignore` added to header, footer, and docs sidebar so boilerplate is excluded from search index

### Fixed
- `/docs/cli-reference`, `/docs/configuration`, `/docs/local-llm` had zero outbound content links (link-graph test catches this going forward)
- `/pricing` had no inbound content links from any other page

---

## [2.0.3] - 2026-04-24

### Added
- Rich PR review summary body: each GitHub PR review comment now includes Why, Action, and Evidence sections
- `--with-llm` enrichment attaches plain-English explanations to high-confidence findings

---

## [2.0.2] - 2026-04-24

### Added
- Duplicate finding grouping: identical findings across multiple files are collapsed into a single annotated entry
- Rich GitHub writer output: structured Markdown output for GitHub Actions annotations

---

## [2.0.1] - 2026-04-24

### Added
- "See it live" demo links in header, footer, and README pointing to the GauntletCI-Demo repository
- Footer added to docs layout (was missing from all `/docs` pages)

### Fixed
- Footer anchor links now use `/` prefix for correct cross-page navigation

---

## [2.0.0] - 2026-04-14

### Fixed
- Culture-invariant percent formatting in `MarkdownReportExporter` (e.g. `P1` rendered as `F1%` on non-en-US locales)

### Infrastructure
- NuGet packaging: added `GauntletCI.nuspec`, enriched `.csproj` package metadata
- NuGet publish workflow rewritten to use dotnet CLI with correct output path and push URL

---

[Unreleased]: https://github.com/EricCogen/GauntletCI/compare/v2.0.4...HEAD
[2.0.4]: https://github.com/EricCogen/GauntletCI/compare/v2.0.3...v2.0.4
[2.0.3]: https://github.com/EricCogen/GauntletCI/compare/v2.0.2...v2.0.3
[2.0.2]: https://github.com/EricCogen/GauntletCI/compare/v2.0.1...v2.0.2
[2.0.1]: https://github.com/EricCogen/GauntletCI/compare/v2.0.0...v2.0.1
[2.0.0]: https://github.com/EricCogen/GauntletCI/releases/tag/v2.0.0

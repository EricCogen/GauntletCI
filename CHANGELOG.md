# Changelog

All notable changes to GauntletCI are documented here.

Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
Versions follow [Semantic Versioning](https://semver.org/).

---

## [Unreleased]

### Added
- `.github/ISSUE_TEMPLATE/risky_diff.yml`: community issue template for submitting risky diffs
- `.github/ISSUE_TEMPLATE/false_positive.yml`: issue template for reporting false positives
- `.github/ISSUE_TEMPLATE/rule_request.yml`: issue template for suggesting new detection rules
- `SUPPORT.md`: support policy with paths for bugs, false positives, and security reports
- `docs/noise-and-false-positives.md`: explains what findings mean and recommends advisory mode first
- `docs/rules/README.md`: rule catalog index with status table
- `docs/rules/GCI0003-behavioral-change-detection.md`: individual rule page
- `docs/rules/GCI0004-breaking-change-risk.md`: individual rule page
- `docs/rules/GCI0006-edge-case-handling.md`: individual rule page
- `docs/rules/GCI0007-error-handling-integrity.md`: individual rule page
- `docs/rules/GCI0010-hardcoding-and-configuration.md`: individual rule page
- `docs/risky-diffs/README.md`: risky diff proof gallery index
- `docs/risky-diffs/efcore-linq-loop.md`: LINQ in loop example
- `docs/risky-diffs/dapper-null-forgiving.md`: null-forgiving operator example
- `docs/risky-diffs/stackexchange-redis-context-mutation.md`: shared context mutation example
- `docs/risky-diffs/sharpcompress-overflow.md`: integer overflow example
- `docs/risky-diffs/anglesharp-enum-removal.md`: enum member removal example
- README: GitHub Actions copy-paste workflow section
- README: GitHub Action inputs table
- README: "What to do with a finding" section
- README: rule ID non-contiguous explanation
- `docs/assets/`: assets folder for terminal demo GIF and future media
- README: "What you see on first run" section updated to reference terminal demo GIF (StackExchange.Redis PR#2995, GCI0007 swallowed exception)
- `--sensitivity` CLI flag: `strict | balanced | permissive` (default: `balanced`) filters findings by a Severity x Confidence 2D priority grid; `balanced` shows all Block plus Warn+Medium/High, `strict` shows Block+Medium/High only, `permissive` restores legacy all-Warn behavior
- `SensitivityThreshold` enum and `SensitivityFilter` static class in `GauntletCI.Core.Model`; filter logic is testable and independent of the CLI
- Compound risk summary line in report header when 4+ distinct rules trigger (replaces the removed synthetic `GCI_SYN_AGG` finding)

### Changed
- Move `CONTRIBUTING.md` and `SECURITY.md` from `.github/` to repo root for discoverability
- Add community-facing contribution sections above the developer guide in `CONTRIBUTING.md`
- `bug_report.yml`: replaced freetext environment with OS dropdown, added separate bash-rendered command field
- Behavioral Change Risk Formal Framework article: reformatted BCR definition block for readability; added "More formally" paragraph expanding on behavioral divergence potential
- `GCI_SYN_AGG` synthetic finding removed from `RuleOrchestrator.PostProcess`; compound risk (4+ rules) is now a summary line in the report header rather than a fake finding in the findings list
- `ConsoleReporter.Report`: findings count in the header now shows the sensitivity-filtered count; suppressed findings display a dim inline note with a tip to use `--sensitivity permissive`
- Exit code now respects the sensitivity threshold: findings hidden by `--sensitivity strict` do not cause a non-zero exit

### Fixed
- Site search not working on live site: CI was running `pnpm next build` directly, bypassing the `build` script in package.json that runs pagefind indexing after the Next.js build. Changed to `pnpm run build` so the pagefind index is generated and included in the deployed artifact.
- GCI0024 vs GCI0039: `new HttpClient()` no longer double-fires; GCI0039 (External Service Safety) is the sole reporter
- GCI0032 vs GCI0042: `throw new NotImplementedException` no longer double-fires; GCI0042 (TODO/Stub Detection) is the sole reporter
- GCI0012 vs GCI0029: log calls containing `token` (e.g. `_logger.Log("token = " + x)`) no longer trigger GCI0012 hardcoded-credential check; GCI0029 (PII Logging Leak) owns that shape
- GCI0043 vs GCI0006: `as`-cast on a line that also accesses `.Value` no longer double-fires; GCI0006 (Edge Case Handling) owns the `.Value` path
- All 5 compare pages (`vs-sonarqube`, `vs-codeql`, `vs-semgrep`, `vs-snyk`, `vs-codeclimate`) were missing Header and Footer; added to all pages
- GCI0006 false positive: expression-body methods with a nullable return type (e.g. `string? ToString() => (string?)Channel`) were incorrectly flagging the cast as a missing null parameter check; fixed by scoping `paramSection` to the actual parameter list only
- GCI0007 false positive: typed exception catches with a one-liner body (e.g. `catch (ChannelClosedException) { break; }`) were incorrectly classified as swallowed; rule now skips all specific typed exception catches

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

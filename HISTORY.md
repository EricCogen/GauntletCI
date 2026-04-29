# Project History

A narrative record of GauntletCI's major milestones and design decisions.

---

## 2026 - v2.x: GitHub Integration and Site SEO

### April 2026 - v2.0.x releases

GauntletCI reached its first stable v2.0 milestone with full NuGet packaging and
a working CI publish pipeline. The release series that followed focused on making
findings actionable inside GitHub pull requests.

v2.0.2 introduced duplicate finding grouping so reviewers see one clear signal per
risk category rather than repeated noise across files. v2.0.3 added structured PR
review comment bodies: every annotation now includes a Why section (why this matters),
an Action section (what to do), and an Evidence section (the exact lines that triggered
the rule).

### April 2026 - Marketing site and SEO buildout

With the core engine stable, effort shifted to the marketing site (`site/`, Next.js,
static export). Work in this phase:

- Rule-as-a-Page: 30+ individual rule detail pages at `/docs/rules/[ruleId]`, each
  with full rule description, severity rationale, and code examples
- Contextual loop: articles link to relevant rules, rule pages link back to articles
- Founder bio and `/about` page for E-E-A-T author trust signals
- JSON-LD `SoftwareApplication` and `FAQPage` schemas on all docs and rule pages
- Pagefind full-text search (client-side, zero backend, build-time indexed)
- Playwright e2e test suite including a link-graph test that enforces every page
  has at least one inbound and one outbound content link
- Releases page (`/releases`) auto-parses CHANGELOG.md at build time, eliminating
  manual duplication of version metadata

---

## Design decisions

### Why deterministic rules instead of ML?

Early prototypes explored embedding-based similarity to flag risky diffs. The signal
was noisy and the false-positive rate made the tool unusable as a pre-commit hook.
Deterministic rules have two properties ML approaches lack: they are auditable (you
can read exactly why a finding fired) and they are fast enough to block a commit
without a perceptible delay.

### Why .NET / C# only?

The initial corpus was drawn entirely from C# open-source repositories. The Roslyn
syntax tree gives the rule engine precise structural information (method signatures,
null guards, exception paths) that a regex-based approach over raw text cannot match.
Support for other languages is on the roadmap but C# remains the primary target.

### Why local-first?

The design constraint from day one was: no code leaves the developer's machine.
Diff content is sensitive. Any tool that requires a cloud round-trip for analysis
introduces a trust problem that slows adoption in enterprise environments. The local
ONNX LLM path extends this to AI enrichment: Phi-4 Mini runs in-process with no
network calls at analysis time.

### Why static export for the marketing site?

`output: 'export'` in `next.config.mjs` means the entire site is HTML files at
build time. This keeps hosting costs at zero (GitHub Pages / Cloudflare Pages),
eliminates server-side attack surface, and lets Pagefind build a full-text index
over the rendered output rather than raw source files.

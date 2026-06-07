# GauntletCI Documentation Hub

**Purpose:** Searchable guide to all GauntletCI documentation  
**Last Updated:** May 2026  
**Scope:** All versions, with phase-specific guides for Phase 21+

---

## Quick Navigation

### 🚀 Getting Started (All Users)
- **[README.md](/README.md)** — Project overview, installation, 5-minute quickstart
- **[CONTRIBUTING.md](/CONTRIBUTING.md)** — Development guide, local setup, coding standards
- **[CHARTER.md](/CHARTER.md)** — Project mission, values, principles

### 📋 By Role

**I'm a Developer**
→ Start with [README.md](/README.md), then explore [/docs/rules/](/docs/rules/) for specific warnings

**I'm DevOps/SRE**
→ Start with [/docs/operations/phase-21-monitoring.md](/docs/operations/phase-21-monitoring.md) (if using v2.4.0+)

**I'm an Architect**
→ Read [HISTORY.md](/HISTORY.md), then [/docs/architecture/](/docs/architecture/)

**I'm in Support**
→ Use [/docs/TROUBLESHOOTING.md](/docs/TROUBLESHOOTING.md) + [/docs/features-benefits.md](/docs/features-benefits.md)

---

## 📚 Core Documentation

### Rules & Detection

| Document | Purpose |
|----------|---------|
| [/docs/rules.md](/docs/rules.md) | Complete GCI rule reference (37 active rules) |
| [/docs/rules/](/docs/rules/) | Individual rule pages (GCI0001–GCI0059) |
| [/docs/best-practices.md](/docs/best-practices.md) | Recommended patterns (BP001-BP030) |
| [/docs/core-engineering-rules.md](/docs/core-engineering-rules.md) | Engineering invariants & principles |

### Architecture & Design

| Document | Purpose |
|----------|---------|
| [/docs/architecture.md](/docs/architecture.md) | System architecture overview |
| [/docs/architecture/adr-0004-phase-21-coordinations.md](/docs/architecture/adr-0004-phase-21-coordinations.md) | Phase 21 coordination pattern (NEW) |

### Operations & Deployment

| Document | Purpose |
|----------|---------|
| [/docs/operations/phase-21-monitoring.md](/docs/operations/phase-21-monitoring.md) | Production monitoring for Phase 21 (NEW) |
| [DEPLOYMENT_CHECKLIST_v2.4.0.md](/DEPLOYMENT_CHECKLIST_v2.4.0.md) | Step-by-step v2.4.0 deployment |
| [/docs/archived/](/docs/archived/) | Historical deployment & release notes |

### Troubleshooting & Configuration

| Document | Purpose |
|----------|---------|
| [/docs/troubleshooting/phase-21-tuning.md](/docs/troubleshooting/phase-21-tuning.md) | Phase 21 troubleshooting & tuning (NEW) |
| [/docs/TROUBLESHOOTING.md](/docs/TROUBLESHOOTING.md) | General tool troubleshooting |

### Releases & History

| Document | Purpose |
|----------|---------|
| [/docs/release-notes/](/docs/release-notes/) | Release notes (all versions) |
| [CHANGELOG.md](/CHANGELOG.md) | Technical version history |
| [HISTORY.md](/HISTORY.md) | Project narrative & milestones |

### Other Documentation

| Document | Purpose |
|----------|---------|
| [/docs/case-studies/](/docs/case-studies/) | Real-world detection examples |
| [/docs/change-risk-thesis.md](/docs/change-risk-thesis.md) | Behavioral change risk theory |
| [/docs/features-benefits.md](/docs/features-benefits.md) | Feature matrix & value proposition |
| [/docs/cli-reference.md](/docs/cli-reference.md) | Complete CLI command reference |
| [/docs/integrations/](/docs/integrations/) | Integration guides (GitHub, CI/CD) |
| [/docs/DEVELOPMENT.md](/docs/DEVELOPMENT.md) | Development workflow & testing |

---

## 🎯 Phase 21 Documentation (NEW - May 2026)

### What is Phase 21?

**Phase 21: Multi-Rule Coordination** implements systematic coordination between detection rules to reduce false positives from 40-50% to 20-30%.

**Three coordinations deployed:**
- **P0 (v2.4.0)** — Async Execution Model: GCI0016 ↔ GCI0039 + GCI0044
- **P1 (v2.5.0)** — Exception Handling: GCI0032 ↔ GCI0003 + GCI0016
- **P2 (v2.6.0)** — Resource Management: GCI0024 ↔ GCI0015

### Phase 21 Documentation Stack

| Document | Read This If | Length |
|----------|--------------|--------|
| [adr-0004-phase-21-coordinations.md](/docs/architecture/adr-0004-phase-21-coordinations.md) | You want to understand *why* we coordinate rules | 15 min |
| [phase-21-monitoring.md](/docs/operations/phase-21-monitoring.md) | You're operating Phase 21 in production | 20 min |
| [phase-21-tuning.md](/docs/troubleshooting/phase-21-tuning.md) | You need to troubleshoot or configure coordinations | 20 min |
| [RELEASE_NOTES_v2.4.0-phase21-coordinations.md](/docs/release-notes/RELEASE_NOTES_v2.4.0-phase21-coordinations.md) | You want a high-level summary of Phase 21 | 10 min |

### Phase 21 Quick Start

1. **Deploying Phase 21?**
   - Read: [adr-0004-phase-21-coordinations.md](/docs/architecture/adr-0004-phase-21-coordinations.md) (understand patterns)
   - Use: [phase-21-monitoring.md](/docs/operations/phase-21-monitoring.md) (monitoring guide)

2. **Phase 21 causing issues?**
   - Reference: [phase-21-tuning.md](/docs/troubleshooting/phase-21-tuning.md)
   - Diagnose: Common Issues section + Diagnostic Procedures

3. **Want to adjust coordination settings?**
   - See: [phase-21-tuning.md](/docs/troubleshooting/phase-21-tuning.md) → Adjusting Confidence Thresholds

4. **Incident response?**
   - See: [phase-21-monitoring.md](/docs/operations/phase-21-monitoring.md) → Incidents section

---

## 📖 Search by Problem

**"I see a GCI0024 warning. What does it mean?"**
→ [/docs/rules/GCI0024.md](/docs/rules/GCI0024.md)

**"I want to understand Phase 21 coordinations."**
→ [adr-0004-phase-21-coordinations.md](/docs/architecture/adr-0004-phase-21-coordinations.md)

**"My Phase 21 finding wasn't boosted. Why?"**
→ [phase-21-tuning.md](/docs/troubleshooting/phase-21-tuning.md) → Issue 1

**"Phase 21 is producing too many false positives."**
→ [phase-21-tuning.md](/docs/troubleshooting/phase-21-tuning.md) → Issue 2

**"How do I set up GauntletCI in my CI/CD?"**
→ [/docs/integrations/](/docs/integrations/)

**"What changed in v2.6.0?"**
→ [/docs/release-notes/RELEASE_NOTES_v2.4.0-phase21-coordinations.md](/docs/release-notes/RELEASE_NOTES_v2.4.0-phase21-coordinations.md)

---

## 📁 File Organization

```
GauntletCI/
├── README.md                       # Entry point (what is GauntletCI?)
├── CHANGELOG.md                    # Technical version history
├── HISTORY.md                      # Narrative history & decisions
├── CONTRIBUTING.md                 # Development guide
├── CHARTER.md                      # Mission & values
├── SECURITY.md                     # Security policy
│
├── docs/
│   ├── INDEX.md                    # Landing page (original)
│   ├── DOCUMENTATION.md            # This file (navigation hub)
│   │
│   ├── rules.md                    # Rule reference index
│   ├── rules/                      # Individual rule pages (GCI0001+)
│   ├── best-practices.md           # Best practices (BP001+)
│   ├── core-engineering-rules.md   # Engineering invariants
│   │
│   ├── architecture/
│   │   └── adr-0004-phase-21-coordinations.md   # Phase 21 design [NEW]
│   │   └── (other ADRs)
│   │
│   ├── operations/
│   │   └── phase-21-monitoring.md              # Phase 21 monitoring [NEW]
│   │
│   ├── troubleshooting/
│   │   └── phase-21-tuning.md                 # Phase 21 tuning [NEW]
│   │
│   ├── release-notes/
│   │   ├── RELEASE_NOTES_v2.4.0-phase21-coordinations.md
│   │   └── RELEASE_NOTES_v2.3.0-phase17-coordinations.md
│   │
│   ├── archived/
│   │   └── (older docs, historical reference)
│   │
│   ├── case-studies/                # Real-world examples
│   ├── integrations/                # CI/CD integration guides
│   │
│   ├── change-risk-thesis.md
│   ├── features-benefits.md
│   ├── cli-reference.md
│   ├── architecture.md
│   ├── DEVELOPMENT.md
│   ├── TROUBLESHOOTING.md
│   └── (other reference docs)
│
├── DEPLOYMENT_CHECKLIST_v2.4.0.md   # Current deployment guide
│
└── src/ / tests/
    └── (implementation code & tests)
```

---

## 🔄 Maintenance

### Adding New Documentation

When creating new docs:
1. **Choose appropriate directory** (architecture/, operations/, troubleshooting/, etc.)
2. **Add entry to this file** (DOCUMENTATION.md) with brief description
3. **Cross-link** from related documents
4. **Commit** with message: `docs: Add [title]`

### Archiving Documentation

Old or superseded docs:
1. Move to `/docs/archived/` with descriptive filename
2. Update links in this file and related docs
3. Keep for historical reference (never delete)

---

## 💡 Contributing to Documentation

- **Typos/clarity:** Fix directly
- **New sections:** Open an issue first to coordinate
- **Major rewrites:** Discuss in GitHub Discussions
- **Phase documentation:** Follow the Phase 21 structure (ADR + Monitoring + Tuning)

---

## 🔗 External Links

- **GitHub:** https://github.com/EricCogen/GauntletCI
- **Website:** https://gauntletci.com
- **NuGet:** https://www.nuget.org/packages/GauntletCI
- **Issues:** GitHub Issues
- **Discussions:** GitHub Discussions

---

## Questions?

1. **Search this file** (Ctrl+F for keyword)
2. **Browse the docs/** directory
3. **Open a GitHub Discussion** (Q&A)
4. **File an issue** if documentation is missing/unclear

# GauntletCI Documentation

Complete guide to GauntletCI - static code security analysis for the .NET ecosystem.

---

## 🚀 Quick Start

- **[Getting Started](./getting-started/)** - Setup and first analysis
- **[Main README](../README.md)** - Project overview and key features
- **[Installation & Usage](../README.md#installation)** - How to use GauntletCI

---

## 📚 Core Documentation

### Project Information
- **[Project Information](./project/)** - Charter, history, governance
  - [Charter](./project/charter.md) - Vision and mission
  - [History](./project/history.md) - 20+ years of evolution
  - [Security Policy](./security.md) - Vulnerability reporting

### Contributing & Support
- **[Contributing Guide](./contributing.md)** - How to contribute
- **[Support](./support.md)** - Getting help, community channels
- **[Code of Conduct]** - Community guidelines (if exists)

### Deployment & Release Management
- **[Current Deployment Guide](../DEPLOYMENT_CHECKLIST_v2.4.0.md)** - v2.4.0 deployment
- **[Release Notes](../RELEASE_NOTES_v2.4.0-phase21-coordinations.md)** - v2.4.0 features
- **[Archived Releases](./archives/release-notes/)** - Previous versions
- **[Archive Deployment Guides](./archives/deployment-checklists/)** - Historical checklists

### Technical Documentation
- **[Rule Reference](../site/app/docs/rules/)** - Security rules (GCI0001-GCI0053)
- **[Architecture](../docs/)** - System design and components
- **[API Documentation]** - REST API reference (if available)

---

## 🏗️ Directory Structure

```
GauntletCI/
├── README.md                           # Main project entry point
├── RELEASE_NOTES_v2.4.0-*.md          # Current release notes
├── DEPLOYMENT_CHECKLIST_v2.4.0.md     # Current deployment guide
│
├── docs/                               # Documentation root
│   ├── README.md                       # THIS FILE
│   ├── getting-started/                # Setup guides
│   ├── contributing.md                 # Contributing guidelines
│   ├── security.md                     # Security policy
│   ├── support.md                      # Support and help
│   │
│   ├── project/                        # Project metadata
│   │   ├── charter.md                  # Project charter
│   │   ├── history.md                  # Project history
│   │   └── README.md                   # Project info hub
│   │
│   └── archives/                       # Historical documents
│       ├── release-notes/              # Old release notes
│       └── deployment-checklists/      # Old deployment guides
│
├── assets/                             # Media files
│   └── images/                         # Logos and diagrams
│
├── site/                               # Website (Next.js)
├── src/                                # Source code (C#)
├── tests/                              # Test suite
└── ...other directories
```

---

## 🔍 Finding What You Need

| Looking for... | See... |
|----------------|--------|
| How to install GauntletCI | [README.md](../README.md) |
| Security rules reference | [site/app/docs/rules/](../site/app/docs/rules/) |
| How to contribute | [contributing.md](./contributing.md) |
| Project charter | [project/charter.md](./project/charter.md) |
| Previous releases | [archives/release-notes/](./archives/release-notes/) |
| Deployment guide | [DEPLOYMENT_CHECKLIST_v2.4.0.md](../DEPLOYMENT_CHECKLIST_v2.4.0.md) |
| Getting help | [support.md](./support.md) |
| Code of conduct | [Project information](./project/) |

---

## 📋 Documentation Standards

- **Markdown format** (.md) for all text documents
- **Relative links** for navigation within docs
- **Clear headings** (use # and ##, avoid deep nesting)
- **Code examples** when showing usage
- **Keep it simple** - aim for ≤10 screens per document

---

## 🔄 Keeping Docs Up-to-Date

When making changes:
1. Update relevant .md files in `/docs`
2. Update README.md if adding major features
3. Update RELEASE_NOTES if releasing new version
4. Add to archives when retiring old documentation

---

## 📞 Questions?

- See [support.md](./support.md) for help resources
- Check [contributing.md](./contributing.md) for development setup
- Review [project/](./project/) for governance and decision-making

---

*Last Updated: 2026-05-04*

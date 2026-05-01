# GauntletCI Case Studies

Deep-dive technical case studies on high-impact detection rules. Each article explores real-world failure modes, how GauntletCI detects the anti-pattern, false positive guards, and safe remediation patterns.

---

## Tier 1: High-Impact Rules

### [GCI0054 - Async Void Abuse](gci0054-async-void-abuse.md)
**Problem:** Fire-and-forget async methods bypass error handling, causing unhandled exceptions to crash request threads.

**Real incident:** Stack Overflow outages from unhandled exceptions in async void event handlers.

**Key takeaway:** Always return `Task` (not `void`) from async methods, except for framework event handlers.

---

### [GCI0055 - Method Signature Change](gci0055-method-signature-change.md)
**Problem:** Public method signature changes (parameter addition, removal, type change) break consuming code at compile time.

**Real incident:** .NET 10.0.7 regression where `Decrypt` method signature changed, breaking ASP.NET Core applications.

**Key takeaway:** Use overloads and optional parameters to extend functionality without breaking backward compatibility. Major version bumps required for breaking changes.

---

### [GCI0045 - Service Locator Anti-Pattern](gci0045-service-locator.md)
**Problem:** Service Locator hides dependencies, making code hard to test and refactor. Hidden coupling increases maintenance risk.

**Real incident:** Enterprise application with 47% of classes using ServiceLocator; circular dependencies discovered during refactoring.

**Key takeaway:** Use constructor-based dependency injection. Service Locator should only appear in legacy code migration scenarios.

---

## Tier 2: Security Rules

### [GCI0048 - Insecure Random Number Generation](gci0048-insecure-random.md)
**Problem:** `System.Random` is cryptographically predictable. Using it for security-sensitive values (tokens, keys, IDs) allows token prediction attacks.

**Real incident:** Drupal token prediction (2018), Java random prediction (2019) — attackers could guess authentication tokens within ~100 attempts.

**Key takeaway:** Always use `RandomNumberGenerator.GetBytes()` for security-sensitive values. Never use `System.Random`.

---

### [GCI0039 - Insecure Deserialization](gci0039-insecure-serialization.md)
**Problem:** Deserializing untrusted data into arbitrary types enables Remote Code Execution (RCE) via gadget chains.

**Real incident:** BinaryFormatter RCE (2020-2022, deprecated in .NET), ObjectInputStream gadget chains (Java 2014).

**Key takeaway:** Only deserialize to explicitly typed, safe DTOs. Never use `BinaryFormatter`, `LosFormatter`, or type-aware JSON deserialization (`TypeNameHandling.Auto`).

---

## Tier 3: Data Integrity

### [GCI0050 - SQL Column Truncation](gci0050-sql-truncation.md)
**Problem:** Reducing column size during schema migration silently truncates existing data. The database doesn't error — it just cuts the data.

**Real incident:** E-commerce platform reduced phone number column, truncating international numbers. Healthcare EHR lost clinical notes (HIPAA violation).

**Key takeaway:** Always validate existing data before reducing column sizes. Back up before migrations. Test in staging first.

---

## How to Use These Case Studies

### For Code Review
Use these articles as references when reviewing code changes that match the pattern. Link to the relevant case study in your review comment:

> "This looks like GCI0045 (Service Locator). See [the case study](gci0045-service-locator.md) for safer alternatives using dependency injection."

### For Learning
Each article includes:
- **Real-world failure modes** with actual incidents and business impact
- **Code examples** showing vulnerable patterns and safe remediation
- **Detection logic** explaining how GauntletCI identifies the issue
- **False positive guards** documenting patterns that don't get flagged
- **Remediation checklists** for fixing the issue in production

### For Documentation
Link these case studies from:
- Rule detail pages in `/docs/rules/`
- CHANGELOG and release notes
- GitHub Releases announcements
- Team wiki or internal documentation

### For Training
Share these articles with team members learning about:
- Security best practices (GCI0048, GCI0039, GCI0050)
- API design and compatibility (GCI0055)
- Async/await patterns (GCI0054)
- Dependency injection and testability (GCI0045)

---

## Coverage Summary

| Rule | Category | Focus | Status |
|------|----------|-------|--------|
| GCI0054 | Async | Reliability | ✅ Complete |
| GCI0055 | API Design | Compatibility | ✅ Complete |
| GCI0045 | Architecture | Testability | ✅ Complete |
| GCI0048 | Security | Cryptography | ✅ Complete |
| GCI0039 | Security | Deserialization | ✅ Complete |
| GCI0050 | Data Integrity | Schema Safety | ✅ Complete |

---

## Future Expansion

Planned case studies for high-impact rules:
- GCI0036 - Pure Context Mutation
- GCI0041 - Test Quality Gaps
- GCI0001 - Diff Integrity

---

## References & Attribution

- Real-world incidents sourced from CVEs, security databases, and published incident reports
- Examples follow GauntletCI's actual detection patterns and false positive guards
- Case studies are part of Phase 9 documentation cycle

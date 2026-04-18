# GauntletCI — Features & Benefits

---

## What It Does

GauntletCI analyzes the exact lines added or removed in a pull request and flags patterns that introduce unvalidated behavioral risk — before code is merged.

---

## Features

### 25 Built-in Detection Rules

#### Behavior & Contract Safety
- Detects removed logic (return, throw, if/else, boolean operators) without matching test changes
- Flags public API removals, signature changes, and dropped [Obsolete] attributes
- Catches breaking serialization changes — removed [JsonProperty], [Column], [DataMember] attributes and dropped enum members

#### Security
- SQL injection via string concatenation or interpolation
- Weak algorithms: MD5, SHA1, DES, RC2, 3DES
- Dangerous APIs: Assembly.Load, Activator.CreateInstance, Process.Start
- Hardcoded secrets: password, token, apikey assignments
- Hardcoded IPs, URLs, connection strings, port numbers, environment names
- Insecure deserialization (TypeNameHandling.All/Auto)
- [AllowAnonymous] added to previously authorized controllers

#### Data & State Integrity
- Unchecked numeric casts
- Mass field assignment without validation
- Unsafe HTTP input binding without allowlist
- SQL IGNORE patterns
- Removed idempotency guards on POST endpoints
- Raw INSERT without upsert guards
- Event handler registration without deduplication

#### Async, Concurrency & Resources
- async void (fire-and-forget)
- Blocking async calls: .Result, .Wait()
- lock(this) antipattern
- Thread.Sleep in async contexts
- Static mutable fields without synchronization
- Disposable types allocated without using or try/finally
- Direct HttpClient instantiation without timeout or CancellationToken

#### Privacy & Observability
- PII terms (email, ssn, creditcard, name, address) inside log calls
- Evidence automatically redacted in output for security and PII findings

#### Code Quality & Correctness
- Empty or silent catch blocks
- Removed error-level logging from catch blocks
- throw new without matching Assert.Throws in tests
- Null-forgiving operator (!) used 2+ times in added lines
- as-casts without null checks nearby
- Method parameters added without null/range validation
- .Value access without null guards

#### Architecture & Design
- Service locator anti-patterns (GetService, GetRequiredService)
- Direct instantiation of *Service/*Repository/*Manager types
- Captive dependencies (singleton capturing scoped/transient)
- Layer import violations (configurable forbidden dependency pairs)
- Assignment inside property getters or [Pure] methods
- Single-use interfaces, abstract classes with no abstract members
- Passive delegation wrappers

#### Test Quality
- Tests silenced with [Skip] or [Ignore]
- Uninformative test method names (Test1, TestMethod)
- Test methods with no assertions
- TODO, FIXME, HACK comments
- throw new NotImplementedException in non-test files

#### Consistency & Naming
- Mixed sync/async naming (Foo and FooAsync in same file)
- CRUD verb inversions (Get→Delete, Add→Remove)
- Boolean property name inversions (IsEnabled→IsDisabled)
- LINQ inside loops; unbounded collection growth inside loops

---

### CLI

| Command | What it does |
|---|---|
| `analyze --staged` | Analyzes staged git changes |
| `analyze --diff` | Analyzes a diff file |
| `analyze --commit` | Analyzes any commit SHA |
| `postmortem --commit` | Runs analysis on a past commit — see what GauntletCI would have caught |
| `audit export` | Exports full scan history as JSON or CSV, filterable by date or count |
| `audit stats` | Summary: total scans, findings count, top 5 rules fired |
| `ignore` | Adds a rule suppression to .gauntletci-ignore, with optional path glob |
| `init` | Creates .gauntletci.json and installs pre-commit hooks (bash + PowerShell) |
| `feedback up\|down` | Rates the last analysis — stored locally and optionally in anonymous aggregate |
| `telemetry` | Opt-in/out controls: shared, local, or off |
| `llm seed` | Seeds 11 curated .NET expert facts into local vector store |
| `llm distill` | Extracts expert facts from GitHub issues via local Ollama model |
| `mcp serve` | Starts a stdio MCP server — exposes analyze, audit, and rule listing to any MCP-compatible AI assistant |

---

### Output Modes

- **Terminal** — findings grouped by severity (Block / Warn / Info), with rule ID, evidence, why it matters, and suggested action
- **JSON** — full machine-readable EvaluationResult
- **GitHub Annotations** — `::error::` / `::warning::` workflow commands for CI check integration
- **GitHub PR Review Comments** — posts findings as inline comments directly on the diff via the GitHub Review API (requires `pull-requests: write`)

---

### GitHub Actions

Drop-in composite action with inputs for commit SHA, fail-on-findings, inline PR comments, ASCII mode, and .NET/GauntletCI version pinning. Outputs `findings-count`.

---

### Local LLM (fully offline)

- Ollama-backed enrichment explains high-confidence findings in plain English
- Expert knowledge vector store matches findings to curated .NET facts with similarity scores
- Fact distillation from real GitHub issue data via local model
- No code, no findings, no file paths leave the machine

---

## Benefits

| Benefit | Why it matters |
|---|---|
| Catches what green tests miss | Tests pass even when behavior changes without matching validation |
| Runs in under one second | No compile, no AST, no network — regex and structural heuristics on diff lines only |
| Zero noise about style | Every rule targets behavioral or security risk, not formatting or naming preferences |
| Works anywhere git does | Pre-commit hook, CI pipeline, or ad-hoc on any commit SHA |
| Fully private by default | All analysis is local — telemetry is opt-in, anonymous, and excludes all code |
| Auditable | Every scan logged to ~/.gauntletci/audit-log.ndjson; exportable as CSV for compliance |
| Plugs into AI assistants | MCP server lets Claude, Cursor, Copilot, and Windsurf call GauntletCI mid-conversation |
| Configurable without breaking | Per-rule severity override via .gauntletci.json or .editorconfig; suppression via .gauntletci-ignore |

---

## Validated Against Real OSS PRs

22 rules validated against real .NET pull requests from top OSS projects.
All findings were human-reviewed against the actual diff — not machine-labeled.

| Rule | What was caught | Example project |
|---|---|---|
| GCI0003 — Removed logic without tests | Return/throw removed from production code with no test diff | Multiple repos |
| GCI0004 — Breaking API change | Public method signatures changed or removed | Multiple repos |
| GCI0006 — Edge case handling | Public `OpenAsyncWriteStream(string path, …)` added with no null guard on `path` | SharpCompress |
| GCI0007 — Breaking serialization change | `[JsonProperty]` / `[DataMember]` attributes removed from DTO | Multiple repos |
| GCI0010 — Hardcoded secret | `_secretKey = "secretkey"` — credential-like string literal in AWS signing test | aws-sdk-net |
| GCI0012 — Hardcoded secret | Password literal assigned in production code | Multiple repos |
| GCI0015 — Unchecked cast | `(int)input.Position` — `Stream.Position` is `long`; overflows for files >2 GB in a compression library | SharpCompress |
| GCI0016 — Async void | `async void` handler in production event wiring | Multiple repos |
| GCI0021 — Data schema compatibility | `Tentative`, `Certain`, `Irrelevant` public enum members removed from `TextSource` — breaking API change | AngleSharp |
| GCI0022 — Idempotency / retry safety | Six `MessageBus<T>.Subscribers +=` handlers registered without deduplication guard — static event accumulation | ILSpy |
| GCI0024 — Disposable without using | `FileStream` returned without `using`, leaking the handle | Multiple repos |
| GCI0032 — Untested throw paths | 3 `throw new` statements added with no `Assert.Throws` in the diff | aaubry/YamlDotNet |
| GCI0036 — Pure context mutation | `_maxNodes = CommandLine.GetInt32(…)` — field mutation inside a property getter, re-reads CLI args on every access | Akka.NET |
| GCI0038 — Service locator | `GetRequiredService<T>()` in production (non-test) IoC composition | DevToys |
| GCI0039 — Direct HttpClient | `new HttpClient()` used directly, bypassing factory and timeout | googleapis/google-api-dotnet-client, grpc/grpc-dotnet, restsharp/RestSharp |
| GCI0041 — Silenced tests | `[Skip]` placed on existing passing tests | Multiple repos |
| GCI0042 — TODO/Stub detection | `throw new NotImplementedException()` in 4 production JWT decoder/encoder files — unshipped stubs merged | DevToys |
| GCI0043 — Nullability and type safety | Null-forgiving `!` operator used 65 times in `SqlMapper.cs` when enabling nullable annotations — suppresses compile-time null safety | Dapper |
| GCI0044 — Performance hotpath risk | LINQ `.Where()` inside outer loop in EF Core model diff logic — O(n²) over entry lists | dotnet/efcore |
| GCI0045 — Complexity control | `abstract class RespAttributeReader` added with no abstract members — abstract keyword without contract | StackExchange.Redis |
| GCI0046 — Pattern consistency deviation | `Subscribe()` and `SubscribeAsync()` both added in same file — mixed sync/async API surface on same base name | StackExchange.Redis |
| GCI0047 — Naming/contract alignment | `IsValid` property renamed to `IsInvalid` in `UnmanagedMemoryHandle` — boolean polarity inversion at all call sites | SixLabors/ImageSharp |

Rules not yet validated from corpus:
- **GCI0001** — Diff integrity check (enabled for users; disabled in GauntletCI team's own config); no clear valid corpus example found
- **GCI0029** — PII in logs; high FP rate on 'name' term (see caveats below); not included in showcase
- **GCI0035** — Layer import violations; requires `ForbiddenImports` config to fire; not validatable from open-source corpus

Rules with known precision caveats (as of current version):
- **GCI0024** — MemoryStream in test helpers sometimes triggers; test files are not excluded
- **GCI0029** — 'name' term fires on logging context keys and property names, not just PII; high FP rate
- **GCI0038** — `GetService` calls in xUnit test fixtures sometimes trigger; test files are not excluded
- **GCI0043** — `as`-cast check fires on XML doc comment lines containing "reported as"; comment lines not excluded
- **GCI0045** — delegation-wrapper check fires on test helper classes; test files not excluded from this sub-check
- **GCI0046** — mixed sync/async fires on intentional sync+async library APIs (e.g., both `Subscribe` and `SubscribeAsync` in a pub/sub library)

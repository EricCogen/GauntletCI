# GauntletCI: Features & Benefits

---

## What It Does

GauntletCI analyzes the exact lines added or removed in a pull request and flags patterns that introduce unvalidated behavioral risk: before code is merged. No compilation, no AST, no network. Results in under one second.

---

## Features

### 30 Built-in Detection Rules

#### Behavior & Contract Safety
- Removed logic (return, throw, if/else, boolean operators) without matching test changes
- Public API removals, signature changes, and dropped `[Obsolete]` attributes
- Breaking serialization changes: removed `[JsonProperty]`, `[Column]`, `[DataMember]` attributes and dropped public enum members

#### Security
- SQL injection via string concatenation or interpolation
- Weak algorithms: MD5, SHA1, DES, RC2, 3DES
- Dangerous APIs: `Assembly.Load`, `Activator.CreateInstance`, `Process.Start`
- Hardcoded secrets: password, token, apikey literal assignments
- Hardcoded IPs, URLs, connection strings, port numbers, environment names
- Insecure deserialization (`TypeNameHandling.All/Auto`)
- `[AllowAnonymous]` added to previously authorized controllers
- Insecure random: `System.Random` / `new Random()` used in security contexts (token generation, session IDs, keys)

#### Data & State Integrity
- Unchecked numeric casts (e.g. `(int)longValue`)
- Mass field assignment without validation
- Unsafe HTTP input binding without allowlist
- SQL `IGNORE` / `INSERT OR IGNORE` patterns
- Removed idempotency guards on POST endpoints
- Raw INSERT without upsert guards
- Event handler registration without deduplication
- SQL column truncation risk: `VARCHAR(N)` mismatches between model and schema
- Float/double equality comparisons (`==` / `!=` on `float` or `double`)

#### Async, Concurrency & Resources
- `async void` (fire-and-forget)
- Blocking async calls: `.Result`, `.Wait()`
- `lock(this)` antipattern
- `Thread.Sleep` in async contexts
- Static mutable fields without synchronization
- Disposable types allocated without `using` or `try/finally`
- Direct `HttpClient` instantiation without timeout or `CancellationToken`

#### Privacy & Observability
- PII terms (email, ssn, creditcard, address) inside log calls
- Evidence automatically redacted in output for security and PII findings

#### Code Quality & Correctness
- Empty or silent catch blocks
- Removed error-level logging from catch blocks
- `throw new` without matching `Assert.Throws` in tests
- Null-forgiving operator (`!`) used 2+ times in added lines
- `as`-casts without null checks nearby (skips comment lines and string literals)
- Public/protected method parameters added without null/range validation
- `.Value` access without null guards (skips comment lines)

#### Architecture & Design
- Service locator anti-patterns (`GetService`, `GetRequiredService`)
- Direct instantiation of `*Service` / `*Repository` / `*Manager` types
- Captive dependencies (singleton capturing scoped/transient)
- Layer import violations (configurable forbidden dependency pairs)
- Assignment inside property getters or `[Pure]` methods
- Abstract classes with no abstract members; single-use interfaces
- Passive delegation wrappers

#### Test Quality
- Tests silenced with `[Skip]` or `[Ignore]`
- Uninformative test method names (`Test1`, `TestMethod`)
- Test methods with no assertions
- TODO, FIXME, HACK comments
- `throw new NotImplementedException` in non-test files

#### Consistency & Naming
- Mixed sync/async naming (`Foo` and `FooAsync` in same file)
- CRUD verb inversions (`Get->Delete`, `Add->Remove`)
- Boolean property name inversions (`IsEnabled->IsDisabled`)
- LINQ inside loops; unbounded collection growth inside loops

---

### CLI

#### Analyze

| Flag | What it does |
|---|---|
| `--staged` | Staged changes (`git diff --cached`) |
| `--unstaged` | Unstaged changes (`git diff`) |
| `--all-changes` | All local changes (`git diff HEAD`) |
| `--diff <file>` | Any `.diff` file |
| `--commit <sha>` | Any commit SHA |
| `--output json` | Machine-readable JSON output |
| `--severity info\|warn\|block` | Minimum severity to display |
| `--sensitivity strict\|balanced\|permissive` | Confidence-based noise filter: `strict` shows Block + High/Medium confidence only; `balanced` (default) shows all Block + Warn High/Medium; `permissive` shows everything |
| `--no-baseline` | Ignore baseline; show all findings |
| `--show-context N` | Include N surrounding diff lines around evidence |
| `--github-annotations` | Emit `::error::` / `::warning::` workflow commands |
| `--github-pr-comments` | Post findings as inline GitHub PR review comments |
| `--pr-comment-suggest` | Print PR review body to stdout without posting |
| `--with-llm` | Enrich high-confidence findings with local Ollama model |
| `--with-expert-context` | Attach nearest expert fact from vector store to findings |

#### Other Commands

| Command | What it does |
|---|---|
| `baseline create` | Snapshot all current findings as the baseline |
| `baseline clear` | Remove the baseline file |
| `baseline show` | Print current baseline contents |
| `postmortem --commit <sha>` | Run analysis on a past commit: see what GauntletCI would have caught |
| `doctor` | Validate environment: config, rules, Ollama connectivity, baseline status |
| `audit export` | Export full scan history as JSON or CSV |
| `audit stats` | Summary: total scans, findings count, top rules fired |
| `ignore` | Add a rule suppression to `.gauntletci-ignore` with optional path glob |
| `init` | Create `.gauntletci.json` and install pre-commit hooks (bash + PowerShell) |
| `feedback up\|down` | Rate the last analysis (stored locally; optionally in anonymous aggregate) |
| `telemetry` | Opt-in/out controls: shared, local, or off |
| `llm seed` | Seed 11 curated .NET expert facts into local vector store |
| `llm distill --input <file>` | Extract expert facts from GitHub issues via local Ollama model |
| `mcp serve` | Start a stdio MCP server exposing analyze, audit, and rule listing |

---

### Baseline Delta Mode

Run GauntletCI on a branch over time without accumulating noise. `baseline create` snapshots the current finding set. Subsequent `analyze` runs suppress baselined findings and surface only net-new issues. Fingerprints are stable across line-number drift caused by unrelated edits.

---

### GitHub Actions

Drop-in composite action with inputs for commit SHA, fail-on-findings, inline PR comments, ASCII mode, and .NET/GauntletCI version pinning. Outputs `findings-count`. Posts findings directly as inline review comments on the diff: no manual review step required.

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
| Runs in under one second | No compile, no AST, no network: structural heuristics on diff lines only |
| Zero noise about style | Every rule targets behavioral or security risk, not formatting or preferences |
| Works anywhere git does | Pre-commit hook, CI pipeline, or ad-hoc on any commit SHA |
| Baseline suppression | Teams can snapshot known findings and only see what is new: no alert fatigue |
| Fully private by default | All analysis is local: telemetry is opt-in, anonymous, and excludes all code |
| Auditable | Every scan logged to `~/.gauntletci/audit-log.ndjson`; exportable as CSV |
| Plugs into AI assistants | MCP server lets Claude, Cursor, Copilot, and Windsurf call GauntletCI mid-conversation |
| Configurable without breaking | Per-rule severity override via `.gauntletci.json`; suppression via `.gauntletci-ignore` |

---

## Validated Against Real OSS PRs

22 rules have been manually confirmed against a corpus of real .NET pull requests across top OSS projects. All findings were human-reviewed against the actual diff: not machine-labeled.

| Rule | What was caught | Example project |
|---|---|---|
| GCI0003: Removed logic without tests | Return/throw removed from production code with no test diff | Multiple repos |
| GCI0004: Breaking API change | Public method signatures changed or removed | Multiple repos |
| GCI0006: Edge case handling | `OpenAsyncWriteStream(string path, ...)` added with no null guard on `path` | SharpCompress |
| GCI0007: Breaking serialization change | `[JsonProperty]` / `[DataMember]` attributes removed from DTO | Multiple repos |
| GCI0010: Hardcoded secret | `_secretKey = "secretkey"`: credential-like string literal in AWS signing code | aws-sdk-net |
| GCI0012: Hardcoded secret | Password literal assigned in production code | Multiple repos |
| GCI0015: Unchecked cast | `(int)input.Position`: `Stream.Position` is `long`; overflows for files >2 GB | SharpCompress |
| GCI0016: Async void | `async void` handler in production event wiring | Multiple repos |
| GCI0021: Data schema compatibility | `Tentative`, `Certain`, `Irrelevant` public enum members removed from `TextSource` | AngleSharp |
| GCI0022: Idempotency / retry safety | Six `MessageBus<T>.Subscribers +=` handlers registered without deduplication guard | ILSpy |
| GCI0024: Disposable without using | `FileStream` returned without `using`, leaking the handle | Multiple repos |
| GCI0032: Untested throw paths | 3 `throw new` statements added with no `Assert.Throws` in the diff | aaubry/YamlDotNet |
| GCI0036: Pure context mutation | `_maxNodes = CommandLine.GetInt32(...)`: field mutation inside a property getter | Akka.NET |
| GCI0038: Service locator | `GetRequiredService<T>()` in production IoC composition | DevToys |
| GCI0039: Direct HttpClient | `new HttpClient()` used directly, bypassing factory and timeout | googleapis/google-api-dotnet-client, grpc/grpc-dotnet, restsharp/RestSharp |
| GCI0041: Silenced tests | `[Skip]` placed on existing passing tests | Multiple repos |
| GCI0042: TODO/Stub detection | `throw new NotImplementedException()` in 4 production JWT files: unshipped stubs merged | DevToys |
| GCI0043: Nullability and type safety | Null-forgiving `!` used 65 times in `SqlMapper.cs` when enabling nullable annotations | Dapper |
| GCI0044: Performance hotpath risk | LINQ `.Where()` inside outer loop in EF Core model diff logic: O(n^2) | dotnet/efcore |
| GCI0045: Complexity control | `abstract class RespAttributeReader` added with no abstract members | StackExchange.Redis |
| GCI0046: Pattern consistency deviation | `Subscribe()` and `SubscribeAsync()` both added: mixed sync/async API surface | StackExchange.Redis |
| GCI0047: Naming/contract alignment | `IsValid` renamed to `IsInvalid`: boolean polarity inversion at all call sites | SixLabors/ImageSharp |

Rules not yet validated from corpus:
- **GCI0001**: Diff integrity check; disabled in GauntletCI own CI config; no corpus example found
- **GCI0029**: PII in logs; high FP rate on broad terms like `name`; excluded from showcase
- **GCI0035**: Layer import violations; requires `ForbiddenImports` config to fire
- **GCI0048**: Insecure random; newly added; corpus validation pending
- **GCI0049**: Float equality; newly added; corpus validation pending
- **GCI0052**: Dependency bot API drift; fires when bot-updated package appears in source without API surface update; corpus validation pending
- **GCI0053**: Lockfile changed without source; fires when lockfile changes with no corresponding .cs changes; corpus validation pending

Known precision caveats (as of current version):
- **GCI0029**: `name` term fires on logging context keys and property names, not just PII; use `.gauntletci-ignore` to suppress per path
- **GCI0038**: `GetService` calls in xUnit test fixtures can trigger; suppress with `.gauntletci-ignore`
- **GCI0046**: fires on intentional sync+async library APIs; suppress when both forms are deliberate design

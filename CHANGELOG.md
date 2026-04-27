# Changelog

All notable changes to GauntletCI are documented here.

Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
Versions follow [Semantic Versioning](https://semver.org/).

---

## [Unreleased]

### Changed
- **GCI0004 rewritten as "Breaking Change Risk" ([Obsolete]-only detection)**: Root-cause analysis of 618 corpus fixtures showed all 27 labeled-True fixtures contain `[Obsolete]` transitions in production C# files; the prior `CheckRemovedPublicApi` check fired on 117 FPs (10.7% precision) with zero additional TPs. New rule: `CheckObsoleteAdded` fires when `[Obsolete]` is added to production C# (active deprecation), `CheckObsoleteRemoved` fires when `[Obsolete]` is removed (guard stripped). `WellKnownPatterns.IsGeneratedFile` extended to also match `.netstandard\d+\.\d+\.cs` API surface manifest files (previously only numeric TFMs like `.net8.0.cs` were matched). Labeler updated to use `prodCsLines`/`prodCsRemovedLines` instead of `addedLines` (prevents false signals from `[Obsolete]` text in `.md` documentation files and test helpers); labeler now also fires for `[Obsolete]` removed (not just added), matching the rule's `CheckObsoleteRemoved` coverage. Corpus score: 10.7% precision / 51.9% recall -> 100% precision / 100% recall (Silver tier, 618 fixtures).
- **GCI0047 rewritten as "Naming/Contract Alignment" (precision hardening)**: Both the rule and labeler now apply a both-sides guard to CRUD verb detection - if the same verb+suffix appears in both removed and added lines (reformatting, not renaming), it is skipped. This eliminates spurious detections on PRs that convert methods to expression-body syntax while keeping both `Add(...)` and `Remove(...)` on both sides of the diff. The labeler's CRUD detection was also corrected to use method-signature context (access modifier required) and to mirror the rule's `ContradictoryPairs` set exactly, preventing label noise from call-site matches. `ContradictoryPairs` expanded from 7 to 16 unique pairs (added Create/Insert/Save/Find/Fetch/Load crossed with Delete/Remove). Boolean inversion check (`CheckBooleanNamingInversion`) gained the same both-sides guard. Test file guard added at the file level. Spec entry added to docs/rules.md Tier 2. Corpus score: no false positives; corpus does not contain fixtures with genuine same-file CRUD contradictions (all prior apparent TPs were reformatting noise).
- **GCI0021 rewritten as "Data & Schema Compatibility" (precision + recall uplift)**: Root-cause investigation of 18 corpus FPs identified three categories of noise: (1) enum member removal without serialization attributes (non-serialized API enums, not schema concerns), (2) labeler over-matching on "Migration" keyword in class names and non-.cs files, (3) deleted file blind spot in ChangedFileAnalyzer. Rule fixes: `IsEnumMember` now rejects lines ending in `;` (class field assignments, not enum members); `CheckRemovedEnumMembers` now only fires when the immediately preceding removed line inside the enum body is a serialization attribute (e.g. `[JsonProperty("x")]`) - undecorated enum API changes are GCI0004 territory, not schema compatibility. Labeler fixes: `ExtractRemovedLinesFromProductionCsFiles` now defers file classification until `+++ b/` is seen, correctly excluding deleted files (rule cannot see their content anyway); `IsEfMigrationCsFile` now excludes test files via `TestFileClassifier`; `hasMigrationModified` now requires `migrationBuilder.Drop*/Alter*/Rename*/Create*` in removed lines rather than triggering on any change to files in a migrations directory (scaffolding/processor changes are not schema risks). Corpus score: 28.6% precision / 10% recall -> 100% precision / 100% recall (Silver tier, 618 fixtures).
- **GCI0012 rewritten as "Security Risk" (precision + recall uplift)**: Hardcoded credential detection now filters production-CS-only lines from diffs (XML, config, test, benchmark, generated files excluded). Added `IsBenignLiteralValue` guard to skip empty strings, short strings, and well-known HTTP scheme names ("Bearer", "Basic", "Token") that were firing on property defaults. Added weak-hash heuristics (MD5.Create, SHA1.Create, new MD5CryptoServiceProvider, etc.) and SQL-injection heuristics (string literals starting with SELECT/INSERT/UPDATE/DELETE) to the Silver labeler. Benchmark directory path segments now excluded from production-CS extraction. Corpus score uplift: 40% precision / 5.9% recall -> 50% precision / 100% recall (Silver tier, 618 fixtures).
- **GCI0016 rewritten as "Async Concurrency Risk"**:Dropped the static mutable field check (51 corpus FPs, ~0 TPs - private primitives dominate and generate noise on config sentinels and singletons). Fixed `.Result` ambiguity: now only flags when preceded by `)` (method-call chain) or when the expression contains explicit Task/Async context - domain property names like `response.Result` and `exception.Result` no longer fire. Rule renamed from "Concurrency and State Risk" to "Async Concurrency Risk" to reflect the tighter scope. All four remaining checks (async void, .Wait()/.GetAwaiter().GetResult()/.Result, lock(this), Thread.Sleep) are now precise and corpus-validated.

### Added
- **Test coverage gap enricher** (`corpus test-coverage enrich`): `TestCoverageEnricher` parses changed file paths from `diff.patch` and classifies each `.cs` file as production (not generated, not in test folder) or test (filename ends in `Tests.cs`/`Test.cs`/`Specs.cs` or path contains `test`/`tests`/`spec`/`specs` folder). Computes `test_coverage_gap` (prod changes with no test changes) and `test_to_prod_ratio`. Results written to `test_coverage_enrichments`. Fixtures with a coverage gap and social score < 0.5 contribute to `UNVALIDATED_BEHAVIORAL_RISK` classification. No HTTP client or GitHub API needed. Options: `--fixtures`, `--db`, `--tier`, `--limit`.
- **Diff entropy enricher** (`corpus diff-entropy enrich`): `DiffEntropyEnricher` computes Kamei et al. (2013) JIT defect prediction features from `diff.patch`: NF (file count), ND (distinct parent directories), NS (distinct top-level namespaces estimated from first two path segments), total lines changed, Shannon change entropy (bits), and normalized entropy in [0,1]. High normalized entropy (>= 0.8) + low social validation (< 0.4) triggers `HOT_PATH_UNREVIEWED` composite label. Results written to `diff_entropy_enrichments`. No HTTP client needed. Options: `--fixtures`, `--db`, `--tier`, `--limit`.
- **EF migration enricher** (`corpus ef-migration enrich`): `EFMigrationEnricher` detects Entity Framework Core migration files and SQL DDL changes using both path-based rules (EF timestamp filename in `/Migrations/` folder, `.sql` files, `*ContextModelSnapshot.cs`) and content-based rules (added lines containing `migrationBuilder.` calls, SQL DDL keywords such as `CREATE TABLE`/`ALTER TABLE`/`DROP COLUMN`, EF data annotations `[Table(`, `[Column(`, `[ForeignKey(`, `[Index(`). Computes `migration_confidence` (0.95 for migration files, 0.85 for SQL/DDL, 0.75 for EF content). Detected fixtures are classified as `HIGH_RISK_GHOST` and additionally receive a GCI0021 expected finding written directly via `WriteEfMigrationFindingAsync`. No HTTP client needed. Options: `--fixtures`, `--db`, `--tier`, `--limit`.
- **`test_coverage_enrichments` DB table**: Migration adds `test_coverage_enrichments (fixture_id, repo, prod_cs_count, test_cs_count, test_coverage_gap, test_to_prod_ratio, analyzed_at_utc)`.
- **`diff_entropy_enrichments` DB table**: Migration adds `diff_entropy_enrichments (fixture_id, repo, file_count, directory_count, namespace_count, total_lines_changed, change_entropy, normalized_entropy, analyzed_at_utc)`.
- **`ef_migration_enrichments` DB table**: Migration adds `ef_migration_enrichments (fixture_id, repo, migration_detected, has_migration_file, has_sql_file, has_ef_content, has_ddl_content, migration_confidence, analyzed_at_utc)`.

(`corpus nuget-advisory enrich`): `NuGetAdvisoryEnricher` parses NuGet package names from added lines in `.csproj` and `packages.lock.json` diff hunks, then queries the GitHub Advisory Database GraphQL API for known vulnerabilities per package. Advisory counts and highest severity are written to `nuget_advisory_enrichments`. When a fixture has NuGet advisories in its diff and is not a Dependabot PR, the composite labeler classifies it as `HIGH_RISK_GHOST` with 0.85 confidence (GHSA is a high-quality oracle). Options: `--fixtures`, `--db`, `--tier`, `--delay-ms`, `--limit`.
- **90-day file churn enricher** (`corpus file-churn enrich`): `FileChurnEnricher` fetches 90-day commit frequency for each changed `.cs` file from the GitHub Commits API, computes a hotspot score (`min(churn/30, 1.0)`), and writes per-file rows to `file_churn_enrichments`. Fixtures with max hotspot score >= 0.7 and social signal score < 0.5 trigger the `HOT_PATH_UNREVIEWED` composite label. Options: `--fixtures`, `--db`, `--tier`, `--delay-ms`, `--limit`.
- **Review comment NLP enricher** (`corpus review-nlp enrich`): `ReviewCommentNlpEnricher` fetches PR review inline comments and review body text from the GitHub API, then applies a 14-rule keyword taxonomy to extract intent signals (race condition, null reference, memory leak, security, breaking change, etc.) and map them to GCI rule IDs. Results are written to `review_comment_nlp_enrichments`. Provides a `MatchTaxonomy(string)` static helper and `QueryMatchesAsync(db, fixtureId)` for SilverLabelEngine integration. Options: `--db`, `--tier`, `--delay-ms`, `--limit`.
- **`nuget_advisory_enrichments` DB table**: Migration adds `nuget_advisory_enrichments (fixture_id, repo, packages_checked, advisory_count, highest_severity, advisories_json, scanned_at_utc)` with `UNIQUE(fixture_id)`.
- **`file_churn_enrichments` DB table**: Migration adds `file_churn_enrichments (id, fixture_id, repo, file_path, churn_90d, hotspot_score, fetched_at_utc)` with `UNIQUE(fixture_id, file_path)`.
- **`review_comment_nlp_enrichments` DB table**: Migration adds `review_comment_nlp_enrichments (id, fixture_id, repo, matched_rule_id, matched_keyword, confidence, fetched_at_utc)` with `UNIQUE(fixture_id, matched_rule_id)`.
- **Dependabot Tier 1 oracle** (`corpus dependabot enrich`): `DependabotEnricher` calls the GitHub PR API for each fixture, checks whether the PR author is `dependabot[bot]` / `dependabot-preview[bot]`, and also matches the Dependabot title pattern (`Bump X from Y to Z`). Results (including non-Dependabot rows) are written to the new `dependabot_matches` table so the `CompositeLabeler` can distinguish checked-and-clean from unchecked.
- **Dependabot GH Archive discovery** (`corpus dependabot discover`): Scans GH Archive hourly event files for `PullRequestEvent` records where the author login starts with `dependabot` and the repo language is `C#`. Matching PRs are seeded into the `candidates` table with `source='dependabot-gharchive'` for subsequent hydration. Options: `--start-date`, `--end-date`, `--limit`.
- **Social signal Tier 2 oracle** (`corpus social-signal enrich`): `SocialSignalEnricher` calls the GitHub PR API and the PR reviews API for each fixture. Computes `review_time_minutes` (created_at to merged_at), human `reviewer_count`, `review_comment_count`, and an `is_bot_merged` flag. These are combined into a `social_signal_score` (0.0 = low-validation, 1.0 = well-reviewed) using a weighted model. Scores below 0.3 are flagged as `LOW_VALIDATION` in the progress output. Results are written to `social_signal_enrichments`.
- **Composite ground-truth labeler** (`corpus composite-label apply`): `CompositeLabeler` reads all enricher signals (Sonar, CodeQL, Dependabot, Social Signal) per fixture and assigns a composite label per the Ground Truth Implementation Guide labeling matrix: `DEPENDABOT_FIX`, `HIGH_RISK_GHOST`, `SILENT_LOGIC_CHANGE`, `UNVALIDATED_BEHAVIORAL_RISK`, `STANDARD_CHANGE`, or `INSUFFICIENT_DATA`. With `--update-expected-findings`, writes rule-level `expected_findings` rows (INSERT OR IGNORE - never overwrites gold labels). GCI0012 targeted for `DEPENDABOT_FIX`; GCI0014 for `HIGH_RISK_GHOST`; GCI0036 for `SILENT_LOGIC_CHANGE`; GCI0003 for `UNVALIDATED_BEHAVIORAL_RISK`.
- **`dependabot_matches` DB table**: Migration adds `dependabot_matches (fixture_id, repo, pr_number, is_dependabot, pr_title, author_login, fetched_at_utc)` with `UNIQUE(fixture_id)`.
- **`social_signal_enrichments` DB table**: Migration adds `social_signal_enrichments (fixture_id, repo, pr_number, review_time_minutes, reviewer_count, review_comment_count, is_bot_merged, social_signal_score, fetched_at_utc)` with `UNIQUE(fixture_id)`.
- **`composite_labels` DB table**: Migration adds `composite_labels (fixture_id, composite_label, label_confidence, signals_json, applied_at_utc)` with `UNIQUE(fixture_id)`.
- **`LoadFixturesAsync` / `LoadFixturesWithPrAsync` helpers** in `CorpusCommand`: Extracted shared fixture-loading helpers used by all new enrichers. `LoadFixturesWithPrAsync` includes `pr_number` in the select for commands that need GitHub API calls.
- **Semgrep Tier 1 scanner oracle** (`corpus semgrep enrich`): `SemgrepEnricher` extracts newly-added `.cs` lines from each fixture's `diff.patch`, writes them to temp files, and invokes `semgrep --config=auto --json`. Findings are written to `semgrep_enrichments`. Fixtures with Semgrep hits + low social signal are classified as `HIGH_RISK_GHOST` by the composite labeler. Gracefully skips if semgrep is not on PATH.
- **Structural Tier 3 oracle** (`corpus structural enrich`): `StructuralEnricher` detects sensitive file paths in each diff (21 patterns: auth, oauth, token, secret, crypto, payment, etc.) and fetches 30-day per-file commit churn via the GitHub Commits API. Computes a `structural_risk_score` (0.0-1.0). Writes to `structural_enrichments`. Unlocks the new `HOT_PATH_UNREVIEWED` composite label for high-risk sensitive-path changes with no review.
- **`HOT_PATH_UNREVIEWED` composite label**: New label in `CompositeLabeler` for fixtures where a sensitive file path + high structural risk score + social signal < 0.5 coincide without any scanner match. Seeded to `expected_findings` targeting `GCI0003`.
- **`semgrep_enrichments` DB table**: Migration adds `semgrep_enrichments (fixture_id, repo, finding_count, rules_fired, highest_severity, findings_json, scanned_at_utc)` with `UNIQUE(fixture_id)`.
- **`structural_enrichments` DB table**: Migration adds `structural_enrichments (fixture_id, repo, changed_files_json, sensitive_file_count, max_file_churn_30d, structural_risk_score, fetched_at_utc)` with `UNIQUE(fixture_id)`.

(`--db`, `--fixtures`, `--tier`). `SonarCloudClient` discovers public SonarCloud projects via conventional `owner_repo` key with org-search fallback, then fetches all open BUG/VULNERABILITY issues (paginated, 1-second courtesy delay). `SonarCloudEnricher` parses `+++ b/` lines from `diff.patch` to extract changed `.cs` files, matches them against SonarCloud issue file paths, and writes results to the new `sonar_matches` table. Provides cross-validated ground truth: when both GauntletCI and SonarCloud flag the same file in the same PR, the finding is externally confirmed.
- **`sonar_matches` DB table**: New migration in `CorpusDb` adds `sonar_matches (fixture_id, sonar_project_key, changed_file, sonar_rule, sonar_severity, sonar_type, sonar_message, fetched_at_utc)` with `UNIQUE(fixture_id, changed_file, sonar_rule)` to prevent duplicates on re-runs.
- **GitHub Code Scanning (CodeQL) external label oracle**: New `corpus codescanning enrich` CLI command (`--db`, `--fixtures`, `--tier`, requires GitHub auth). `CodeScanningClient` fetches open CodeQL alerts for each repo via the GitHub Code Scanning API (100/page, 100ms courtesy delay, graceful 404 skip for repos without scanning enabled). `CodeScanningEnricher` parses `+++ b/` changed `.cs` paths from diffs, matches against alert file paths (no prefix stripping needed - GitHub returns repo-relative paths directly), and writes to the new `code_scanning_matches` table. Enrichment result summary reports repos with/without scanning, fixtures processed, and total matches written.
- **`code_scanning_matches` DB table**: Migration adds `code_scanning_matches (fixture_id, repo, changed_file, codeql_rule, codeql_rule_name, alert_state, tool_name, severity, start_line, message, fetched_at_utc)` with `UNIQUE(fixture_id, changed_file, codeql_rule)`.
- **`GitHubTokenResolver`**: New shared utility in `GauntletCI.Corpus` that resolves a GitHub token from `GITHUB_TOKEN` env var first, then falls back to `gh auth token` (GitHub CLI credential store). All corpus HTTP clients (`GitHubRestHydrator`, `IssueEnricher`, `MaintainerFetcher`, `CodeScanningClient`) and `LlmLabelerFactory` now use it. Corpus commands that previously required `GITHUB_TOKEN` to be set explicitly will now work automatically when the user is authenticated via `gh auth login`.

### Changed
- **Corpus (repo allowlist)**: Added 6 domain-targeted repositories to `build-corpus.ps1` allowlist: MassTransit/MassTransit (saga/idempotency patterns), dotnet/winforms (IDisposable chains), microsoft/semantic-kernel (async/retry/naming churn), abpframework/abp (DDD interfaces, EF migrations), OrchardCMS/OrchardCore (CMS schema migrations), nhibernate/nhibernate-core (ORM state graphs). Added 30 new Silver fixtures (618 total). GCI0039 entered the scorecard for the first time: 75.0% precision, 42.9% recall.
- **GCI0029 (PII Logging Leak)**:Skip XML doc comment lines (`///`). PII term false positives in documentation comments were causing spurious findings. Corpus precision: 62.5% -> 80.0%.
- **GCI0046 (Pattern Consistency Deviation)**: Added `FrameworkExemptPairs` HashSet for standard sync+async pairs (Dispose/DisposeAsync, Flush/FlushAsync, Open/OpenAsync, Close/CloseAsync, Subscribe/SubscribeAsync, Read/ReadAsync, Write/WriteAsync, Execute/ExecuteAsync, etc.). These are idiomatic framework patterns, not rule violations. Corpus precision: 68.2% -> 87.5%.
- **GCI0041 (Test Quality Gaps)**: Expanded `AssertionKeywords` from 6 to 16 entries, covering xUnit, NUnit, FluentAssertions, Shouldly, NSubstitute, Moq, and FakeItEasy assertion patterns. `CheckEmptyAssertions` now scans all visible hunk lines (not just added lines) to catch test methods whose body was already present as context. Corpus precision: 64.7% -> 69.6%.
- **GCI0045 (Complexity Control)**: `CheckAbstractClassWithNoAbstractMembers` now skips abstract classes that inherit from a base type (`:` in the class declaration) - the contract comes from the ancestor. Also scans all visible hunk lines for abstract members to avoid missing pre-existing members. `CheckSingleUseInterface` now skips test files via `WellKnownPatterns.IsTestFile`. Corpus precision: 71.8% -> 83.3%.
- **SilverLabelEngine (phantom rule removal)**: Removed GCI0005 and GCI0023 from `RulesWithHeuristics`, `CommentRules`, and `ApplyDiffHeuristics`. Neither rule has an implementation file - the heuristics were generating orphaned expected.json labels that polluted the scorecard. Redirected the "needs tests/untested" comment keyword to GCI0041; redirected the "rename/sweeping change" keyword to GCI0047.
- **SilverLabelEngine (GCI0016 heuristic)**: Tightened `.Result`/`.Wait()` heuristic to require `Task` or `Async` context on the same line, preventing false positive labels from `response.Result` or `DbResult.Value` property accesses.
- **SilverLabelEngine (GCI0010 heuristic)**: Tightened URL string literal heuristic to only fire on localhost/IP URLs or connection string patterns. Previously any HTTPS URL in a string literal triggered a positive label - extremely common in test fixtures.
- **SilverLabelEngine (GCI0047 heuristic)**: Tightened CRUD-verb contradiction heuristic to extract `(Verb, Base)` tuples from removed/added lines and require the same base name appear with a different verb. Prevents false labels when unrelated CRUD verbs appear on either side of a diff.
- **GCI0022 (Idempotency/Retry Safety)**:Added generic-type event channel suppression. Subscriptions of the form `MessageBus<T>.Subscribers += ...` (where a generic type argument appears before `+=`) are now excluded - static typed event bus patterns are not idempotency risks. Corpus precision: 50.0% -> 66.7%.
- **GCI0029 (PII Logging Leak)**: Test and example files are now skipped entirely using `WellKnownPatterns.IsTestFile`. PII term matches in test fixtures and SDK example projects were generating spurious findings. Corpus precision: 54.5% -> 62.5%.
- **GCI0036 (Pure Context Mutation)**: Added null-guard detection for lazy-init assignments in property getters. Assignments of the form `_field = value;` that are preceded by `if (_field is null)` or a `!= null` early-return guard are recognized as intentional lazy initialization and no longer flagged. Corpus precision: 40.0% -> 42.9%.
- **SilverLabelEngine (GCI0047 heuristic)**: Fixed CRUD-verb contradiction heuristic to use actual removed diff lines instead of `--- a/path` file headers. The old code searched path header lines for method names, which always found nothing; the fix uses `-` lines from the actual patch body. GCI0047 corpus precision: 0% -> 100%.
- **SilverLabelEngine (GCI0021 heuristic)**: Extended the positive-label signal to also detect removed serialization attributes (`[JsonProperty`, `[DataMember`, `[Column(`, `[BsonElement`, `[Key]`, `[ForeignKey`, `[Required]`). Previously only migration-keyword file paths triggered a positive label. Corpus precision: 28.6% -> 42.9%.
- **SilverLabelEngine**: Extended `RulesWithHeuristics` from 9 to 24 rules.Added comment keyword mappings and diff heuristics for 14 previously unlabeled rules (GCI0024, GCI0029, GCI0032, GCI0036, GCI0038, GCI0039, GCI0041, GCI0042, GCI0043, GCI0044, GCI0045, GCI0046, GCI0047, GCI0049). Re-ran `label-all` on 588 Silver fixtures, unlocking precision measurement for all rules. Notable first readings: GCI0032 98.1%, GCI0043 90.3%, GCI0042 85.3%, GCI0039 85.7%, GCI0038 82.8%, GCI0049 100.0%.
- **GCI0016 (Concurrency and State Risk)**:Refactored from `AllAddedLines` iteration to per-file iteration with path context. Auto-generated files (`/Generated/`, `.g.cs`, `.Designer.cs`, etc.) are now skipped entirely - noise from generated API clients eliminated. `Thread.Sleep` check now skips test files (timing control in tests is legitimate). Corpus precision: 57.4% -> 66.1%.
- **GCI0003 (Behavioral Change Detection)**:Raised logic-removal threshold from 3 to 5 removed lines before flagging. Reduces FPs on small incidental removals. Corpus precision: 59.7% -> 60.7%.
- **GCI0004 (Breaking Change Risk)**: Replaced local `IsTestFile` with `WellKnownPatterns.IsTestFile` (covers benchmark/example/sample/Mock/Fake paths missed by the old check). Added `StripPropertyInitializer` to skip property default-value-only changes. Corpus precision: 59.9% -> 63.8%.
- **WellKnownPatterns.IsTestFile**: Added detection for benchmark/example/sample directory segments, `Mock`/`Fake` segments, and `Benchmark`/`Benchmarks` file suffixes.
- **WellKnownPatterns.IsBackwardCompatibleExtension**: Now normalizes modifier keywords (`virtual`, `override`, `sealed`, `abstract`, `new`) before comparing signatures so modifier-only additions are treated as backward-compatible rather than breaking changes.
- **GCI0016 (Concurrency and State Risk)**:`CheckStaticMutableField` now excludes expression-bodied members (`=>`) and property declarations (`{`) from the "static mutable field" check. These are read-only or accessor-only patterns that the rule was incorrectly flagging. Corpus: 41 auto-property/expression-body FPs eliminated.
- **GCI0021 (Data and Schema Compatibility)**: Added `WellKnownPatterns.IsGeneratedFile` guard to both `CheckRemovedSerializationAttributes` and `CheckRemovedEnumMembers`. Generated API clients (e.g. Google API dotnet client, protobuf stubs) no longer trigger the rule for mechanical enum-member removals.
- **GCI0012 (Security Risk)**: Fixed `HasAssignment` and `FindAssignmentIndex` to skip `=` signs inside string literals (eliminates false positives from format strings like `"[Token: Id={0}]"`). Fixed both helpers to treat `=>` (expression-body / lambda) as non-assignment. Added `Activator.CreateInstance` exclusion for controlled-type-safe call patterns: calls where the type argument is a `typeof()` literal or where the result is immediately cast to a known type (e.g. `(IList)Activator.CreateInstance(...)`) are no longer flagged as dangerous.


- **GCI0006 (Edge Case Handling)**: Reduced false positives in CheckNullDereferences and CheckMissingParameterValidation. CheckNullDereferences now skips `.Value!` (null-forgiving operator), `.Value?.` (null-conditional after), `?.Value` (null-conditional before), `.Values` (different property), and expression-bodied declarations. CheckMissingParameterValidation now skips `override`, `abstract`, and `delegate` declarations where a method body is absent or the parameter contract is fixed by the base type. Newtonsoft PR#1950: 57 -> 32 GCI0006 findings (-44%).
- **GCI0047 (Naming/Contract Alignment)**: Added generated-file guard (skips `Src/Generated/` and similar paths); deduplicated CRUD-verb contradiction findings per unique verb-pair per file, eliminating N x M explosion in auto-generated API clients. Corpus findings: 725 -> 5 (-99.3%).
- **GCI0004 (Breaking Change Risk)**: Added `WellKnownPatterns.IsGeneratedFile` check to both `CheckRemovedPublicApi` and `CheckObsoleteRemoved`, excluding Azure SDK `.net{major}.{minor}.cs` API surface manifest files. Corpus findings: 4368 -> 2452 (-43.9%).
- **GCI0015 (Data Integrity Risk)**: Gated `CheckUncheckedCasts` behind an HTTP input context signal (Request.Form, [FromBody], etc.). Internal numeric casts no longer trigger the rule; only casts in files that also contain HTTP input bindings are flagged. Corpus findings: 579 -> 241 (-58.3%).
- **GCI0015 (Data Integrity Risk)**: Gated `CheckMassAssignment` behind the same HTTP context signal. Mass field assignments in non-HTTP libraries (image format parsers, JSON serializers, data decoders) no longer trigger the rule. ImageSharp PR#3096: 11 false positives eliminated.
- **GCI0003 (Behavioral Change Detection)**: Inherits the `WellKnownPatterns.IsGeneratedFile` improvement; `.net{major}.{minor}.cs` API surface files are now excluded. Corpus findings: 2566 -> 1904 (-25.8%).
- **GCI0032 (Uncaught Exception Path)**: Excludes guard-clause throws (ArgumentNullException, ArgumentException, ArgumentOutOfRangeException, ObjectDisposedException) from the uncovered-throw count. These are defensive precondition checks that do not represent untested business logic. Corpus findings: 86 -> 54 (-37.2%).
- **WellKnownPatterns.IsGeneratedFile**: Added `.net{major}.{minor}.cs` filename pattern to detect .NET SDK API surface manifest files (e.g. `Azure.Search.Documents.net10.0.cs`).
- **WellKnownPatterns**: Extracted `IsBackwardCompatibleExtension` and `ExtractParenContent` from GCI0003 and GCI0004 into `WellKnownPatterns` - both helpers were identical duplicates; GCI0003/GCI0004 now delegate to the shared versions.
- **GCI0003 (Behavioral Change Detection)**: Rewrote `CheckMethodSignatureChanges` to emit at most 2 findings per file (one for incompatible, one for compatible changes) instead of N findings per changed method. Corpus findings: 1904 -> 548 (-71.2%).
- **GCI0004 (Breaking Change Risk)**: Rewrote `CheckRemovedPublicApi` to emit at most 2 findings per file (one for removals, one for sig changes) instead of N findings per removed member. Corpus findings: 2452 -> 686 (-72.0%).
- **GCI0003 (Behavioral Change Detection)**: Added cross-diff deduplication - when more than 3 files are affected, collapses per-file findings into a single cross-diff summary ("N method signatures changed across M files"). Azure SDK PR57223: 3 GCI0003 findings (was proportional to file count).
- **GCI0004 (Breaking Change Risk)**: Added cross-diff deduplication - same >3 files threshold collapses removals and sig-change findings to single summary findings. Azure SDK PR57223: 688 findings -> 20 across all rules (-97.1%).
- **DiffParser**: All public async methods now accept a `contextLines` parameter (default 10, up from git default of 3); wired through `GauntletConfig.DiffContextLines` so it is configurable per repo.
- **Overall corpus noise**: 10,262 -> 3,472 findings across 588 real .NET OSS PR diffs (-66.2%).

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
- `output.sensitivity` config key in `.gauntletci.json`; accepts `strict`, `balanced`, or `permissive`; CLI flag overrides it when explicitly passed
- Site: "GauntletCI vs AI code review tools" compare page at `/compare/gauntletci-vs-ai-code-review`
- Site: "What it checks / What it misses" orientation table added to the top of all 6 existing compare pages
- Site: "Compare vs AI code review tools" link added to homepage comparison section
- Site: README now includes "The Change That Looked Safe" concrete diff example as a Behavioral Change Risk anchor
- README: Killer PR example section with diff, consequence, and GauntletCI output
- README: Punchy intro block with BCR framing; "The Missing Layer: Change Validation" section; h1 simplified to "GauntletCI"
- GitHub: Repo description updated to lead with "Behavioral Change Risk detection"; topics updated -- removed `sast`, `static-analysis`, `roslyn-analyzer`, `behavior-change-risk`; added `behavioral-change-risk`, `change-safety`, `diff-validation`

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

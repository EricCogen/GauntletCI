// SPDX-License-Identifier: Elastic-2.0
using System.Text.Json;
using GauntletCI.Corpus.Models;
using GauntletCI.Corpus.Storage;

namespace GauntletCI.Corpus.Labeling;

/// <summary>
/// Combines all enricher signals (Tier 1: Sonar/CodeQL/Dependabot, Tier 2: Social Signal)
/// to assign a composite ground-truth label per fixture, per the labeling matrix in the
/// Ground Truth Implementation Guide.
///
/// Label hierarchy (evaluated in order):
/// <list type="number">
///   <item><see cref="LabelDependabotFix"/> - PR was authored by Dependabot (highest-confidence Tier 1)</item>
///   <item><see cref="LabelHighRiskGhost"/> - Scanner alert present AND low social validation</item>
///   <item><see cref="LabelSilentLogicChange"/> - No scanner match, low-medium validation, structural diff</item>
///   <item><see cref="LabelUnvalidatedBehavioralRisk"/> - Very low validation, no scanner match</item>
///   <item><see cref="LabelStandardChange"/> - Well-reviewed, no scanner alerts</item>
///   <item><see cref="LabelInsufficientData"/> - Not enough signal to classify</item>
/// </list>
///
/// Results are written to the <c>composite_labels</c> table.
/// Optionally updates <c>expected_findings</c> to seed rule-level training labels.
/// </summary>
public sealed class CompositeLabeler
{
    public const string LabelDependabotFix            = "DEPENDABOT_FIX";
    public const string LabelHighRiskGhost            = "HIGH_RISK_GHOST";
    public const string LabelSilentLogicChange        = "SILENT_LOGIC_CHANGE";
    public const string LabelUnvalidatedBehavioralRisk = "UNVALIDATED_BEHAVIORAL_RISK";
    public const string LabelStandardChange           = "STANDARD_CHANGE";
    public const string LabelHotPathUnreviewed        = "HOT_PATH_UNREVIEWED";
    public const string LabelInsufficientData         = "INSUFFICIENT_DATA";

    // Guide thresholds
    private const double LowValidationThreshold  = 0.3;
    private const double HighValidationThreshold = 0.6;

    // Rule targets written to expected_findings when updateExpectedFindings = true
    private static readonly IReadOnlyDictionary<string, (string RuleId, string Reason)[]> LabelRuleMap =
        new Dictionary<string, (string, string)[]>(StringComparer.Ordinal)
        {
            [LabelDependabotFix] =
            [
                ("GCI0012", "Dependabot dependency-vulnerability-fix PR - likely contains hardcoded package reference change"),
            ],
            [LabelHighRiskGhost] =
            [
                ("GCI0014", "Scanner match + low-validation merge = complex ghost change (GCI0014 ComplexLogicChange)"),
            ],
            [LabelSilentLogicChange] =
            [
                ("GCI0036", "Logic change not caught by standard scanners; tune Roslyn branch detection"),
            ],
            [LabelUnvalidatedBehavioralRisk] =
            [
                ("GCI0003", "Unvalidated behavioral change; likely swallowed exception or missing error handling"),
            ],
            [LabelHotPathUnreviewed] =
            [
                ("GCI0003", "Sensitive file path changed with high structural risk and low social validation"),
            ],
        };

    /// <summary>
    /// Reads enrichment signals for each fixture and writes a composite label to the DB.
    /// </summary>
    /// <param name="updateExpectedFindings">
    /// When <c>true</c>, inserts rule-level <c>expected_findings</c> rows based on the composite label
    /// (INSERT OR IGNORE - never overwrites human/gold labels).
    /// </param>
    public async Task<CompositeLabelerResult> ApplyAsync(
        IEnumerable<FixtureMetadata> fixtures,
        CorpusDb db,
        bool updateExpectedFindings = false,
        Action<string>? progress = null,
        CancellationToken ct = default)
    {
        var result = new CompositeLabelerResult();

        foreach (var fixture in fixtures)
        {
            ct.ThrowIfCancellationRequested();

            var signals    = await ReadSignalsAsync(db, fixture.FixtureId, ct);
            var label      = ClassifyLabel(signals);
            var confidence = ComputeConfidence(signals, label);

            await WriteCompositeLabelAsync(db, fixture.FixtureId, label, confidence, signals, ct);
            result.FixturesLabeled++;

            IncrementBucket(result, label);

            if (label is not (LabelStandardChange or LabelInsufficientData))
                progress?.Invoke($"[composite] {fixture.FixtureId}: {label} (conf={confidence:F2})");

            if (updateExpectedFindings && label != LabelInsufficientData)
                await UpdateExpectedFindingsAsync(db, fixture.FixtureId, label, confidence, ct);

            // EF migration always warrants a GCI0021 expected finding regardless of composite label
            if (updateExpectedFindings && signals.HasEfMigrationData && signals.MigrationDetected)
                await WriteEfMigrationFindingAsync(db, fixture.FixtureId, signals.MigrationConfidence, ct);
        }

        return result;
    }

    // ── classification ────────────────────────────────────────────────────────

    private static string ClassifyLabel(FixtureSignals s)
    {
        // No enrichment data at all -> cannot classify
        if (!s.HasDependabotData && !s.HasSocialSignalData &&
            s.SonarMatchCount == 0 && s.CodeQlMatchCount == 0 &&
            !s.HasSemgrepData && !s.HasStructuralData &&
            !s.HasNuGetAdvisoryData && !s.HasChurnData && !s.HasNlpData &&
            !s.HasTestCoverageData && !s.HasEntropyData && !s.HasEfMigrationData)
            return LabelInsufficientData;

        // Tier 1 Dependabot - highest confidence
        if (s.IsDependabot) return LabelDependabotFix;

        // EF migration detected -> HIGH_RISK_GHOST (schema changes are high-confidence)
        if (s.HasEfMigrationData && s.MigrationDetected)
            return LabelHighRiskGhost;

        // NuGet vulnerability in diff = high risk
        if (s.HasNuGetAdvisoryData && s.NuGetAdvisoryCount > 0 && !s.IsDependabot)
            return LabelHighRiskGhost;

        var hasScannerMatch  = s.SonarMatchCount > 0 || s.CodeQlMatchCount > 0
                            || (s.HasSemgrepData && s.SemgrepFindingCount > 0);
        var hasLowValidation = s.HasSocialSignalData && s.SocialSignalScore < LowValidationThreshold;
        var hasHighValidation = s.HasSocialSignalData && s.SocialSignalScore >= HighValidationThreshold;

        // Guide: Sonar(Complexity Up) + GitHub(No Review) -> HIGH_RISK_GHOST
        if (hasScannerMatch && hasLowValidation)  return LabelHighRiskGhost;

        // Scanner match but well-reviewed is still a real finding
        if (hasScannerMatch && hasHighValidation) return LabelHighRiskGhost;

        // Scanner match with unknown social signal
        if (hasScannerMatch) return LabelHighRiskGhost;

        // Structural: sensitive path + high risk score + low social validation -> HOT_PATH_UNREVIEWED
        if (s.HasStructuralData && s.HasSensitivePath &&
            s.StructuralRiskScore >= 0.6 && !hasScannerMatch &&
            s.HasSocialSignalData && s.SocialSignalScore < 0.5)
            return LabelHotPathUnreviewed;

        // File churn: high hotspot score + low social validation -> HOT_PATH_UNREVIEWED
        if (s.HasChurnData && s.MaxHotspotScore >= 0.7 &&
            s.HasSocialSignalData && s.SocialSignalScore < 0.5)
            return LabelHotPathUnreviewed;

        // Diff entropy: highly scattered change + very low social validation -> HOT_PATH_UNREVIEWED
        if (s.HasEntropyData && s.NormalizedEntropy >= 0.8 &&
            s.HasSocialSignalData && s.SocialScore < 0.4 && !hasScannerMatch)
            return LabelHotPathUnreviewed;

        // Guide: Snyk(Clean) + LibGit2Sharp(Logic Diff > 0) -> SILENT_LOGIC_CHANGE
        // Proxy: social signal exists (diff is real), no scanner, low-medium validation, sparse review
        // High entropy amplifies scatter signal for SILENT_LOGIC_CHANGE classification
        if (!hasScannerMatch && s.HasSocialSignalData &&
            s.SocialSignalScore < HighValidationThreshold &&
            s.ReviewerCount <= 1)
            return LabelSilentLogicChange;

        // Test coverage gap with moderate social signal -> UNVALIDATED_BEHAVIORAL_RISK
        if (s.HasTestCoverageData && s.TestCoverageGap && !hasScannerMatch &&
            s.HasSocialSignalData && s.SocialScore < 0.5)
            return LabelUnvalidatedBehavioralRisk;

        // WIP/empty PR with very low validation -> UNVALIDATED_BEHAVIORAL_RISK
        if (s.HasPrDescriptionData && s.IsEmptyPrBody && s.HasWipKeywords &&
            hasLowValidation && !hasScannerMatch)
            return LabelUnvalidatedBehavioralRisk;

        // First-time contributor + empty body -> amplified UNVALIDATED_BEHAVIORAL_RISK signal
        if (s.HasAuthorData && s.IsFirstContributor &&
            s.HasPrDescriptionData && s.IsEmptyPrBody && !hasScannerMatch)
            return LabelUnvalidatedBehavioralRisk;

        // Very low validation, no scanner hit -> UNVALIDATED_BEHAVIORAL_RISK
        if (hasLowValidation && !hasScannerMatch)
            return LabelUnvalidatedBehavioralRisk;

        // Well-reviewed, no scanner alerts -> STANDARD_CHANGE
        if (hasHighValidation && !hasScannerMatch)
            return LabelStandardChange;

        return LabelInsufficientData;
    }

    private static double ComputeConfidence(FixtureSignals s, string label) => label switch
    {
        LabelDependabotFix             => 0.95,
        LabelHighRiskGhost             => s.SonarMatchCount > 0 || s.CodeQlMatchCount > 0 ? 0.80
                                        : s.HasNuGetAdvisoryData && s.NuGetAdvisoryCount > 0 ? 0.85
                                        : 0.60,
        LabelSilentLogicChange         => 0.55,
        LabelUnvalidatedBehavioralRisk => s.HasPrDescriptionData && s.HasLinkedIssue ? 0.40 : 0.50,
        LabelHotPathUnreviewed         => 0.65,
        LabelStandardChange            => s.ReviewerCount >= 2 ? 0.75 : 0.60,
        _                              => 0.0,
    };

    private static void IncrementBucket(CompositeLabelerResult r, string label)
    {
        switch (label)
        {
            case LabelDependabotFix:            r.DependabotFix++;            break;
            case LabelHighRiskGhost:            r.HighRiskGhost++;            break;
            case LabelSilentLogicChange:        r.SilentLogicChange++;        break;
            case LabelUnvalidatedBehavioralRisk: r.UnvalidatedBehavioralRisk++; break;
            case LabelHotPathUnreviewed:        r.HotPathUnreviewed++;        break;
            case LabelStandardChange:           r.StandardChange++;           break;
            default:                            r.InsufficientData++;         break;
        }
    }

    // ── DB reads ──────────────────────────────────────────────────────────────

    private static async Task<FixtureSignals> ReadSignalsAsync(
        CorpusDb db, string fixtureId, CancellationToken ct)
    {
        var s = new FixtureSignals();

        using (var cmd = db.Connection.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM sonar_matches WHERE fixture_id = $id";
            cmd.Parameters.AddWithValue("$id", fixtureId);
            s.SonarMatchCount = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct) ?? 0);
        }

        using (var cmd = db.Connection.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM code_scanning_matches WHERE fixture_id = $id";
            cmd.Parameters.AddWithValue("$id", fixtureId);
            s.CodeQlMatchCount = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct) ?? 0);
        }

        using (var cmd = db.Connection.CreateCommand())
        {
            cmd.CommandText = "SELECT is_dependabot FROM dependabot_matches WHERE fixture_id = $id";
            cmd.Parameters.AddWithValue("$id", fixtureId);
            var val = await cmd.ExecuteScalarAsync(ct);
            s.HasDependabotData = val is not null;
            s.IsDependabot      = val is not null && Convert.ToInt32(val) == 1;
        }

        using (var cmd = db.Connection.CreateCommand())
        {
            cmd.CommandText = """
                SELECT social_signal_score, review_time_minutes, reviewer_count
                FROM   social_signal_enrichments
                WHERE  fixture_id = $id
                """;
            cmd.Parameters.AddWithValue("$id", fixtureId);
            using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                s.SocialSignalScore   = reader.GetDouble(0);
                s.ReviewTimeMinutes   = reader.GetDouble(1);
                s.ReviewerCount       = reader.GetInt32(2);
                s.HasSocialSignalData = true;
            }
        }

        using (var cmd = db.Connection.CreateCommand())
        {
            cmd.CommandText = """
                SELECT finding_count
                FROM   semgrep_enrichments
                WHERE  fixture_id = $id
                """;
            cmd.Parameters.AddWithValue("$id", fixtureId);
            var val = await cmd.ExecuteScalarAsync(ct);
            if (val is not null)
            {
                s.HasSemgrepData      = true;
                s.SemgrepFindingCount = Convert.ToInt32(val);
            }
        }

        using (var cmd = db.Connection.CreateCommand())
        {
            cmd.CommandText = """
                SELECT sensitive_file_count, structural_risk_score
                FROM   structural_enrichments
                WHERE  fixture_id = $id
                """;
            cmd.Parameters.AddWithValue("$id", fixtureId);
            using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                s.HasStructuralData   = true;
                s.HasSensitivePath    = reader.GetInt32(0) > 0;
                s.StructuralRiskScore = reader.GetDouble(1);
            }
        }

        using (var cmd = db.Connection.CreateCommand())
        {
            cmd.CommandText = "SELECT advisory_count FROM nuget_advisory_enrichments WHERE fixture_id = $id";
            cmd.Parameters.AddWithValue("$id", fixtureId);
            var val = await cmd.ExecuteScalarAsync(ct);
            if (val is not null)
            {
                s.HasNuGetAdvisoryData = true;
                s.NuGetAdvisoryCount   = Convert.ToInt32(val);
            }
        }

        using (var cmd = db.Connection.CreateCommand())
        {
            cmd.CommandText = """
                SELECT COALESCE(MAX(hotspot_score), 0.0)
                FROM   file_churn_enrichments
                WHERE  fixture_id = $id
                """;
            cmd.Parameters.AddWithValue("$id", fixtureId);
            var val = await cmd.ExecuteScalarAsync(ct);
            if (val is not null)
            {
                s.HasChurnData    = true;
                s.MaxHotspotScore = Convert.ToDouble(val);
            }
        }

        using (var cmd = db.Connection.CreateCommand())
        {
            cmd.CommandText = "SELECT matched_rule_id FROM review_comment_nlp_enrichments WHERE fixture_id = $id";
            cmd.Parameters.AddWithValue("$id", fixtureId);
            using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                s.NlpMatchedRuleIds.Add(reader.GetString(0));
                s.HasNlpData = true;
            }
        }

        using (var cmd = db.Connection.CreateCommand())
        {
            cmd.CommandText = """
                SELECT test_coverage_gap, test_to_prod_ratio
                FROM   test_coverage_enrichments
                WHERE  fixture_id = $id
                """;
            cmd.Parameters.AddWithValue("$id", fixtureId);
            using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                s.HasTestCoverageData = true;
                s.TestCoverageGap     = reader.GetInt32(0) == 1;
                s.TestToProdRatio     = reader.GetDouble(1);
            }
        }

        using (var cmd = db.Connection.CreateCommand())
        {
            cmd.CommandText = """
                SELECT normalized_entropy, file_count
                FROM   diff_entropy_enrichments
                WHERE  fixture_id = $id
                """;
            cmd.Parameters.AddWithValue("$id", fixtureId);
            using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                s.HasEntropyData    = true;
                s.NormalizedEntropy = reader.GetDouble(0);
                s.ChangeFileCount   = reader.GetInt32(1);
            }
        }

        using (var cmd = db.Connection.CreateCommand())
        {
            cmd.CommandText = """
                SELECT migration_detected, migration_confidence
                FROM   ef_migration_enrichments
                WHERE  fixture_id = $id
                """;
            cmd.Parameters.AddWithValue("$id", fixtureId);
            using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                s.HasEfMigrationData  = true;
                s.MigrationDetected   = reader.GetInt32(0) == 1;
                s.MigrationConfidence = reader.GetDouble(1);
            }
        }

        using (var cmd = db.Connection.CreateCommand())
        {
            cmd.CommandText = """
                SELECT is_empty_body, has_linked_issue, has_wip_keywords
                FROM   pr_description_enrichments
                WHERE  fixture_id = $id
                """;
            cmd.Parameters.AddWithValue("$id", fixtureId);
            using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                s.HasPrDescriptionData = true;
                s.IsEmptyPrBody        = reader.GetInt32(0) == 1;
                s.HasLinkedIssue       = reader.GetInt32(1) == 1;
                s.HasWipKeywords       = reader.GetInt32(2) == 1;
            }
        }

        using (var cmd = db.Connection.CreateCommand())
        {
            cmd.CommandText = """
                SELECT is_first_contributor, experience_tier
                FROM   author_experience_enrichments
                WHERE  fixture_id = $id
                """;
            cmd.Parameters.AddWithValue("$id", fixtureId);
            using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                s.HasAuthorData        = true;
                s.IsFirstContributor   = reader.GetInt32(0) == 1;
                s.AuthorExperienceTier = reader.GetString(1);
            }
        }

        return s;
    }

    // ── DB writes ─────────────────────────────────────────────────────────────

    private static async Task WriteCompositeLabelAsync(
        CorpusDb db, string fixtureId, string label, double confidence,
        FixtureSignals signals, CancellationToken ct)
    {
        var signalsJson = JsonSerializer.Serialize(new
        {
            sonar_matches          = signals.SonarMatchCount,
            codeql_matches         = signals.CodeQlMatchCount,
            is_dependabot          = signals.IsDependabot,
            social_score           = signals.HasSocialSignalData ? signals.SocialSignalScore : (double?)null,
            review_time_min        = signals.ReviewTimeMinutes,
            reviewer_count         = signals.ReviewerCount,
            semgrep_findings       = signals.HasSemgrepData ? signals.SemgrepFindingCount : (int?)null,
            structural_risk_score  = signals.HasStructuralData ? signals.StructuralRiskScore : (double?)null,
            has_sensitive_path     = signals.HasStructuralData ? signals.HasSensitivePath : (bool?)null,
            nuget_advisories       = signals.HasNuGetAdvisoryData ? signals.NuGetAdvisoryCount : (int?)null,
            max_hotspot_score      = signals.HasChurnData ? signals.MaxHotspotScore : (double?)null,
            nlp_matched_rules      = signals.HasNlpData ? signals.NlpMatchedRuleIds.Count : (int?)null,
            test_coverage_gap      = signals.HasTestCoverageData ? (bool?)signals.TestCoverageGap : null,
            normalized_entropy     = signals.HasEntropyData ? (double?)signals.NormalizedEntropy : null,
            migration_detected     = signals.HasEfMigrationData ? (bool?)signals.MigrationDetected : null,
            is_empty_pr_body       = signals.HasPrDescriptionData ? (bool?)signals.IsEmptyPrBody : null,
            has_linked_issue       = signals.HasPrDescriptionData ? (bool?)signals.HasLinkedIssue : null,
            is_first_contributor   = signals.HasAuthorData ? (bool?)signals.IsFirstContributor : null,
            author_experience_tier = signals.HasAuthorData ? signals.AuthorExperienceTier : null,
        });

        using var cmd = db.Connection.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO composite_labels
                (fixture_id, composite_label, label_confidence, signals_json)
            VALUES
                ($id, $label, $conf, $signals)
            """;
        cmd.Parameters.AddWithValue("$id",      fixtureId);
        cmd.Parameters.AddWithValue("$label",   label);
        cmd.Parameters.AddWithValue("$conf",    confidence);
        cmd.Parameters.AddWithValue("$signals", signalsJson);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task UpdateExpectedFindingsAsync(
        CorpusDb db, string fixtureId, string label, double confidence, CancellationToken ct)
    {
        if (!LabelRuleMap.TryGetValue(label, out var targets)) return;

        foreach (var (ruleId, reason) in targets)
        {
            var id = $"{fixtureId}_{ruleId}_composite";

            using var cmd = db.Connection.CreateCommand();
            cmd.CommandText = """
                INSERT OR IGNORE INTO expected_findings
                    (id, fixture_id, rule_id, should_trigger, expected_confidence, reason, label_source)
                VALUES
                    ($id, $fixtureId, $ruleId, 1, $conf, $reason, 'composite-labeler')
                """;
            cmd.Parameters.AddWithValue("$id",        id);
            cmd.Parameters.AddWithValue("$fixtureId", fixtureId);
            cmd.Parameters.AddWithValue("$ruleId",    ruleId);
            cmd.Parameters.AddWithValue("$conf",      confidence);
            cmd.Parameters.AddWithValue("$reason",    reason);
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }

    private static async Task WriteEfMigrationFindingAsync(
        CorpusDb db, string fixtureId, double confidence, CancellationToken ct)
    {
        var id     = $"{fixtureId}_GCI0021_ef-migration";
        var reason = "EF Core migration or SQL DDL change detected in diff - schema change risk";

        using var cmd = db.Connection.CreateCommand();
        cmd.CommandText = """
            INSERT OR IGNORE INTO expected_findings
                (id, fixture_id, rule_id, should_trigger, expected_confidence, reason, label_source)
            VALUES
                ($id, $fixtureId, 'GCI0021', 1, $conf, $reason, 'ef-migration-enricher')
            """;
        cmd.Parameters.AddWithValue("$id",        id);
        cmd.Parameters.AddWithValue("$fixtureId", fixtureId);
        cmd.Parameters.AddWithValue("$conf",      confidence);
        cmd.Parameters.AddWithValue("$reason",    reason);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ── internal signal bag ───────────────────────────────────────────────────

    private sealed class FixtureSignals
    {
        public int    SonarMatchCount     { get; set; }
        public int    CodeQlMatchCount    { get; set; }
        public bool   IsDependabot        { get; set; }
        public bool   HasDependabotData   { get; set; }
        public double SocialSignalScore   { get; set; }
        public double ReviewTimeMinutes   { get; set; }
        public int    ReviewerCount       { get; set; }
        public bool   HasSocialSignalData { get; set; }
        public bool   HasSemgrepData      { get; set; }
        public int    SemgrepFindingCount { get; set; }
        public bool   HasStructuralData   { get; set; }
        public double StructuralRiskScore { get; set; }
        public bool   HasSensitivePath    { get; set; }
        public int    NuGetAdvisoryCount  { get; set; }
        public bool   HasNuGetAdvisoryData { get; set; }
        public bool   HasChurnData        { get; set; }
        public double MaxHotspotScore     { get; set; }
        public HashSet<string> NlpMatchedRuleIds { get; set; } = new(StringComparer.Ordinal);
        public bool   HasNlpData          { get; set; }

        // Test coverage enrichment signals
        public bool   HasTestCoverageData { get; set; }
        public bool   TestCoverageGap     { get; set; }
        public double TestToProdRatio     { get; set; }

        // Diff entropy enrichment signals
        public bool   HasEntropyData      { get; set; }
        public double NormalizedEntropy   { get; set; }
        public int    ChangeFileCount     { get; set; }

        // EF migration enrichment signals
        public bool   HasEfMigrationData  { get; set; }
        public bool   MigrationDetected   { get; set; }
        public double MigrationConfidence { get; set; }

        // PR description enrichment signals
        public bool   HasPrDescriptionData { get; set; }
        public bool   IsEmptyPrBody        { get; set; }
        public bool   HasLinkedIssue       { get; set; }
        public bool   HasWipKeywords       { get; set; }

        // Author experience enrichment signals
        public bool   HasAuthorData           { get; set; }
        public bool   IsFirstContributor      { get; set; }
        public string AuthorExperienceTier    { get; set; } = "unknown";

        // Alias used in ClassifyLabel for readability
        public double SocialScore => SocialSignalScore;
    }
}

/// <summary>Summary statistics from a <see cref="CompositeLabeler.ApplyAsync"/> run.</summary>
public sealed class CompositeLabelerResult
{
    public int FixturesLabeled           { get; set; }
    public int DependabotFix             { get; set; }
    public int HighRiskGhost             { get; set; }
    public int SilentLogicChange         { get; set; }
    public int UnvalidatedBehavioralRisk { get; set; }
    public int HotPathUnreviewed         { get; set; }
    public int StandardChange            { get; set; }
    public int InsufficientData          { get; set; }
}
